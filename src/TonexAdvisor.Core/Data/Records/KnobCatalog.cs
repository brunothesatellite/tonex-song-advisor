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
/// <param name="Label">French display label.</param>
/// <param name="Shape">Rendering hint.</param>
/// <param name="Unit">Optional display suffix such as <c>%</c>, <c>ms</c> or <c>Hz</c>.</param>
public sealed record KnobDefinition(string Param, string Label, KnobShape Shape, string? Unit = null);

/// <param name="Key">Stable key used for grouping and persistence.</param>
/// <param name="Title">French section title.</param>
/// <param name="EnableParam">The 0/1 switch that turns the block on, when it has one.</param>
/// <param name="PositionParam">The 0/1 switch selecting pre/post placement, when it has one.</param>
/// <param name="Knobs">Parameters belonging to the section, in signal order.</param>
public sealed record SettingSection(
    string Key,
    string Title,
    string? EnableParam,
    string? PositionParam,
    IReadOnlyList<KnobDefinition> Knobs);

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
        string Title,
        string? EnableParam,
        string? PositionParam,
        IReadOnlyList<KnobDefinition> Knobs);

    private static readonly SectionSpec[] BaseSections =
    [
        new("tone", "Tone model", "ModelEnable", null,
        [
            new("ModelGain", "Gain", KnobShape.Knob),
            new("ModelMix", "Mix", KnobShape.Knob, "%"),
            new("ModelVolume", "Volume", KnobShape.Knob),
            new("PwrAmpEqPresence", "Presence", KnobShape.Knob),
            new("PwrAmpEqDepth", "Depth", KnobShape.Knob),
        ]),

        new("eq", "Égalisation", null, "EqPost",
        [
            new("EqBass", "Graves", KnobShape.Knob),
            new("EqBassFreq", "Fréq. graves", KnobShape.Knob, "Hz"),
            new("EqMid", "Médiums", KnobShape.Knob),
            new("EqMidFreq", "Fréq. médiums", KnobShape.Knob, "Hz"),
            new("EqMidQ", "Q médiums", KnobShape.Knob),
            new("EqTreble", "Aigus", KnobShape.Knob),
            new("EqTrebleFreq", "Fréq. aigus", KnobShape.Knob, "Hz"),
        ]),

        new("comp", "Compresseur", "CompEnable", "CompPost",
        [
            new("CompThreshold", "Seuil", KnobShape.Knob),
            new("CompMakeUp", "Make-up", KnobShape.Knob),
            new("CompAttack", "Attaque", KnobShape.Knob),
        ]),

        new("gate", "Noise gate", "NoiseGateEnable", "NoiseGatePost",
        [
            new("NoiseGateThreshold", "Seuil", KnobShape.Knob),
            new("NoiseGateRelease", "Relâchement", KnobShape.Knob),
            new("NoiseGateDepth", "Profondeur", KnobShape.Knob),
        ]),

        new("mod", "Modulation", "ModEnable", "ModPost",
        [
            new("BPM", "Tempo", KnobShape.Knob, "BPM"),
            new("ModModel", "Type", KnobShape.Selector),
            new("ModChorusSync", "Chorus sync", KnobShape.Switch),
            new("ModChorusTS", "Chorus type de sync", KnobShape.Selector),
            new("ModChorusRate", "Chorus rate", KnobShape.Knob),
            new("ModChorusDepth", "Chorus depth", KnobShape.Knob),
            new("ModChorusLevel", "Chorus level", KnobShape.Knob),
            new("ModTremoloSync", "Tremolo sync", KnobShape.Switch),
            new("ModTremoloTS", "Tremolo type de sync", KnobShape.Selector),
            new("ModTremoloRate", "Tremolo rate", KnobShape.Knob),
            new("ModTremoloShape", "Tremolo shape", KnobShape.Knob),
            new("ModTremoloSpread", "Tremolo spread", KnobShape.Knob),
            new("ModTremoloLevel", "Tremolo level", KnobShape.Knob),
            new("ModPhaserSync", "Phaser sync", KnobShape.Switch),
            new("ModPhaserTS", "Phaser type de sync", KnobShape.Selector),
            new("ModPhaserRate", "Phaser rate", KnobShape.Knob),
            new("ModPhaserDepth", "Phaser depth", KnobShape.Knob),
            new("ModPhaserLevel", "Phaser level", KnobShape.Knob),
            new("ModFlangerSync", "Flanger sync", KnobShape.Switch),
            new("ModFlangerTS", "Flanger type de sync", KnobShape.Selector),
            new("ModFlangerRate", "Flanger rate", KnobShape.Knob),
            new("ModFlangerDepth", "Flanger depth", KnobShape.Knob),
            new("ModFlangerFeedback", "Flanger feedback", KnobShape.Knob),
            new("ModFlangerLevel", "Flanger level", KnobShape.Knob),
            new("ModRotarySync", "Rotary sync", KnobShape.Switch),
            new("ModRotaryTS", "Rotary type de sync", KnobShape.Selector),
            new("ModRotarySpeed", "Rotary speed", KnobShape.Knob),
            new("ModRotaryRadius", "Rotary radius", KnobShape.Knob),
            new("ModRotarySpread", "Rotary spread", KnobShape.Knob),
            new("ModRotaryLevel", "Rotary level", KnobShape.Knob),
        ]),

        new("delay", "Delay", "DelayEnable", "DelayPost",
        [
            new("DelayModel", "Type", KnobShape.Selector),
            new("DelayDigitalSync", "Digital sync", KnobShape.Switch),
            new("DelayDigitalTS", "Digital type de sync", KnobShape.Selector),
            new("DelayDigitalTime", "Digital time", KnobShape.Knob, "ms"),
            new("DelayDigitalFeedback", "Digital feedback", KnobShape.Knob, "%"),
            new("DelayDigitalMode", "Digital mode", KnobShape.Selector),
            new("DelayDigitalMix", "Digital mix", KnobShape.Knob, "%"),
            new("DelayTapeSync", "Tape sync", KnobShape.Switch),
            new("DelayTapeTS", "Tape type de sync", KnobShape.Selector),
            new("DelayTapeTime", "Tape time", KnobShape.Knob, "ms"),
            new("DelayTapeFeedback", "Tape feedback", KnobShape.Knob, "%"),
            new("DelayTapeMode", "Tape mode", KnobShape.Selector),
            new("DelayTapeMix", "Tape mix", KnobShape.Knob, "%"),
        ]),

        new("reverb", "Reverb", "ReverbEnable", "ReverbPosition",
        [
            new("ReverbModel", "Type", KnobShape.Selector),
            new("ReverbSpring1Time", "Spring 1 time", KnobShape.Knob),
            new("ReverbSpring1PreDelay", "Spring 1 predelay", KnobShape.Knob, "ms"),
            new("ReverbSpring1Color", "Spring 1 color", KnobShape.Knob),
            new("ReverbSpring1Mix", "Spring 1 mix", KnobShape.Knob, "%"),
            new("ReverbSpring2Time", "Spring 2 time", KnobShape.Knob),
            new("ReverbSpring2PreDelay", "Spring 2 predelay", KnobShape.Knob, "ms"),
            new("ReverbSpring2Color", "Spring 2 color", KnobShape.Knob),
            new("ReverbSpring2Mix", "Spring 2 mix", KnobShape.Knob, "%"),
            new("ReverbSpring3Time", "Spring 3 time", KnobShape.Knob),
            new("ReverbSpring3PreDelay", "Spring 3 predelay", KnobShape.Knob, "ms"),
            new("ReverbSpring3Color", "Spring 3 color", KnobShape.Knob),
            new("ReverbSpring3Mix", "Spring 3 mix", KnobShape.Knob, "%"),
            new("ReverbSpring4Time", "Spring 4 time", KnobShape.Knob),
            new("ReverbSpring4PreDelay", "Spring 4 predelay", KnobShape.Knob, "ms"),
            new("ReverbSpring4Color", "Spring 4 color", KnobShape.Knob),
            new("ReverbSpring4Mix", "Spring 4 mix", KnobShape.Knob, "%"),
            new("ReverbRoomTime", "Room time", KnobShape.Knob),
            new("ReverbRoomPreDelay", "Room predelay", KnobShape.Knob, "ms"),
            new("ReverbRoomColor", "Room color", KnobShape.Knob),
            new("ReverbRoomMix", "Room mix", KnobShape.Knob, "%"),
            new("ReverbPlateTime", "Plate time", KnobShape.Knob),
            new("ReverbPlatePreDelay", "Plate predelay", KnobShape.Knob, "ms"),
            new("ReverbPlateColor", "Plate color", KnobShape.Knob),
            new("ReverbPlateMix", "Plate mix", KnobShape.Knob, "%"),
        ]),

        new("cab", "Baffle & micros", null, null,
        [
            new("CabType", "Type de cab", KnobShape.Selector),
            new("VIRCabModel", "Baffle", KnobShape.Selector),
            new("VIRCabMic1Model", "Micro 1", KnobShape.Selector),
            new("VIRCabMic1X", "Micro 1 position", KnobShape.Knob),
            new("VIRCabMic1Z", "Micro 1 distance", KnobShape.Knob),
            new("VIRCabMic2Model", "Micro 2", KnobShape.Selector),
            new("VIRCabMic2X", "Micro 2 position", KnobShape.Knob),
            new("VIRCabMic2Z", "Micro 2 distance", KnobShape.Knob),
            new("VIRCabMicBlend", "Blend micros", KnobShape.Knob, "%"),
            new("VIRCabResonance", "Résonance", KnobShape.Knob),
        ]),
    ];

    /// <summary>
    /// Parameters that exist on the preset row but do not belong to any audio block. They are
    /// never duplicated by the hardware prefixes, so they are appended to the software group only.
    /// </summary>
    private static readonly SettingSection[] ExtraSections =
    [
        new("hw-ext", "Contrôleur externe", null, null,
        [
            new("HW_ExtControllerEnable", "Contrôleur externe", KnobShape.Switch),
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
                Title: section.Title,
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
