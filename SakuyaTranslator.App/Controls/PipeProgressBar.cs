using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace SakuyaTranslator.App.Controls;

public sealed class PipeProgressBar : Control
{
    private readonly DispatcherTimer _animationTimer;

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<PipeProgressBar, double>(nameof(Value));

    public static readonly StyledProperty<string> StatusProperty =
        AvaloniaProperty.Register<PipeProgressBar, string>(nameof(Status), "等待中");

    public static readonly StyledProperty<bool> ShowPercentProperty =
        AvaloniaProperty.Register<PipeProgressBar, bool>(nameof(ShowPercent), true);

    public static readonly StyledProperty<double> PercentFontSizeProperty =
        AvaloniaProperty.Register<PipeProgressBar, double>(nameof(PercentFontSize), 0);

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public bool ShowPercent
    {
        get => GetValue(ShowPercentProperty);
        set => SetValue(ShowPercentProperty, value);
    }

    public double PercentFontSize
    {
        get => GetValue(PercentFontSizeProperty);
        set => SetValue(PercentFontSizeProperty, value);
    }

    static PipeProgressBar()
    {
        AffectsRender<PipeProgressBar>(ValueProperty, StatusProperty, ShowPercentProperty, PercentFontSizeProperty);
    }

    public PipeProgressBar()
    {
        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(45)
        };
        _animationTimer.Tick += (_, _) => InvalidateVisual();
        UpdateAnimationState();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == StatusProperty)
        {
            UpdateAnimationState();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var rect = Bounds.Deflate(0.5);
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        var isDark = ActualThemeVariant == ThemeVariant.Dark;
        var value = Math.Clamp(Value, 0, 100);
        var hasCap = rect.Width > 28;
        var capWidth = hasCap ? Math.Clamp(rect.Height * 0.28, 3, 5) : 0;
        var capGap = hasCap ? Math.Clamp(rect.Height * 0.12, 1, 2) : 0;
        var body = new Rect(rect.X, rect.Y, Math.Max(1, rect.Width - capWidth - capGap), rect.Height);
        var bodyRadius = Math.Min(body.Height / 2, 8);
        var shellBrush = new SolidColorBrush(Color.Parse(isDark ? "#263347" : "#CCD7E4"));
        var shellBorder = new Pen(new SolidColorBrush(Color.Parse(isDark ? "#4B5E7A" : "#9EACBC")), 1);
        var socketBrush = new SolidColorBrush(Color.Parse(isDark ? "#3A4860" : "#B6C3D1"));
        var innerBackground = new SolidColorBrush(Color.Parse(isDark ? "#0E1520" : "#F6F9FC"));

        context.DrawRectangle(shellBrush, shellBorder, body, bodyRadius, bodyRadius);

        if (hasCap)
        {
            var capHeight = Math.Max(4, body.Height * 0.48);
            var cap = new Rect(body.Right + capGap, body.Center.Y - capHeight / 2, capWidth, capHeight);
            context.DrawRectangle(socketBrush, null, cap, capWidth / 2, capWidth / 2);
        }

        var innerInset = Math.Clamp(body.Height * 0.18, 1.5, 3);
        var inner = body.Deflate(innerInset);
        var innerRadius = Math.Min(inner.Height / 2, 6);
        context.DrawRectangle(innerBackground, null, inner, innerRadius, innerRadius);

        var fillWidth = inner.Width * value / 100;
        if (fillWidth > 0)
        {
            var fillRect = new Rect(inner.X, inner.Y, fillWidth, inner.Height);
            var fillColor = GetFillColor(Status);
            var fill = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                [
                    new GradientStop(Lighten(fillColor, 0.18), 0),
                    new GradientStop(fillColor, 0.58),
                    new GradientStop(Darken(fillColor, 0.14), 1)
                ]
            };

            context.DrawRectangle(fill, null, fillRect, innerRadius, innerRadius);

            DrawChargeSegments(context, inner, fillRect, fillColor);

            var shineRect = new Rect(
                fillRect.X + 1,
                fillRect.Y + 1,
                Math.Max(0, fillRect.Width - 2),
                Math.Max(1, fillRect.Height * 0.36));
            context.DrawRectangle(
                new SolidColorBrush(Color.FromArgb(55, 255, 255, 255)),
                null,
                shineRect,
                innerRadius,
                innerRadius);

            if (IsChargingStatus(Status))
            {
                var phase = Environment.TickCount64 / 45.0;
                DrawContainedEnergyFlow(context, fillRect, innerRadius, phase, fillColor);
                DrawChargeHead(context, inner, fillRect, fillColor, phase);
            }

        }

        var topGlow = new Rect(body.X + 1, body.Y + 1, Math.Max(0, body.Width - 2), Math.Max(1, body.Height * 0.34));
        context.DrawRectangle(
            new SolidColorBrush(Color.FromArgb(isDark ? (byte)32 : (byte)85, 255, 255, 255)),
            null,
            topGlow,
            bodyRadius,
            bodyRadius);

        if (ShowPercent)
        {
            var text = $"{value:0}%";
            var fontSize = PercentFontSize > 0
                ? PercentFontSize
                : Math.Clamp(rect.Height * 0.58, 8, 10);
            var formatted = new FormattedText(
                text,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter, Microsoft YaHei UI, Segoe UI"),
                fontSize,
                new SolidColorBrush(Color.Parse(isDark ? "#E6EDF5" : "#243044")));
            var point = new Point(body.Center.X - formatted.Width / 2, body.Center.Y - formatted.Height / 2);
            context.DrawText(formatted, point);
        }
    }

    private void UpdateAnimationState()
    {
        if (IsChargingStatus(Status))
        {
            if (!_animationTimer.IsEnabled)
            {
                _animationTimer.Start();
            }

            return;
        }

        if (_animationTimer.IsEnabled)
        {
            _animationTimer.Stop();
        }
    }

    private static Color GetFillColor(string status)
    {
        return status switch
        {
            "翻译中" => Color.Parse("#1FB8FF"),
            "Running" => Color.Parse("#1FB8FF"),
            "暂停" => Color.Parse("#8A94A6"),
            "Paused" => Color.Parse("#8A94A6"),
            "错误" => Color.Parse("#E15A5A"),
            "Error" => Color.Parse("#E15A5A"),
            "完成" => Color.Parse("#3BB273"),
            "Completed" => Color.Parse("#3BB273"),
            _ => Color.Parse("#9CA8B7")
        };
    }

    private static bool IsChargingStatus(string status)
    {
        return status is "翻译中" or "Running";
    }

    private static void DrawChargeSegments(
        DrawingContext context,
        Rect inner,
        Rect fillRect,
        Color fillColor)
    {
        if (inner.Width < 54 || inner.Height < 9)
        {
            return;
        }

        var gap = Math.Clamp(inner.Height * 0.18, 2, 3);
        var segmentWidth = Math.Clamp(inner.Height * 1.45, 12, 18);
        var segmentHeight = Math.Max(1, inner.Height - gap * 1.6);
        var y = inner.Center.Y - segmentHeight / 2;
        var light = Lighten(fillColor, 0.5);
        for (var x = inner.X + gap; x < fillRect.Right - gap; x += segmentWidth + gap)
        {
            var width = Math.Min(segmentWidth, fillRect.Right - x);
            if (width <= 1)
            {
                continue;
            }

            var segment = new Rect(x, y, width, segmentHeight);
            context.DrawRectangle(
                new SolidColorBrush(Color.FromArgb(42, light.R, light.G, light.B)),
                null,
                segment,
                segment.Height * 0.28,
                segment.Height * 0.28);
        }
    }

    private static void DrawContainedEnergyFlow(
        DrawingContext context,
        Rect fillRect,
        double radius,
        double phase,
        Color fillColor)
    {
        if (fillRect.Width < 4)
        {
            return;
        }

        using (context.PushClip(fillRect))
        {
            var stripeColor = Lighten(fillColor, 0.66);
            var stripeWidth = Math.Max(fillRect.Height * 0.48, 5);
            var spacing = stripeWidth * 2.4;
            var travel = Math.Max(1, spacing);
            var offset = phase * 1.55 % travel;
            for (var x = fillRect.X - spacing + offset; x < fillRect.Right + spacing; x += spacing)
            {
                var stripe = new Rect(x, fillRect.Y - fillRect.Height * 0.35, stripeWidth, fillRect.Height * 1.7);
                var brush = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops =
                    [
                        new GradientStop(Color.FromArgb(0, stripeColor.R, stripeColor.G, stripeColor.B), 0),
                        new GradientStop(Color.FromArgb(54, stripeColor.R, stripeColor.G, stripeColor.B), 0.45),
                        new GradientStop(Color.FromArgb(0, stripeColor.R, stripeColor.G, stripeColor.B), 1)
                    ]
                };
                context.DrawRectangle(brush, null, stripe, radius, radius);
            }

            var waveWidth = Math.Max(fillRect.Height * 2.4, 18);
            var waveTravel = Math.Max(1, fillRect.Width + waveWidth);
            var waveX = fillRect.Right - (phase * 2.15 % waveTravel);
            var wave = new Rect(waveX, fillRect.Y + fillRect.Height * 0.18, waveWidth, fillRect.Height * 0.64);
            var waveBrush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops =
                [
                    new GradientStop(Color.FromArgb(0, 255, 255, 255), 0),
                    new GradientStop(Color.FromArgb(96, 255, 255, 255), 0.55),
                    new GradientStop(Color.FromArgb(0, 255, 255, 255), 1)
                ]
            };
            context.DrawRectangle(
                waveBrush,
                null,
                wave,
                wave.Height / 2,
                wave.Height / 2);
        }
    }

    private static void DrawChargeHead(
        DrawingContext context,
        Rect inner,
        Rect fillRect,
        Color fillColor,
        double phase)
    {
        var x = Math.Clamp(fillRect.Right, inner.X, inner.Right);
        var radius = Math.Clamp(inner.Height * (0.48 + Math.Sin(phase * 0.35) * 0.08), 3, 7);
        var glow = Lighten(fillColor, 0.62);
        var center = new Point(x, inner.Center.Y);
        context.DrawEllipse(
            new SolidColorBrush(Color.FromArgb(72, glow.R, glow.G, glow.B)),
            null,
            center,
            radius * 1.9,
            radius * 1.25);
        context.DrawEllipse(
            new SolidColorBrush(Color.FromArgb(210, 255, 255, 255)),
            null,
            center,
            radius * 0.42,
            radius * 0.42);
    }

    private static Color Lighten(Color color, double amount)
    {
        return Color.FromArgb(
            color.A,
            BlendChannel(color.R, 255, amount),
            BlendChannel(color.G, 255, amount),
            BlendChannel(color.B, 255, amount));
    }

    private static Color Darken(Color color, double amount)
    {
        return Color.FromArgb(
            color.A,
            BlendChannel(color.R, 0, amount),
            BlendChannel(color.G, 0, amount),
            BlendChannel(color.B, 0, amount));
    }

    private static byte BlendChannel(byte from, byte to, double amount)
    {
        return (byte)Math.Clamp(from + (to - from) * amount, 0, 255);
    }
}
