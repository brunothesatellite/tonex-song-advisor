using TonexAdvisor.Core.Data;
using TonexAdvisor.Core.Data.Records;
using TonexAdvisor.Core.Localization;

namespace TonexAdvisor.Core.Advice;

/// <summary>
/// Ranks a library against a song, an artist or a style — with a sentence for every point.
/// </summary>
/// <remarks>
/// <para>Everything here is local and synchronous: a 2 310 preset library scores in a few
/// milliseconds, so the user never waits and never sends their whole library to a model. The
/// shortlist this produces is exactly what Phase 4 will hand to the AI as context.</para>
///
/// <para>Points are summed per signal and then normalised against what <i>this</i> query could
/// possibly reach, so « artiste exact » reads as a comparable percentage whether the user typed
/// one field or three. Signals that fired are kept verbatim as <see cref="ScoreReason"/>: an
/// advisor that cannot explain itself is not worth trusting.</para>
/// </remarks>
public sealed class LibraryAdvisor
{
    // Preset signals.
    private const double ArtistPoints = 45;
    private const double ArtistOwnerPoints = 32;
    private const double ArtistPartialPoints = 20;
    private const double ArtistHalfPoints = 10;

    private const double SongPoints = 40;
    private const double SongFieldPoints = 26;
    private const double SongContextPoints = 16;
    private const double SongHalfPoints = 8;

    private const double SaturationPoints = 16;
    private const double GenrePoints = 10;
    private const double TextPoints = 6;
    private const double TextPerToken = 3;

    // Combination signals (a tone model has no knobs, its metadata is all we have).
    private const double ComboArtistPoints = 40;
    private const double ComboArtistHalfPoints = 20;
    private const double ComboSongPoints = 30;
    private const double ComboSongHalfPoints = 15;
    private const double ComboTextPoints = 8;
    private const double ComboTextPerToken = 4;

    // Fixed bonuses, attainable by every query.
    private const double FavoritePoints = 3;
    private const double SettingsPoints = 2;
    private const double UsedPoints = 3;

    /// <summary>
    /// Points a high-gain style earns for putting a boost pedal in front of the amp — the one
    /// piece of guitar craft this advisor asserts beyond simple matching. It stays in the
    /// denominator: it is a recommendation every competing combination could also deserve.
    /// </summary>
    private const double FrontBoostPoints = 4;

    /// <summary>
    /// Points for the cabinet recommended next to the block. The stomp and the amplifier of a
    /// capture come as one indivisible pair — TONEX only lets the cabinet change — so this is the
    /// single element the advisor is free to pick anywhere in the library.
    /// </summary>
    private const double ComboCabPoints = 6;

    private readonly LibraryIndex _index;

    public LibraryAdvisor(LibraryIndex index)
        => _index = index ?? throw new ArgumentNullException(nameof(index));

    public LibraryIndex Index => _index;

    /// <summary>Top presets for this query, best first. Empty for a blank query.</summary>
    public IReadOnlyList<ScoredPreset> RankPresets(AdviceQuery query, int limit = 3)
    {
        ArgumentNullException.ThrowIfNull(query);

        var plan = BuildPlan(query, isCombination: false);
        if (plan is null || limit <= 0)
            return Array.Empty<ScoredPreset>();

        var results = new List<ScoredPreset>();

        foreach (var preset in _index.Presets)
        {
            var reasons = new List<ScoreReason>(5);
            var content = ScorePresetContent(preset, plan, reasons);
            if (content <= 0)
                continue;

            var total = content + ScorePresetBonus(preset, reasons);
            results.Add(new ScoredPreset(
                preset,
                Math.Round(100 * total / plan.MaxPoints, 1),
                Order(reasons)));
        }

        return results
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Preset.Favorite)
            .ThenByDescending(item => item.Preset.HasKnobSettings)
            .ThenBy(item => item.Preset.Name, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Top captured blocks — stomp + amplifier as the capture records them, never mixed — each
    /// with the cabinet that suits it best. Two captures sharing a block collapse into one entry.
    /// </summary>
    public IReadOnlyList<ScoredCombination> RankCombinations(AdviceQuery query, int limit = 3)
    {
        ArgumentNullException.ThrowIfNull(query);

        var plan = BuildPlan(query, isCombination: true);
        if (plan is null || limit <= 0)
            return Array.Empty<ScoredCombination>();

        var bestByKey = new Dictionary<string, ScoredCombination>(StringComparer.Ordinal);

        foreach (var model in _index.ToneModels)
        {
            // A combination starts with an amplifier; stomp-only and cab-only captures are not one.
            if (model.AmpName.Length == 0)
                continue;

            var reasons = new List<ScoreReason>(5);
            var content = ScoreModelContent(model, plan, reasons);
            if (content <= 0)
                continue;

            var used = _index.PresetsFor(model.Key).Count;
            double bonus = (used > 0 ? UsedPoints : 0) + (model.Favorite ? FavoritePoints : 0);
            if (used > 0)
                reasons.Add(new ScoreReason(CoreTexts.Plural(used, "Raison.Uses"), UsedPoints));
            if (model.Favorite)
                reasons.Add(new ScoreReason(CoreTexts.Get("Raison.Favori"), FavoritePoints));

            // The block itself is untouchable: its stomp and its amplifier come from the same
            // capture and may never be mixed with another capture. The cabinet is the one free
            // element, so it is chosen here instead of being inherited.
            var cabinet = ChooseCab(model, plan);
            if (cabinet.Reason is not null)
            {
                content += cabinet.Reason.Points;
                reasons.Add(cabinet.Reason);
            }

            var score = Math.Round(100 * (content + bonus) / plan.MaxPoints, 1);

            // Grouped on the block alone: two captures sharing stomp + amplifier but not their
            // cabinet are one and the same recommendation, since the cabinet may be swapped.
            var key = string.Join('|',
                Tokenizer.Normalize(model.AmpName),
                Tokenizer.Normalize(model.StompName));

            if (bestByKey.TryGetValue(key, out var current) && current.Score >= score)
                continue;

            bestByKey[key] = new ScoredCombination(
                model.AmpName,
                model.StompName,
                cabinet.Cab,
                cabinet.Note,
                score,
                Order(reasons),
                model,
                used);
        }

        return bestByKey.Values
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.PresetCount)
            .ThenBy(item => item.Amp, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    // ── Cabinet choice ──────────────────────────────────────────────────────

    private sealed record Cabinet(string Name, double Saturation, int Uses);

    private List<Cabinet>? _cabinets;

    /// <summary>
    /// The cab list of the library, computed once: it depends on the index only, and grouping
    /// three thousand captures again for every candidate would turn a ranking quadratic.
    /// </summary>
    private List<Cabinet> Cabinets
        => _cabinets ??= _index.ToneModels
            .Where(model => model.CabName.Length > 0)
            .GroupBy(model => model.CabName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new Cabinet(
                group.Key,
                group.Average(model => StyleVocabulary.CategorySaturation(model.Category)),
                group.Count()))
            .ToList();

    /// <param name="Cab">Chosen cabinet, empty when the library holds none.</param>
    /// <param name="Note">French line stating that the cabinet is swappable.</param>
    /// <param name="Reason">Points earned against the style, when a style was given.</param>
    private sealed record CabChoice(string Cab, string Note, ScoreReason? Reason);

    /// <summary>
    /// Picks the cabinet to put behind a captured block. The stomp and the amplifier are frozen
    /// together by the capture, but TONEX accepts any cabinet behind them, so the whole cab list
    /// of the library is eligible: the one whose average saturation fits the style wins, with ties
    /// going to the cabinet the capture already ships with.
    /// </summary>
    private CabChoice ChooseCab(ToneModelRecord block, Plan plan)
    {
        var cabinets = Cabinets;

        if (cabinets.Count == 0)
            return new CabChoice("", CoreTexts.Get("Raison.NoCab"), null);

        if (plan.Profile is not { } profile)
        {
            // No style to satisfy: keep what the capture already provides.
            var own = cabinets.FirstOrDefault(cabinet => IsSame(cabinet.Name, block.CabName));
            if (own is not null)
                return new CabChoice(own.Name, CoreTexts.Get("Raison.CabFournie"), null);

            var widest = cabinets.OrderByDescending(cabinet => cabinet.Uses).First();
            return new CabChoice(widest.Name, CoreTexts.Get("Raison.CabLibreSansAucun"), null);
        }

        Cabinet? best = null;
        double bestRank = double.NegativeInfinity;
        double bestFit = 0;
        var bestIsOwn = false;

        foreach (var cabinet in cabinets)
        {
            var fit = 1 - Math.Min(1, Math.Abs(cabinet.Saturation - profile.TargetSaturation));
            var isOwn = IsSame(cabinet.Name, block.CabName);

            // Ties go to the cabinet the capture already ships with.
            var rank = fit + (isOwn ? 0.05 : 0);
            if (rank <= bestRank)
                continue;

            bestRank = rank;
            best = cabinet;
            bestFit = fit;
            bestIsOwn = isOwn;
        }

        if (best is null)
            return new CabChoice("", CoreTexts.Get("Raison.NoCab"), null);

        var suffix = bestIsOwn ? CoreTexts.Get("Raison.BaffleCapture") : "";

        var points = Math.Round(ComboCabPoints * bestFit, 1);
        var reason = points >= 1
            ? new ScoreReason(
                CoreTexts.Format("Raison.BaffleSaturation", null, best.Name, profile.Label, suffix),
                points)
            : null;

        var note = bestIsOwn
            ? CoreTexts.Get("Raison.CabFournie")
            : CoreTexts.Get("Raison.CabLibre");

        return new CabChoice(best.Name, note, reason);
    }

    private static bool IsSame(string left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    // ── Plan ────────────────────────────────────────────────────────────────

    private sealed record Plan(
        string ArtistText,
        IReadOnlyList<string> ArtistTokens,
        string SongText,
        IReadOnlyList<string> SongTokens,
        StyleProfile? Profile,
        IReadOnlyList<string> StyleTokens,
        double MaxPoints)
    {
        public bool HasArtist => ArtistTokens.Count > 0;
        public bool HasSong => SongTokens.Count > 0;
        public bool HasStyle => StyleTokens.Count > 0;
    };

    /// <summary>
    /// Precomputes the query, or returns null when it cannot score anything.
    /// <paramref name="isCombination"/> switches the denominator to what a tone model can reach:
    /// a captured rig has no genre and no knobs, so it cannot win the preset-only points.
    /// </summary>
    private static Plan? BuildPlan(AdviceQuery query, bool isCombination)
    {
        if (query.IsBlank)
            return null;

        var artistTokens = Tokenizer.Tokenize(query.Artist);
        var songTokens = Tokenizer.Tokenize(query.Song);
        var styleTokens = Tokenizer.Tokenize(query.Style);
        var profile = StyleVocabulary.Detect(query.Style);

        var max = FavoritePoints + SettingsPoints;

        if (artistTokens.Count > 0)
            max += isCombination ? ComboArtistPoints : ArtistPoints;

        if (songTokens.Count > 0)
            max += isCombination ? ComboSongPoints : SongPoints;

        if (styleTokens.Count > 0)
        {
            max += (profile is null ? 0 : SaturationPoints)
                   + (isCombination ? ComboTextPoints : GenrePoints + TextPoints);

            if (isCombination && profile is not null)
                max += FrontBoostPoints + ComboCabPoints;
        }

        if (max <= FavoritePoints + SettingsPoints)
            return null;

        return new Plan(
            Tokenizer.Normalize(query.Artist),
            artistTokens,
            Tokenizer.Normalize(query.Song),
            songTokens,
            profile,
            styleTokens,
            max);
    }

    // ── Preset scoring ──────────────────────────────────────────────────────

    private double ScorePresetContent(PresetRecord preset, Plan plan, List<ScoreReason> reasons)
    {
        double points = 0;

        if (plan.HasArtist)
            points += ScoreArtist(
                plan,
                preset.Artist,
                Join(preset.UserName, string.Join(' ', preset.Folders), preset.Name, preset.Description),
                reasons);

        if (plan.HasSong)
            points += ScoreSong(plan, preset, reasons);

        if (plan.HasStyle)
            points += ScoreStyle(plan, preset, reasons);

        return points;
    }

    private static double ScoreArtist(Plan plan, string artist, string context, List<ScoreReason> reasons)
    {
        var normalizedArtist = Tokenizer.Normalize(artist);

        if (normalizedArtist.Length > 0 && string.Equals(normalizedArtist, plan.ArtistText, StringComparison.Ordinal))
        {
            reasons.Add(new ScoreReason(CoreTexts.Format("Raison.ArtisteIdentique", null, artist), ArtistPoints));
            return ArtistPoints;
        }

        var haystack = Tokenizer.Normalize(context);

        if (plan.ArtistText.Length > 0 && haystack.Contains(plan.ArtistText, StringComparison.Ordinal))
        {
            reasons.Add(new ScoreReason(
                artist.Length > 0
                    ? CoreTexts.Format("Raison.ArtisteCiteAvec", null, artist)
                    : CoreTexts.Get("Raison.ArtisteCite"),
                ArtistOwnerPoints));
            return ArtistOwnerPoints;
        }

        return PartialMatch(
            plan.ArtistTokens, haystack, ArtistPartialPoints, ArtistHalfPoints,
            CoreTexts.Get("Raison.Label.Artiste"), reasons);
    }

    private static double ScoreSong(Plan plan, PresetRecord preset, List<ScoreReason> reasons)
    {
        var normalizedSong = Tokenizer.Normalize(preset.Song);

        if (normalizedSong.Length > 0 && string.Equals(normalizedSong, plan.SongText, StringComparison.Ordinal))
        {
            reasons.Add(new ScoreReason(CoreTexts.Format("Raison.ChansonIdentique", null, preset.Song), SongPoints));
            return SongPoints;
        }

        if (normalizedSong.Length > 0 && ContainsAll(normalizedSong, plan.SongTokens))
        {
            reasons.Add(new ScoreReason(CoreTexts.Format("Raison.ChansonChamp", null, preset.Song), SongFieldPoints));
            return SongFieldPoints;
        }

        var context = Join(preset.Name, preset.Album, preset.Description, string.Join(' ', preset.Folders));
        if (ContainsAll(context, plan.SongTokens))
        {
            reasons.Add(new ScoreReason(CoreTexts.Format("Raison.ChansonContexte", null, plan.SongText), SongContextPoints));
            return SongContextPoints;
        }

        return PartialMatch(
            plan.SongTokens, context, SongHalfPoints, SongHalfPoints,
            CoreTexts.Get("Raison.Label.Chanson"), reasons);
    }

    private double ScoreStyle(Plan plan, PresetRecord preset, List<ScoreReason> reasons)
    {
        double points = 0;

        if (plan.Profile is { } profile)
        {
            var saturation = StyleVocabulary.CategorySaturation(preset.Category);
            var fit = 1 - Math.Min(1, Math.Abs(saturation - profile.TargetSaturation));
            var fitPoints = Math.Round(SaturationPoints * fit, 1);

            if (fitPoints >= 1)
            {
                points += fitPoints;
                reasons.Add(new ScoreReason(
                    CoreTexts.Format("Raison.CategorieSaturation", null, preset.Category, profile.Label),
                    fitPoints));
            }
        }

        var genre = Tokenizer.Normalize(preset.Genre);
        if (genre.Length > 0 && plan.StyleTokens.Any(token => genre.Contains(token, StringComparison.Ordinal)))
        {
            points += GenrePoints;
            reasons.Add(new ScoreReason(CoreTexts.Format("Raison.Genre", null, preset.Genre), GenrePoints));
        }

        var keywords = Tokenizer.Normalize(string.Join(' ',
            preset.Name, preset.Description, string.Join(' ', preset.Folders),
            string.Join(' ', preset.ToneModelKeys.Select(key => ModelText(_index.Model(key))))));

        var matched = plan.StyleTokens.Where(token => keywords.Contains(token, StringComparison.Ordinal)).ToList();
        if (matched.Count > 0)
        {
            var textPoints = Math.Min(TextPoints, matched.Count * TextPerToken);
            points += textPoints;
            reasons.Add(new ScoreReason(
                CoreTexts.Format("Raison.MotsCles", null,
                    string.Join(CoreTexts.Get("Raison.SepCitation"), matched)),
                textPoints));
        }

        return points;
    }

    // ── Tone model scoring ──────────────────────────────────────────────────

    private double ScoreModelContent(ToneModelRecord model, Plan plan, List<ScoreReason> reasons)
    {
        double points = 0;

        if (plan.HasArtist)
        {
            var context = Join(model.Name, model.Keywords, model.Description, model.ModelComment,
                model.Author, string.Join(' ', model.Folders), model.AmpName);

            points += PartialMatch(plan.ArtistTokens, context, ComboArtistPoints, ComboArtistHalfPoints,
                "Artiste", reasons);
        }

        if (plan.HasSong)
        {
            var context = Join(model.Name, model.Keywords, model.Description, model.ModelComment);
            points += PartialMatch(plan.SongTokens, context, ComboSongPoints, ComboSongHalfPoints,
                "Chanson", reasons);
        }

        if (plan.HasStyle)
        {
            if (plan.Profile is { } profile)
            {
                var saturation = StyleVocabulary.CategorySaturation(model.Category);
                var fit = 1 - Math.Min(1, Math.Abs(saturation - profile.TargetSaturation));
                var fitPoints = Math.Round(SaturationPoints * fit, 1);

                if (fitPoints >= 1)
                {
                    points += fitPoints;
                    reasons.Add(new ScoreReason(
                        CoreTexts.Format("Raison.CategorieSaturation", null, model.Category, profile.Label),
                        fitPoints));
                }
            }

            var text = Tokenizer.Normalize(
                Join(model.Name, model.Keywords, model.Description, model.AmpName, model.CabName));
            var matched = plan.StyleTokens.Where(token => text.Contains(token, StringComparison.Ordinal)).ToList();
            if (matched.Count > 0)
            {
                var textPoints = Math.Min(ComboTextPoints, matched.Count * ComboTextPerToken);
                points += textPoints;
                reasons.Add(new ScoreReason(
                    CoreTexts.Format("Raison.MotsCles", null,
                        string.Join(CoreTexts.Get("Raison.SepCitation"), matched)),
                    textPoints));
            }

            if (plan.Profile is { TargetSaturation: >= 0.9 } && StyleVocabulary.IsFrontBoost(model.StompName))
            {
                points += FrontBoostPoints;
                reasons.Add(new ScoreReason(
                    CoreTexts.Format("Raison.Boost", null, model.StompName),
                    FrontBoostPoints));
            }
        }

        return points;
    }

    // ── Shared helpers ──────────────────────────────────────────────────────

    private static double ScorePresetBonus(PresetRecord preset, List<ScoreReason> reasons)
    {
        double points = 0;

        if (preset.Favorite)
        {
            points += FavoritePoints;
            reasons.Add(new ScoreReason(CoreTexts.Get("Raison.Favori"), FavoritePoints));
        }

        if (preset.HasKnobSettings)
        {
            points += SettingsPoints;
            reasons.Add(new ScoreReason(CoreTexts.Get("Raison.Reglages"), SettingsPoints));
        }

        return points;
    }

    private static double PartialMatch(
        IReadOnlyList<string> tokens,
        string context,
        double allPoints,
        double halfPoints,
        string label,
        List<ScoreReason> reasons)
    {
        if (tokens.Count == 0)
            return 0;

        // Tokens arrive normalised, contexts do not: fold the text before comparing, otherwise
        // « Dumble » never matches « dumble » and every accent silently kills a hit.
        var haystack = Tokenizer.Normalize(context);
        var hits = tokens.Count(token => haystack.Contains(token, StringComparison.Ordinal));
        if (hits == 0)
            return 0;

        var ratio = hits / (double)tokens.Count;

        if (ratio >= 0.999)
        {
            reasons.Add(new ScoreReason(CoreTexts.Format("Raison.Partial", null, label), allPoints));
            return allPoints;
        }

        // A single matching word out of two is a coincidence far more often than a match
        // ("zzz personne" happily lands inside "fuzzzzy"): the weaker tier needs two of them.
        if (ratio >= 0.5 && hits >= 2)
        {
            reasons.Add(new ScoreReason(
                CoreTexts.Format("Raison.PartialPartiel", null, label, hits, tokens.Count),
                halfPoints));
            return halfPoints;
        }

        return 0;
    }

    private static bool ContainsAll(string context, IReadOnlyList<string> tokens)
    {
        if (tokens.Count == 0)
            return false;

        var haystack = Tokenizer.Normalize(context);
        return tokens.All(token => haystack.Contains(token, StringComparison.Ordinal));
    }

    private static string ModelText(ToneModelRecord? model)
        => model is null ? "" : Join(model.Name, model.Keywords, model.Description, model.AmpName, model.CabName);

    private static string Join(params string[] parts)
        => string.Join(' ', parts.Where(part => !string.IsNullOrWhiteSpace(part)));

    private static IReadOnlyList<ScoreReason> Order(List<ScoreReason> reasons)
    {
        reasons.Sort((left, right) => right.Points.CompareTo(left.Points));
        return reasons;
    }
}
