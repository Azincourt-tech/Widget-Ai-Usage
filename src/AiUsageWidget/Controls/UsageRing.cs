using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AiUsageWidget.Controls;

/// <summary>
/// Anel de progresso no estilo dos anéis de atividade do Apple Watch: trilha discreta e arco com
/// pontas arredondadas, começando no topo e seguindo no sentido horário. <see cref="Value"/> vai de 0 a 100.
/// </summary>
public sealed class UsageRing : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<UsageRing, double>(nameof(Value));

    public static readonly StyledProperty<IBrush?> RingBrushProperty =
        AvaloniaProperty.Register<UsageRing, IBrush?>(nameof(RingBrush));

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<UsageRing, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<double> StrokeWidthProperty =
        AvaloniaProperty.Register<UsageRing, double>(nameof(StrokeWidth), 5);

    static UsageRing() =>
        AffectsRender<UsageRing>(ValueProperty, RingBrushProperty, TrackBrushProperty, StrokeWidthProperty);

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public IBrush? RingBrush
    {
        get => GetValue(RingBrushProperty);
        set => SetValue(RingBrushProperty, value);
    }

    public IBrush? TrackBrush
    {
        get => GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public double StrokeWidth
    {
        get => GetValue(StrokeWidthProperty);
        set => SetValue(StrokeWidthProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var stroke = StrokeWidth;
        var size = Math.Min(Bounds.Width, Bounds.Height);
        var radius = (size - stroke) / 2;
        if (radius <= 0)
            return;

        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);

        if (TrackBrush is { } track)
            context.DrawEllipse(null, new Pen(track, stroke), center, radius, radius);

        var fraction = Math.Clamp(double.IsFinite(Value) ? Value : 0, 0, 100) / 100;
        if (fraction <= 0 || RingBrush is not { } ring)
            return;

        var pen = new Pen(ring, stroke, lineCap: PenLineCap.Round);
        if (fraction >= 0.9999)
        {
            context.DrawEllipse(null, pen, center, radius, radius);
            return;
        }

        // Arco do topo (-90°) até o ângulo do valor, no sentido horário.
        var end = -Math.PI / 2 + fraction * 2 * Math.PI;
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(new Point(center.X, center.Y - radius), isFilled: false);
            g.ArcTo(
                new Point(center.X + radius * Math.Cos(end), center.Y + radius * Math.Sin(end)),
                new Size(radius, radius),
                rotationAngle: 0,
                isLargeArc: fraction > 0.5,
                SweepDirection.Clockwise);
            g.EndFigure(isClosed: false);
        }

        context.DrawGeometry(null, pen, geometry);
    }
}
