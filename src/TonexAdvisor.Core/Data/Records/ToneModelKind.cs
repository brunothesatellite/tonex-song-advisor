namespace TonexAdvisor.Core.Data.Records;

/// <summary>What a tone model actually is, derived from <c>ToneModels.Target</c>.</summary>
public enum ToneModelKind
{
    /// <summary>Stomp box capture only.</summary>
    Stomp = 0,

    /// <summary>Stomp followed by an amp.</summary>
    StompAndAmp = 1,

    /// <summary>Amplifier capture only, no cabinet.</summary>
    Amp = 2,

    /// <summary>Amplifier plus cabinet - the most common case.</summary>
    AmpAndCab = 3,

    /// <summary>Full rig: stomp, amp and cabinet together.</summary>
    ComplexRig = 4,

    /// <summary>Impulse response / cabinet only.</summary>
    CustomIR = 5,
}
