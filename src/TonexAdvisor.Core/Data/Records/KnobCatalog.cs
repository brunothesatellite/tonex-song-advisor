using TonexAdvisor.Core.Localization;

namespace TonexAdvisor.Core.Data.Records;

/// <summary>How a parameter should be rendered.</summary>
public enum KnobShape
{
    /// <summary>Continuous value drawn as a potentiometer.</summary>
    Knob,

    /// <summary>0/1 value drawn as an on/off switch.</summary>
    Switch,

    /// <summary>Integer index drawn as a drop-down of named options.</summary>
    Selector,
}

/// <param name="Param">Canonical TONEX column name, including any hardware prefix.</param>
/// <param name="LabelKey">Resource key of the display label — the language lives in the lookup.</param>
/// <param name="Shape">Rendering hint.</param>
/// <param name="Unit">Optional display suffix such as <c>%</c>, <c>ms</c> or <c>Hz</c> — units are language-neutral and stay in code (§8.4, hors-champ).</param>
public sealed record KnobDefinition(string Param, string LabelKey, KnobShape Shape, string? Unit = null)
{
    /// <summary>Display label in the language of the moment (§11) — never baked at startup.</summary>
    public string Label => CoreTexts.Get(LabelKey);
}

/// <param name="Key">Stable key used for grouping and persistence.</param>
/// <param name="TitleKey">Resource key of the section title (§11).</param>
/// <param name="EnableParam">The 0/1 switch that turns the block on, when it has one.</param>
/// <param name="PositionParam">The 0/1 switch selecting pre/post placement, when it has one.</param>
/// <param name="Knobs">Parameters belonging to the section, in signal order.</param>
public sealed record SettingSection(
    string Key,
    string TitleKey,
    string? EnableParam,
    string? PositionParam,
    IReadOnlyList<KnobDefinition> Knobs)
{
    /// <summary>Section title in the language of the moment (§11).</summary>
    public string Title => CoreTexts.Get(TitleKey);
}

/// <summary>
/// Declarative description of every V1 preset parameter, grouped into the blocks a guitarist
/// recognises from the TONEX UI.
/// </summary>
/// <remarks>
/// The same catalogue is used to render the detail panel and to build the JSON digest sent to
/// the model, so an answer like <c>{"param":"EqBass","value":7.0}</c> maps directly onto a
/// control the user can see and onto a column TONEX itself stores.
/// </remarks>
public static class KnobCatalog
{
    private sealed record SectionSpec(
        string Key,
        string TitleKey,
        string? EnableParam,
        string? PositionParam,
        IReadOnlyList<KnobDefinition> Knobs);

    private static readonly SectionSpec[] BaseSections =
    [
        new("tone", "Potard.Section.ToneModel", "ModelEnable", null,
        [
            new("ModelGain", "Potard.Tone.Gain", KnobShape.Knob),
            new("ModelMix", "Potard.Tone.Mix", KnobShape.Knob, "%"),
            new("ModelVolume", "Potard.Tone.Volume", KnobShape.Knob),
            new("PwrAmpEqPresence", "Potard.Tone.Presence", KnobShape.Knob),
            new("PwrAmpEqDepth", "Potard.Tone.Depth", KnobShape.Knob),
        ]),

        new("eq", "Potard.Section.Eq", null, "EqPost",
        [
            new("EqBass", "Potard.Eq.Graves", KnobShape.Knob),
            new("EqBassFreq", "Potard.Eq.FreqGraves", KnobShape.Knob, "Hz"),
            new("EqMid", "Potard.Eq.Mids", KnobShape.Knob),
            new("EqMidFreq", "Potard.Eq.FreqMids", KnobShape.Knob, "Hz"),
            new("EqMidQ", "Potard.Eq.QMids", KnobShape.Knob),
            new("EqTreble", "Potard.Eq.Aigus", KnobShape.Knob),
            new("EqTrebleFreq", "Potard.Eq.FreqTrebles", KnobShape.Knob, "Hz"),
        ]),

        new("comp", "Potard.Section.Compresseur", "CompEnable", "CompPost",
        [
            new("CompThreshold", "Potard.Comp.Seuil", KnobShape.Knob),
            new("CompMakeUp", "Potard.Comp.MakeUp", KnobShape.Knob),
            new("CompAttack", "Potard.Comp.Attaque", KnobShape.Knob),
        ]),

        new("gate", "Potard.Section.NoiseGate", "NoiseGateEnable", "NoiseGatePost",
        [
            new("NoiseGateThreshold", "Potard.Gate.Seuil", KnobShape.Knob),
            new("NoiseGateRelease", "Potard.Gate.Relachement", KnobShape.Knob),
            new("NoiseGateDepth", "Potard.Gate.Profondeur", KnobShape.Knob),
        ]),

        new("mod", "Potard.Section.Modulation", "ModEnable", "ModPost",
        [
            new("BPM", "Potard.Mod.Tempo", KnobShape.Knob, "BPM"),
            new("ModModel", "Potard.Mod.Type", KnobShape.Selector),
            new("ModChorusSync", "Potard.Mod.ChorusSync", KnobShape.Switch),
            new("ModChorusTS", "Potard.Mod.ChorusTypeSync", KnobShape.Selector),
            new("ModChorusRate", "Potard.Mod.ChorusRate", KnobShape.Knob),
            new("ModChorusDepth", "Potard.Mod.ChorusDepth", KnobShape.Knob),
            new("ModChorusLevel", "Potard.Mod.ChorusLevel", KnobShape.Knob),
            new("ModTremoloSync", "Potard.Mod.TremoloSync", KnobShape.Switch),
            new("ModTremoloTS", "Potard.Mod.TremoloTypeSync", KnobShape.Selector),
            new("ModTremoloRate", "Potard.Mod.TremoloRate", KnobShape.Knob),
            new("ModTremoloShape", "Potard.Mod.TremoloShape", KnobShape.Knob),
            new("ModTremoloSpread", "Potard.Mod.TremoloSpread", KnobShape.Knob),
            new("ModTremoloLevel", "Potard.Mod.TremoloLevel", KnobShape.Knob),
            new("ModPhaserSync", "Potard.Mod.PhaserSync", KnobShape.Switch),
            new("ModPhaserTS", "Potard.Mod.PhaserTypeSync", KnobShape.Selector),
            new("ModPhaserRate", "Potard.Mod.PhaserRate", KnobShape.Knob),
            new("ModPhaserDepth", "Potard.Mod.PhaserDepth", KnobShape.Knob),
            new("ModPhaserLevel", "Potard.Mod.PhaserLevel", KnobShape.Knob),
            new("ModFlangerSync", "Potard.Mod.FlangerSync", KnobShape.Switch),
            new("ModFlangerTS", "Potard.Mod.FlangerTypeSync", KnobShape.Selector),
            new("ModFlangerRate", "Potard.Mod.FlangerRate", KnobShape.Knob),
            new("ModFlangerDepth", "Potard.Mod.FlangerDepth", KnobShape.Knob),
            new("ModFlangerFeedback", "Potard.Mod.FlangerFeedback", KnobShape.Knob),
            new("ModFlangerLevel", "Potard.Mod.FlangerLevel", KnobShape.Knob),
            new("ModRotarySync", "Potard.Mod.RotarySync", KnobShape.Switch),
            new("ModRotaryTS", "Potard.Mod.RotaryTypeSync", KnobShape.Selector),
            new("ModRotarySpeed", "Potard.Mod.RotarySpeed", KnobShape.Knob),
            new("ModRotaryRadius", "Potard.Mod.RotaryRadius", KnobShape.Knob),
            new("ModRotarySpread", "Potard.Mod.RotarySpread", KnobShape.Knob),
            new("ModRotaryLevel", "Potard.Mod.RotaryLevel", KnobShape.Knob),
        ]),

        new("delay", "Potard.Section.Delay", "DelayEnable", "DelayPost",
        [
            new("DelayModel", "Potard.Delay.Type", KnobShape.Selector),
            new("DelayDigitalSync", "Potard.Delay.DigitalSync", KnobShape.Switch),
            new("DelayDigitalTS", "Potard.Delay.DigitalTypeSync", KnobShape.Selector),
            new("DelayDigitalTime", "Potard.Delay.DigitalTime", KnobShape.Knob, "ms"),
            new("DelayDigitalFeedback", "Potard.Delay.DigitalFeedback", KnobShape.Knob, "%"),
            new("DelayDigitalMode", "Potard.Delay.DigitalMode", KnobShape.Selector),
            new("DelayDigitalMix", "Potard.Delay.DigitalMix", KnobShape.Knob, "%"),
            new("DelayTapeSync", "Potard.Delay.TapeSync", KnobShape.Switch),
            new("DelayTapeTS", "Potard.Delay.TapeTypeSync", KnobShape.Selector),
            new("DelayTapeTime", "Potard.Delay.TapeTime", KnobShape.Knob, "ms"),
            new("DelayTapeFeedback", "Potard.Delay.TapeFeedback", KnobShape.Knob, "%"),
            new("DelayTapeMode", "Potard.Delay.TapeMode", KnobShape.Selector),
            new("DelayTapeMix", "Potard.Delay.TapeMix", KnobShape.Knob, "%"),
        ]),

        new("reverb", "Potard.Section.Reverb", "ReverbEnable", "ReverbPosition",
        [
            new("ReverbModel", "Potard.Reverb.Type", KnobShape.Selector),
            new("ReverbSpring1Time", "Potard.Reverb.Spring1Time", KnobShape.Knob),
            new("ReverbSpring1PreDelay", "Potard.Reverb.Spring1PreDelay", KnobShape.Knob, "ms"),
            new("ReverbSpring1Color", "Potard.Reverb.Spring1Color", KnobShape.Knob),
            new("ReverbSpring1Mix", "Potard.Reverb.Spring1Mix", KnobShape.Knob, "%"),
            new("ReverbSpring2Time", "Potard.Reverb.Spring2Time", KnobShape.Knob),
            new("ReverbSpring2PreDelay", "Potard.Reverb.Spring2PreDelay", KnobShape.Knob, "ms"),
            new("ReverbSpring2Color", "Potard.Reverb.Spring2Color", KnobShape.Knob),
            new("ReverbSpring2Mix", "Potard.Reverb.Spring2Mix", KnobShape.Knob, "%"),
            new("ReverbSpring3Time", "Potard.Reverb.Spring3Time", KnobShape.Knob),
            new("ReverbSpring3PreDelay", "Potard.Reverb.Spring3PreDelay", KnobShape.Knob, "ms"),
            new("ReverbSpring3Color", "Potard.Reverb.Spring3Color", KnobShape.Knob),
            new("ReverbSpring3Mix", "Potard.Reverb.Spring3Mix", KnobShape.Knob, "%"),
            new("ReverbSpring4Time", "Potard.Reverb.Spring4Time", KnobShape.Knob),
            new("ReverbSpring4PreDelay", "Potard.Reverb.Spring4PreDelay", KnobShape.Knob, "ms"),
            new("ReverbSpring4Color", "Potard.Reverb.Spring4Color", KnobShape.Knob),
            new("ReverbSpring4Mix", "Potard.Reverb.Spring4Mix", KnobShape.Knob, "%"),
            new("ReverbRoomTime", "Potard.Reverb.RoomTime", KnobShape.Knob),
            new("ReverbRoomPreDelay", "Potard.Reverb.RoomPreDelay", KnobShape.Knob, "ms"),
            new("ReverbRoomColor", "Potard.Reverb.RoomColor", KnobShape.Knob),
            new("ReverbRoomMix", "Potard.Reverb.RoomMix", KnobShape.Knob, "%"),
            new("ReverbPlateTime", "Potard.Reverb.PlateTime", KnobShape.Knob),
            new("ReverbPlatePreDelay", "Potard.Reverb.PlatePreDelay", KnobShape.Knob, "ms"),
            new("ReverbPlateColor", "Potard.Reverb.PlateColor", KnobShape.Knob),
            new("ReverbPlateMix", "Potard.Reverb.PlateMix", KnobShape.Knob, "%"),
        ]),

        new("cab", "Potard.Section.Cab", null, null,
        [
            new("CabType", "Potard.Cab.Type", KnobShape.Selector),
            new("VIRCabModel", "Potard.Cab.Model", KnobShape.Selector),
            new("VIRCabMic1Model", "Potard.Cab.Mic1", KnobShape.Selector),
            new("VIRCabMic1X", "Potard.Cab.Mic1Position", KnobShape.Knob),
            new("VIRCabMic1Z", "Potard.Cab.Mic1Distance", KnobShape.Knob),
            new("VIRCabMic2Model", "Potard.Cab.Mic2", KnobShape.Selector),
            new("VIRCabMic2X", "Potard.Cab.Mic2Position", KnobShape.Knob),
            new("VIRCabMic2Z", "Potard.Cab.Mic2Distance", KnobShape.Knob),
            new("VIRCabMicBlend", "Potard.Cab.Blend", KnobShape.Knob, "%"),
            new("VIRCabResonance", "Potard.Cab.Resonance", KnobShape.Knob),
        ]),
    ];

    /// <summary>
    /// Parameters that exist on the preset row but do not belong to any audio block. They are
    /// never duplicated by the hardware prefixes, so they are appended to the software group only.
    /// </summary>
    private static readonly SettingSection[] ExtraSections =
    [
        new("hw-ext", "Potard.Section.HwExt", null, null,
        [
            new("HW_ExtControllerEnable", "Potard.HwExt.Controller", KnobShape.Switch),
        ]),
    ];

    /// <summary>Sections for the software blocks of a preset, with no prefix.</summary>
    public static IReadOnlyList<SettingSection> Sections { get; } =
        Build(prefix: "").Concat(ExtraSections).ToList();

    /// <summary>Sections for the hardware slot A of a preset.</summary>
    public static IReadOnlyList<SettingSection> HardwareASections { get; } = Build("HWParamA_");

    /// <summary>Sections for the hardware slot B of a preset.</summary>
    public static IReadOnlyList<SettingSection> HardwareBSections { get; } = Build("HWParamB_");

    private static IReadOnlyList<SettingSection> Build(string prefix)
        => BaseSections
            .Select(section => new SettingSection(
                Key: prefix.Length == 0 ? section.Key : $"{section.Key}-{prefix.TrimEnd('_')}",
                TitleKey: section.TitleKey,
                EnableParam: section.EnableParam is null ? null : prefix + section.EnableParam,
                PositionParam: section.PositionParam is null ? null : prefix + section.PositionParam,
                Knobs: section.Knobs
                    .Select(knob => knob with { Param = prefix + knob.Param })
                    .ToList()))
            .ToList();

    /// <summary>Every parameter name the catalogue knows about, across all three groups.</summary>
    public static IReadOnlyList<string> AllParameters { get; } =
        Sections.Concat(HardwareASections).Concat(HardwareBSections)
            .SelectMany(section => section.Knobs.Select(knob => knob.Param)
                .Concat(new[] { section.EnableParam, section.PositionParam })
                .Where(param => param is not null)
                .Select(param => param!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(param => param, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Looks up a single parameter definition by its TONEX column name.</summary>
    public static KnobDefinition? Find(string param)
        => AllDefinitions.FirstOrDefault(definition =>
            string.Equals(definition.Param, param, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<KnobDefinition> AllDefinitions
        => Sections.Concat(HardwareASections).Concat(HardwareBSections)
            .SelectMany(section => section.Knobs);
}
