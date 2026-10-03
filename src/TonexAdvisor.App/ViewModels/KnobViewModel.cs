using TonexAdvisor.Core.Data.Records;

namespace TonexAdvisor.App.ViewModels;

/// <summary>One displayed parameter of a preset.</summary>
public sealed class KnobViewModel
{
    public KnobViewModel(
        string param,
        string label,
        KnobShape shape,
        string display,
        string unit,
        double value,
        double fraction,
        double minimum,
        double maximum,
        bool isOn)
    {
        Param = param;
        Label = label;
        Shape = shape;
        Display = display;
        Unit = unit;
        Value = value;
        Fraction = fraction;
        Minimum = minimum;
        Maximum = maximum;
        IsOn = isOn;
    }

    /// <summary>TONEX column name, echoed so an AI recommendation can name it precisely.</summary>
    public string Param { get; }

    public string Label { get; }

    public KnobShape Shape { get; }

    /// <summary>Human readable value, already formatted with invariant decimals.</summary>
    public string Display { get; }

    public string Unit { get; }

    /// <summary>Raw value fed to the potentiometer control.</summary>
    public double Value { get; }

    /// <summary>Position inside the observed span, 0..1.</summary>
    public double Fraction { get; }

    public double Minimum { get; }

    public double Maximum { get; }

    /// <summary>State of a 0/1 switch parameter.</summary>
    public bool IsOn { get; }

    public bool IsKnob => Shape == KnobShape.Knob;

    public bool IsSwitch => Shape == KnobShape.Switch;

    public bool IsSelector => Shape == KnobShape.Selector;

    public bool IsSwitchOn => IsSwitch && IsOn;

    public bool IsSwitchOff => IsSwitch && !IsOn;

    public bool HasUnit => Unit.Length > 0;

    public string SwitchLabel => IsOn ? "ACTIF" : "BYPASS";
}
