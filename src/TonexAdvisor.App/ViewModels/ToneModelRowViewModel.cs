using TonexAdvisor.App.Localization;
using TonexAdvisor.Core.Data.Records;

namespace TonexAdvisor.App.ViewModels;

/// <summary>One row of the tone models grid.</summary>
public sealed class ToneModelRowViewModel
{
    public ToneModelRowViewModel(ToneModelRecord model, int linkedPresetCount)
    {
        Record = model;
        Name = model.Name.Length > 0 ? model.Name : model.Key;
        Amp = model.AmpName;
        Stomp = model.StompName;
        Cab = model.CabName;
        Mics = model.Mic1.Length > 0 && model.Mic2.Length > 0
            ? model.Mic1 + " + " + model.Mic2
            : model.Mic1.Length > 0 ? model.Mic1 : model.Mic2;
        Category = model.Category;
        Kind = KindLabel(model.Kind);
        Folders = model.Folders.Count > 0 ? model.Folders[0] : "";
        Author = model.Author.Length > 0 ? model.Author : model.Copyright;
        Channel = model.AmpChannel;
        Skin = model.Skin;
        Collection = model.Collection ?? "";
        Favorite = model.Favorite;
        PresetCount = linkedPresetCount;
        HasCab = model.CabName.Length > 0;
    }

    public ToneModelRecord Record { get; }

    public string Name { get; }

    public string Amp { get; }

    public string Stomp { get; }

    public string Cab { get; }

    public string Mics { get; }

    public string Category { get; }

    public string Kind { get; }

    public string Folders { get; }

    public string Author { get; }

    public string Channel { get; }

    public string Skin { get; }

    public string Collection { get; }

    public bool Favorite { get; }

    public bool HasCab { get; }

    public int PresetCount { get; }

    private static string KindLabel(ToneModelKind kind) => kind switch
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
