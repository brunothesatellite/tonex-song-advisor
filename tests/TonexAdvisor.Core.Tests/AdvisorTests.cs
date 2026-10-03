using System.Diagnostics;
using TonexAdvisor.Core.Advice;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// The advice must stay explainable and bounded: a score above 100 or a recommendation without a
/// reason would be worse than no advice at all.
/// </summary>
public class AdvisorTests
{
    private static LibraryAdvisor Advisor => new(SampleLibraries.Gen1);

    [LibraryFact]
    public void BlankQuery_RanksNothing()
    {
        Assert.Empty(Advisor.RankPresets(new AdviceQuery()));
        Assert.Empty(Advisor.RankCombinations(new AdviceQuery()));
    }

    [LibraryFact]
    public void ScoresAreBoundedAndEveryResultExplainsItself()
    {
        var results = Advisor.RankPresets(new AdviceQuery { Style = "metal" }, 5);

        Assert.NotEmpty(results);
        Assert.All(results, result =>
        {
            Assert.InRange(result.Score, 0.01, 100);
            Assert.NotEmpty(result.Reasons);
            Assert.All(result.Reasons, reason =>
            {
                Assert.False(string.IsNullOrWhiteSpace(reason.Text));
                Assert.True(reason.Points > 0);
            });
        });

        // Sorted, best first.
        for (var i = 1; i < results.Count; i++)
            Assert.True(results[i - 1].Score >= results[i].Score);
    }

    [LibraryFact]
    public void MetalQuery_RecommendsHighGainPresets()
    {
        var results = Advisor.RankPresets(new AdviceQuery { Style = "metal" }, 3);

        Assert.Equal(3, results.Count);
        Assert.All(results, result =>
            Assert.True(StyleVocabulary.CategorySaturation(result.Preset.Category) >= 0.8,
                $"{result.Preset.Name} is {result.Preset.Category}, not saturated enough for « metal »"));
    }

    [LibraryFact]
    public void BluesQuery_StaysInTheDriveFamily()
    {
        var results = Advisor.RankPresets(new AdviceQuery { Style = "blues" }, 3);

        Assert.NotEmpty(results);
        Assert.All(results, result =>
        {
            var saturation = StyleVocabulary.CategorySaturation(result.Preset.Category);
            Assert.InRange(saturation, 0.4, 0.75);
        });
    }

    [LibraryFact]
    public void CleanQuery_NeverRecommendsASaturatedPreset()
    {
        var results = Advisor.RankPresets(new AdviceQuery { Style = "clean funk" }, 3);

        Assert.NotEmpty(results);
        Assert.All(results, result =>
            Assert.True(StyleVocabulary.CategorySaturation(result.Preset.Category) <= 0.3,
                $"{result.Preset.Name} is {result.Preset.Category}"));
    }

    [LibraryFact]
    public void AnArtistThatIsInTheLibraryWins()
    {
        // The busiest artist of the sample library, so there are enough presets to fill the podium.
        var artist = SampleLibraries.Gen1.Presets
            .Where(preset => preset.Artist.Length > 0)
            .GroupBy(preset => preset.Artist)
            .Where(group => group.Count() >= 3)
            .OrderByDescending(group => group.Count())
            .First()
            .Key;

        var results = Advisor.RankPresets(new AdviceQuery { Artist = artist }, 3);

        Assert.Equal(3, results.Count);
        Assert.All(results, result => Assert.Equal(artist, result.Preset.Artist));
        Assert.True(results[0].Score >= 89, $"only {results[0].Score} for an exact artist match");
        Assert.Contains(results[0].Reasons, reason => reason.Text.StartsWith("Artiste identique", StringComparison.Ordinal));
    }

    [LibraryFact]
    public void ArtistAndSongTogetherPointAtOnePreset()
    {
        var preset = SampleLibraries.Gen1.Presets
            .First(candidate => candidate.Artist.Length > 0 && candidate.Song.Length > 0);

        var results = Advisor.RankPresets(
            new AdviceQuery { Artist = preset.Artist, Song = preset.Song }, 1);

        Assert.Single(results);
        Assert.Equal(preset.Artist, results[0].Preset.Artist);
        Assert.Equal(preset.Song, results[0].Preset.Song);
        Assert.True(results[0].Score >= 85);
    }

    [LibraryFact]
    public void AnUnknownArtistReturnsNothingInsteadOfAGuess()
    {
        // Regression: a single matching word ("zzz personne" landing inside "fuzzzzy") used to
        // be enough to fill the podium with unrelated presets.
        Assert.Empty(Advisor.RankPresets(new AdviceQuery { Artist = "zzz personne inconnue" }, 3));
    }

    [LibraryFact]
    public void MetalCombination_KeepsTheCapturedBlockIntact_AndChoosesItsCabinet()
    {
        var combinations = Advisor.RankCombinations(new AdviceQuery { Style = "metal" }, 3);

        Assert.NotEmpty(combinations);

        var libraryCabs = SampleLibraries.Gen1.ToneModels
            .Select(model => model.CabName)
            .Where(name => name.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.All(combinations, combination =>
        {
            Assert.False(string.IsNullOrWhiteSpace(combination.Amp));
            Assert.InRange(combination.Score, 0.01, 100);
            Assert.NotEmpty(combination.Reasons);

            // Atomicity, the rule this whole card exists for: the stomp and the amplifier always
            // come from the very same capture and are never borrowed from two of them.
            Assert.Equal(combination.Example.AmpName, combination.Amp);
            Assert.Equal(combination.Example.StompName, combination.Stomp);

            // The cabinet is free — but it still has to exist in the library.
            Assert.False(string.IsNullOrWhiteSpace(combination.Cab));
            Assert.Contains(combination.Cab, libraryCabs);
            Assert.Contains("baffle", combination.CabNote, StringComparison.OrdinalIgnoreCase);
        });

        // The cabinet choice is explained, not silent.
        Assert.Contains(combinations, combination => combination.Reasons
            .Any(reason => reason.Text.StartsWith("Baffle", StringComparison.Ordinal)));

        // The one piece of guitar craft the advisor is allowed to assert: a boost pedal in
        // front of a high-gain amplifier.
        Assert.Contains(combinations, combination => StyleVocabulary.IsFrontBoost(combination.Stomp));
    }

    [LibraryFact]
    public void WithoutARecognisedStyleTheCabinetStaysThatOfTheCapture()
    {
        // « dumble » names an amplifier, not a style: the vocabulary returns no profile, so the
        // advisor must not pretend to choose a cabinet and simply keeps the capture's own.
        var combinations = Advisor.RankCombinations(new AdviceQuery { Style = "dumble" }, 3);

        Assert.NotEmpty(combinations);
        Assert.All(combinations, combination =>
        {
            Assert.Contains("Dumble", combination.Amp, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(combination.Reasons,
                reason => reason.Text.StartsWith("Baffle", StringComparison.Ordinal));
            Assert.False(string.IsNullOrWhiteSpace(combination.CabNote));
        });
    }

    [LibraryFact]
    public void Generation2LibraryAdvisesFromMetadataAlone()
    {
        var advisor = new LibraryAdvisor(SampleLibraries.Gen2);
        var results = advisor.RankPresets(new AdviceQuery { Style = "clean" }, 3);

        Assert.NotEmpty(results);
        Assert.All(results, result => Assert.False(result.Preset.HasKnobSettings));
        Assert.All(results, result => Assert.NotEmpty(result.Reasons));
    }

    [LibraryFact]
    public void RankingIsFastEnoughToRunOnEveryKeystroke()
    {
        var advisor = Advisor;
        var query = new AdviceQuery { Style = "metal" };

        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < 5; i++)
        {
            advisor.RankPresets(query, 3);
            advisor.RankCombinations(query, 3);
        }

        stopwatch.Stop();
        Assert.True(stopwatch.ElapsedMilliseconds < 3000,
            $"10 rankings took {stopwatch.ElapsedMilliseconds} ms");
    }

    [LibraryFact]
    public void StyleVocabulary_FoldsWordingOntoTheSaturationLadder()
    {
        Assert.Equal("metal", StyleVocabulary.Detect("Metal moderne")!.Label);
        Assert.Equal(1.0, StyleVocabulary.Detect("djent")!.TargetSaturation);
        Assert.Equal(0.15, StyleVocabulary.Detect("clean funk")!.TargetSaturation);
        Assert.Null(StyleVocabulary.Detect("un truc qui n'existe pas"));

        Assert.Equal(1.0, StyleVocabulary.CategorySaturation("HI-GAIN"));
        Assert.Equal(0.15, StyleVocabulary.CategorySaturation("CLEAN"));
        Assert.Equal(StyleVocabulary.NeutralSaturation, StyleVocabulary.CategorySaturation("GEÇONNUE"));
        Assert.Equal(StyleVocabulary.NeutralSaturation, StyleVocabulary.CategorySaturation(""));

        Assert.True(StyleVocabulary.IsFrontBoost("Maxon OD808"));
        Assert.True(StyleVocabulary.IsFrontBoost("Ibanez Tube Screamer"));
        Assert.False(StyleVocabulary.IsFrontBoost("ProCo Rat"));
        Assert.False(StyleVocabulary.IsFrontBoost(""));
    }
}
