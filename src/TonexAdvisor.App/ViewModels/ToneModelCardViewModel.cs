using TonexAdvisor.App.Localization;
using TonexAdvisor.Core.Data;
using TonexAdvisor.Core.Data.Records;

namespace TonexAdvisor.App.ViewModels;

/// <summary>Compact card describing a tone model inside a preset detail panel.</summary>
public sealed class ToneModelCardViewModel
{
    public ToneModelCardViewModel(ToneModelRecord model)
    {
        Key = model.Key;
        Name = model.Name.Length > 0 ? model.Name : model.Key;
        AmpName = model.AmpName;
        StompName = model.StompName;
        CabName = model.CabName;
        Micros = Join(model.Mic1, model.Mic2);
        KindLabel = Kind(model.Kind);
        Category = model.Category;
        Author = model.Author;
        Folders = model.Folders.Count > 0 ? string.Join(" · ", model.Folders) : "";
        Skin = model.Skin;
        Channel = model.AmpChannel;
        HasStomp = model.StompName.Length > 0;
        HasCab = model.CabName.Length > 0;
    }

    public string Key { get; }

    public string Name { get; }

    public string AmpName { get; }

    public string StompName { get; }

    public string CabName { get; }

    public string Micros { get; }

    public string KindLabel { get; }

    public string Category { get; }

    public string Author { get; }

    public string Folders { get; }

    public string Skin { get; }

    public string Channel { get; }

    public bool HasStomp { get; }

    public bool HasCab { get; }

    /// <summary>
    /// La chaine capturee, dans l'ordre du signal : stomp puis ampli. Un « rig complet » n'a
    /// pas toujours d'ampli nomme : le stomp doit alors rester visible, sinon la capture reste
    /// muette sur ce qu'elle contient.
    /// </summary>
    public string AmpLine
    {
        get
        {
            var parts = new List<string>();

            if (HasStomp)
                parts.Add(StompName);

            if (AmpName.Length > 0)
            {
                var amp = AmpName;
                if (Channel.Length > 0)
                    amp += Localizer.Instance.Get("Libelle.Canal.Suffixe", Channel);
                parts.Add(amp);
            }

            return parts.Count > 0 ? string.Join(" -> ", parts) : Name;
        }
    }

    private static string Join(string first, string second)
        => second.Length > 0 ? first + " + " + second : first;

    private static string Kind(ToneModelKind kind) => kind switch
    {
        ToneModelKind.Stomp => Localizer.Instance["Libelle.Type.Stomp"],
        ToneModelKind.StompAndAmp => Localizer.Instance["Libelle.Type.StompAmp"],
        ToneModelKind.Amp => Localizer.Instance["Libelle.Type.Amp"],
        ToneModelKind.AmpAndCab => Localizer.Instance["Libelle.Type.AmpCab"],
        ToneModelKind.ComplexRig => Localizer.Instance["Libelle.Type.RigComplet"],
        ToneModelKind.CustomIR => Localizer.Instance["Libelle.Type.IRCab"],
        _ => kind.ToString(),
    };
}
