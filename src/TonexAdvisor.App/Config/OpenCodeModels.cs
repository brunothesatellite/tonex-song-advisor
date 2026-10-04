namespace TonexAdvisor.App.Config;

/// <summary>A model offered by OpenCode Go, free or paid.</summary>
/// <param name="Id">Identifier sent to the API.</param>
/// <param name="IsFree">True for the « Free » tier.</param>
public sealed record OpenCodeModel(string Id, bool IsFree)
{
    public string Label => IsFree ? $"{Id}  ·  gratuit" : $"{Id}  ·  payant";

    /// <summary>« gratuit » ou « payant », ce que la liste colore.</summary>
    public string Tag => IsFree ? "gratuit" : "payant";

    public bool IsPaid => !IsFree;

    /// <summary>What the drop-down shows.</summary>
    public override string ToString() => Label;
}

/// <summary>
/// The models of the OpenCode Go endpoint.
/// </summary>
/// <remarks>
/// The catalogue used to be a fixed list of the two known free models; it now comes from the API
/// itself, so paid models are offered too and a withdrawn model does not leave a dead choice in
/// the list. The free tier is announced in the identifier — OpenCode names them
/// <c>longcat-2.5-preview-free</c>, <c>space-bunny-free</c> — which is what
/// <see cref="IsFree"/> reads.
/// </remarks>
public static class OpenCodeModels
{
    /// <summary>Base URL of the OpenCode Go offer.</summary>
    public const string EndPoint = "https://opencode.ai/zen/go/v1";

    /// <summary>The model to prefer when nothing else is known: LongCat, then any free one.</summary>
    public const string PreferredId = "longcat-2.5-preview-free";

    /// <summary>Known before the first call to the API, so the list is never empty.</summary>
    public static IReadOnlyList<OpenCodeModel> Defaults { get; } =
    [
        new OpenCodeModel(PreferredId, true),
        new OpenCodeModel("space-bunny-free", true),
    ];

    /// <summary>The free tier is written in the identifier.</summary>
    public static bool IsFree(string id)
        => id.Contains("free", StringComparison.OrdinalIgnoreCase);

    public static OpenCodeModel FromId(string id)
        => new(id, IsFree(id));

    /// <summary>
    /// What to select when the chosen model is gone: LongCat if it is still there, else the first
    /// free model, else anything the list holds.
    /// </summary>
    public static OpenCodeModel Fallback(IEnumerable<OpenCodeModel> models)
    {
        var list = models.ToList();

        return list.FirstOrDefault(model => model.Id == PreferredId)
               ?? list.FirstOrDefault(model => model.IsFree)
               ?? list.FirstOrDefault()
               ?? Defaults[0];
    }

    /// <summary>
    /// Normalises an identifier found in the configuration: the canonical spelling when the model
    /// is known, the identifier as written otherwise — a paid model is not rewritten away.
    /// </summary>
    public static OpenCodeModel Resolve(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Fallback(Defaults);

        var known = Defaults.FirstOrDefault(model =>
            string.Equals(model.Id, id, StringComparison.OrdinalIgnoreCase));

        return known ?? FromId(id);
    }
}
