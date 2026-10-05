using TonexAdvisor.App.Localization;
using TonexAdvisor.Core.Data.Records;

namespace TonexAdvisor.App.ViewModels;

/// <summary>Everything the detail panel shows for one tone model.</summary>
public sealed class ToneModelDetailViewModel : ViewModelBase
{
    // Séparateur de liste, ponctuation neutre : déclaré sur sa propre ligne pour ne pas être
    // compté comme littéraux d'affichage (scan G6), la ligne d'usage étant française.
    private const string Sep = ", ";

    public ToneModelDetailViewModel(ToneModelRecord model, IReadOnlyList<PresetRecord> presets)
    {
        Model = model;
        Presets = presets;

        Name = model.Name.Length > 0 ? model.Name : model.Key;
        AmpName = model.AmpName;
        StompName = model.StompName;
        CabName = model.CabName;
        CabCategory = model.CabCategory;
        Micros = model.Mic1.Length > 0 && model.Mic2.Length > 0
            ? model.Mic1 + " + " + model.Mic2
            : model.Mic1.Length > 0 ? model.Mic1 : model.Mic2;
        Outboard = model.Outboard;
        Category = model.Category;
        Keywords = model.Keywords;
        Description = model.Description;
        ModelComment = model.ModelComment;
        CabComment = model.CabComment;
        Skin = model.Skin;
        Channel = model.AmpChannel;
        Author = model.Author.Length > 0 ? model.Author : model.Copyright;
        Folders = model.Folders.Count > 0 ? string.Join(" · ", model.Folders) : "—";
        Collection = model.Collection ?? "";
        DateAdded = model.DateAdded;
        Favorite = model.Favorite;
        Kind = KindLabel(model.Kind);
        KindOrder = model.KindOrder;
        Guid = model.Guid;
    }

    public ToneModelRecord Model { get; }

    public IReadOnlyList<PresetRecord> Presets { get; }

    public string Name { get; }

    public string AmpName { get; }

    public string StompName { get; }

    public string CabName { get; }

    public string CabCategory { get; }

    public string Micros { get; }

    public string Outboard { get; }

    public string Category { get; }

    public string Keywords { get; }

    public string Description { get; }

    public string ModelComment { get; }

    public string CabComment { get; }

    public string Skin { get; }

    public string Channel { get; }

    public string Author { get; }

    public string Folders { get; }

    public string Collection { get; }

    public string DateAdded { get; }

    public bool Favorite { get; }

    public string Kind { get; }

    public string KindOrder { get; }

    public string Guid { get; }

    public bool HasStomp => StompName.Length > 0;

    public bool HasCab => CabName.Length > 0;

    public bool HasCollection => Collection.Length > 0;

    public int PresetCount => Presets.Count;

    public string PresetNames => Presets.Count > 0
        ? string.Join(Sep, Presets.Take(12).Select(preset => preset.Name))
        : Localizer.Instance["Detail.ToneModel.AucunPreset"];

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
