namespace TonexAdvisor.App.ViewModels;

/// <summary>A group of parameters rendered as one block of the signal path.</summary>
public sealed class SettingSectionViewModel
{
    public SettingSectionViewModel(
        string key,
        string title,
        bool? blockEnabled,
        string? positionLabel,
        IReadOnlyList<KnobViewModel> knobs)
    {
        Key = key;
        Title = title;
        BlockEnabled = blockEnabled;
        PositionLabel = positionLabel;
        Knobs = knobs;
    }

    public string Key { get; }

    public string Title { get; }

    /// <summary>Block switch: false when bypassed, true when active, null when the block has none.</summary>
    public bool? BlockEnabled { get; }

    /// <summary>Pre/post placement label, when the block exposes one.</summary>
    public string? PositionLabel { get; }

    public IReadOnlyList<KnobViewModel> Knobs { get; }

    public bool HasSwitch => BlockEnabled is not null;

    public string SwitchLabel => BlockEnabled switch
    {
        true => "ACTIF",
        false => "BYPASS",
        _ => "",
    };

    /// <summary>Bypassed blocks are drawn dimmed but never hidden.</summary>
    public bool IsDimmed => BlockEnabled == false;
}
