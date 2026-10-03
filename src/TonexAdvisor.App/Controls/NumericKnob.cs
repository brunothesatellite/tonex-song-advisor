using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TonexAdvisor.App.Controls;

/// <summary>
/// A read-only potentiometer that renders the observed position of a preset parameter.
/// </summary>
/// <remarks>
/// TONEX libraries are opened strictly read-only, so this control never accepts input: it is a
/// gauge, not an editor. Values arrive already scaled by <see cref="Minimum"/> and
/// <see cref="Maximum"/>, which are derived from the span actually present in the user's
/// library because TONEX does not publish parameter ranges.
/// </remarks>
public sealed class NumericKnob : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<NumericKnob, double>(nameof(Value));

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<NumericKnob, double>(nameof(Minimum));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<NumericKnob, double>(nameof(Maximum), 1d);

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<NumericKnob, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> ValueBrushProperty =
        AvaloniaProperty.Register<NumericKnob, IBrush?>(nameof(ValueBrush));

    static NumericKnob()
    {
        AffectsRender<NumericKnob>(
            ValueProperty, MinimumProperty, MaximumProperty, TrackBrushProperty, ValueBrushProperty);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public IBrush? TrackBrush
    {
        get => GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public IBrush? ValueBrush
    {
        get => GetValue(ValueBrushProperty);
        set => SetValue(ValueBrushProperty, value);
    }

    /// <summary>Position of the value inside the observed span, clamped to 0..1.</summary>
    public double Fraction
    {
        get
        {
            var span = Maximum - Minimum;
            if (span <= double.Epsilon)
                return 0.5;

            return Math.Clamp((Value - Minimum) / span, 0d, 1d);
        }
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 4 || size.Height <= 4)
            return;

        const double startAngle = 135d;
        const double sweepAngle = 270d;

        var center = new Point(size.Width / 2d, size.Height / 2d);
        var thickness = Math.Max(3d, Math.Min(size.Width, size.Height) * 0.15d);
        var radius = (Math.Min(size.Width, size.Height) - thickness) / 2d - 1d;
        if (radius <= 1d)
            return;

        var track = TrackBrush ?? Brush.Parse("#262630");
        var accent = ValueBrush ?? Brush.Parse("#FF6A00");
        var trackPen = new Pen(track, thickness, lineCap: PenLineCap.Round);
        var valuePen = new Pen(accent, thickness, lineCap: PenLineCap.Round);

        context.DrawGeometry(null, trackPen, CreateArc(center, radius, startAngle, sweepAngle));

        var fraction = Fraction;
        if (fraction > 0.001d)
        {
            context.DrawGeometry(null, valuePen, CreateArc(center, radius, startAngle, sweepAngle * fraction));
        }

        // Knob body, so the gauge reads as hardware rather than a progress ring.
        var bodyRadius = radius - thickness * 0.55d;
        context.DrawEllipse(Brush.Parse("#17171E"), null, center, bodyRadius, bodyRadius);
        context.DrawEllipse(null, new Pen(Brush.Parse("#2C2C38"), 1d), center, bodyRadius, bodyRadius);

        // Pointer.
        var angle = startAngle + sweepAngle * fraction;
        var tip = PointAt(center, radius - thickness * 0.5d, angle);
        var basePoint = PointAt(center, thickness * 0.55d, angle);
        var pointer = new StreamGeometry();
        using (var geometry = pointer.Open())
        {
            geometry.BeginFigure(basePoint, false);
            geometry.LineTo(tip);
            geometry.EndFigure(false);
        }

        context.DrawGeometry(
            null,
            new Pen(accent, 2d, lineCap: PenLineCap.Round),
            pointer);

        context.DrawEllipse(accent, null, PointAt(center, radius, angle), 2.5d, 2.5d);
    }

    private static StreamGeometry CreateArc(Point center, double radius, double startDegrees, double sweepDegrees)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();

        var start = PointAt(center, radius, startDegrees);
        var end = PointAt(center, radius, startDegrees + sweepDegrees);

        context.BeginFigure(start, false);
        context.ArcTo(
            end,
            new Size(radius, radius),
            rotationAngle: 0,
            isLargeArc: sweepDegrees > 180d,
            sweepDirection: SweepDirection.Clockwise,
            isStroked: true);
        context.EndFigure(false);

        return geometry;
    }

    private static Point PointAt(Point center, double radius, double degrees)
    {
        var radians = degrees * Math.PI / 180d;
        return new Point(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
    }
}
