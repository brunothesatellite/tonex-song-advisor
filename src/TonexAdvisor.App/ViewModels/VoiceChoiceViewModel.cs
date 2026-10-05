using CommunityToolkit.Mvvm.ComponentModel;
using TonexAdvisor.App.Config;

namespace TonexAdvisor.App.ViewModels;

/// <summary>
/// One OpenCode model, offered as a voice of the panel: ticked, it speaks like the free voices;
/// unticked, it says nothing.
/// </summary>
public partial class VoiceChoiceViewModel : ViewModelBase
{
    private readonly Action _changed;

    public VoiceChoiceViewModel(OpenCodeModel model, bool isChecked, Action changed)
    {
        Model = model;
        _isChecked = isChecked;
        _changed = changed;
    }

    public OpenCodeModel Model { get; }

    public string Label => Model.Label;

    /// <summary>« gratuit » ou « payant », ce que la ligne montre.</summary>
    public string Tag => Model.Tag;

    public bool IsFree => Model.IsFree;

    public bool IsPaid => Model.IsPaid;

    /// <summary>
    /// Re-notifies the localized labels after a language switch (§11.4) : a data template
    /// never revalidates on its own, the property notification is what repaints the line.
    /// </summary>
    public void NotifyLocalizedLabels()
    {
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(Tag));
    }

    [ObservableProperty]
    private bool _isChecked;

    partial void OnIsCheckedChanged(bool value) => _changed();
}
