using System;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Quell.Services;
using Quell.ViewModels;

namespace Quell.Components;

public class DiagramSurfaceControl : Control
{
    public static readonly StyledProperty<DatabaseDiagramModalViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<DiagramSurfaceControl, DatabaseDiagramModalViewModel?>(nameof(ViewModel));

    public DatabaseDiagramModalViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public DiagramSurfaceControl()
    {
        ClipToBounds = false;
    }

    private static Color ResolveColor(string key, Color fallback)
    {
        if (Application.Current != null &&
            Application.Current.Resources.TryGetValue(key, out var val) &&
            val is ISolidColorBrush scb)
        {
            return scb.Color;
        }
        return fallback;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        bool isDark = ThemeService.Instance.IsDarkTheme();

        // 1. Architectural Dot-Matrix + Major Grid
        DrawGrid(context, bounds, isDark);

        // 2. Orthogonal Stepped ER Relationship Lines with Theme-Synced Badges
        var vm = ViewModel ?? (DataContext as DatabaseDiagramModalViewModel);
        if (vm == null || vm.Relations.Count == 0) return;

        DrawRelationships(context, vm, isDark);
    }

    private void DrawGrid(DrawingContext context, Rect bounds, bool isDark)
    {
        double minorSpacing = 28.0;
        double majorSpacing = minorSpacing * 5.0;

        Color dotColor = isDark
            ? Color.FromArgb(46, 148, 163, 184)
            : Color.FromArgb(62, 100, 116, 139);
        Color majorLineColor = isDark
            ? Color.FromArgb(18, 148, 163, 184)
            : Color.FromArgb(22, 148, 163, 184);

        var majorPen = new Pen(new SolidColorBrush(majorLineColor), 1.0);
        // Period = 1.5 + 26.5 = 28.0px so each horizontal line renders a full row of dots in one draw call
        var dotPen = new Pen(
            new SolidColorBrush(dotColor),
            1.6,
            dashStyle: new DashStyle(new[] { 0.1, minorSpacing - 0.1 }, 0.0),
            lineCap: PenLineCap.Round);

        for (double x = 0; x <= bounds.Width; x += majorSpacing)
        {
            context.DrawLine(majorPen, new Point(x, 0), new Point(x, bounds.Height));
        }

        for (double y = 0; y <= bounds.Height; y += majorSpacing)
        {
            context.DrawLine(majorPen, new Point(0, y), new Point(bounds.Width, y));
        }

        for (double y = minorSpacing; y < bounds.Height; y += minorSpacing)
        {
            context.DrawLine(dotPen, new Point(minorSpacing, y), new Point(bounds.Width, y));
        }
    }

    private void DrawRelationships(DrawingContext context, DatabaseDiagramModalViewModel vm, bool isDark)
    {
        Color accent = ResolveColor("AppAccentColor", isDark ? Color.Parse("#4C7DFF") : Color.Parse("#2563EB"));
        Color surfaceColor = ResolveColor("AppSurfaceBrush", isDark ? Color.Parse("#171B22") : Color.Parse("#F8FAFC"));
        Color borderColor = ResolveColor("AppBorderBrush", isDark ? Color.Parse("#2A303B") : Color.Parse("#E2E8F0"));
        Color textPrimary = ResolveColor("AppTextPrimaryBrush", isDark ? Color.Parse("#E6E8EC") : Color.Parse("#0F172A"));

        bool anyHighlighted = vm.Relations.Any(r => r.IsHighlighted || r.SourceTable.IsHighlighted || r.TargetTable.IsHighlighted);

        Color normalStroke = isDark
            ? Color.FromArgb(anyHighlighted ? (byte)95 : (byte)175, 108, 140, 255)
            : Color.FromArgb(anyHighlighted ? (byte)90 : (byte)185, 37, 99, 235);

        var normalPen = new Pen(new SolidColorBrush(normalStroke), 1.7, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var highlightHaloPen = new Pen(new SolidColorBrush(Color.FromArgb(52, accent.R, accent.G, accent.B)), 6.0, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var highlightPen = new Pen(new SolidColorBrush(accent), 2.5, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

        var anchorBrush = new SolidColorBrush(normalStroke);
        var highlightAnchorBrush = new SolidColorBrush(accent);
        var anchorCenterBrush = new SolidColorBrush(surfaceColor);

        var badgeBgBrush = new SolidColorBrush(Color.FromArgb(242, surfaceColor.R, surfaceColor.G, surfaceColor.B));
        var badgeBorderPen = new Pen(new SolidColorBrush(borderColor), 1.0);
        var badgeHighlightBorderPen = new Pen(new SolidColorBrush(accent), 1.4);
        var badgeTextBrush = new SolidColorBrush(textPrimary);
        var badgeDimTextBrush = new SolidColorBrush(Color.FromArgb(160, textPrimary.R, textPrimary.G, textPrimary.B));

        // Draw non-highlighted lines first so highlighted paths always render cleanly on top
        var ordered = vm.Relations.OrderBy(r => (r.IsHighlighted || r.SourceTable.IsHighlighted || r.TargetTable.IsHighlighted) ? 1 : 0);

        foreach (var rel in ordered)
        {
            var start = rel.StartPoint;
            var end = rel.EndPoint;
            if (start == default || end == default) continue;

            bool isHigh = rel.IsHighlighted || rel.SourceTable.IsHighlighted || rel.TargetTable.IsHighlighted;
            var pen = isHigh ? highlightPen : normalPen;
            var anchor = isHigh ? highlightAnchorBrush : anchorBrush;

            double midX = rel.MidX != 0 ? rel.MidX : (start.X + end.X) / 2.0;
            int startDir = rel.StartDirection != 0 ? rel.StartDirection : 1;
            int endDir = rel.EndDirection != 0 ? rel.EndDirection : 1;

            var geom = new StreamGeometry();
            using (var ctx = geom.Open())
            {
                ctx.BeginFigure(start, false);

                if (startDir == endDir)
                {
                    if (Math.Abs(start.Y - end.Y) < 4.0)
                    {
                        ctx.LineTo(end);
                    }
                    else
                    {
                        double dy = end.Y - start.Y;
                        double signY = Math.Sign(dy);
                        if (signY == 0) signY = 1;
                        double signX = startDir;
                        double r = Math.Min(9.0, Math.Min(Math.Abs(dy) / 2.0, Math.Abs(midX - start.X) / 2.0));

                        ctx.LineTo(new Point(midX - signX * r, start.Y));
                        ctx.QuadraticBezierTo(new Point(midX, start.Y), new Point(midX, start.Y + signY * r));
                        ctx.LineTo(new Point(midX, end.Y - signY * r));
                        ctx.QuadraticBezierTo(new Point(midX, end.Y), new Point(midX + signX * r, end.Y));
                        ctx.LineTo(end);
                    }
                }
                else
                {
                    double dy = end.Y - start.Y;
                    double signY = Math.Sign(dy);
                    if (signY == 0) signY = 1;
                    double r = Math.Min(9.0, Math.Min(Math.Max(Math.Abs(dy) / 2.0, 4.0), Math.Abs(midX - start.X) / 2.0));

                    ctx.LineTo(new Point(midX - r, start.Y));
                    ctx.QuadraticBezierTo(new Point(midX, start.Y), new Point(midX, start.Y + signY * r));
                    ctx.LineTo(new Point(midX, end.Y - signY * r));
                    ctx.QuadraticBezierTo(new Point(midX, end.Y), new Point(midX - r, end.Y));
                    ctx.LineTo(end);
                }
            }

            if (isHigh)
            {
                context.DrawGeometry(null, highlightHaloPen, geom);
            }
            context.DrawGeometry(null, pen, geom);

            // Source FK Anchor Ring
            context.DrawEllipse(anchorCenterBrush, pen, start, 3.6, 3.6);
            context.DrawEllipse(anchor, null, start, 1.6, 1.6);

            // Target PK Arrowhead
            bool pointingRight = (endDir == 1);
            DrawArrowhead(context, anchor, end, pointingRight);

            // Relation Badge
            string badgeText = !string.IsNullOrWhiteSpace(rel.ShortRelationText)
                ? rel.ShortRelationText
                : rel.RelationText;

            if (!string.IsNullOrWhiteSpace(badgeText))
            {
                double labelX = midX;
                double t = (startDir == endDir)
                    ? (0.35 + (rel.RelationIndex % 3) * 0.15)
                    : 0.5;
                double labelY = start.Y + (end.Y - start.Y) * t;
                var textBrush = (anyHighlighted && !isHigh) ? badgeDimTextBrush : badgeTextBrush;
                DrawRelationBadge(context, badgeText, new Point(labelX, labelY), badgeBgBrush, isHigh ? badgeHighlightBorderPen : badgeBorderPen, textBrush);
            }
        }
    }

    private static void DrawArrowhead(DrawingContext context, IBrush brush, Point end, bool pointingRight)
    {
        double size = 5.5;
        var geom = new StreamGeometry();
        using (var ctx = geom.Open())
        {
            ctx.BeginFigure(end, true);
            if (pointingRight)
            {
                ctx.LineTo(new Point(end.X - size * 1.65, end.Y - size));
                ctx.LineTo(new Point(end.X - size * 1.65, end.Y + size));
            }
            else
            {
                ctx.LineTo(new Point(end.X + size * 1.65, end.Y - size));
                ctx.LineTo(new Point(end.X + size * 1.65, end.Y + size));
            }
        }
        context.DrawGeometry(brush, null, geom);
    }

    private static void DrawRelationBadge(DrawingContext context, string text, Point center, IBrush bg, IPen border, IBrush fg)
    {
        var typeface = new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.SemiBold);
        var formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface,
            9.5,
            fg);

        double padX = 7.0;
        double padY = 2.5;
        double w = formatted.Width + padX * 2;
        double h = formatted.Height + padY * 2;
        var rect = new Rect(center.X - w / 2, center.Y - h / 2, w, h);

        context.DrawRectangle(bg, border, rect, 6.0, 6.0);
        context.DrawText(formatted, new Point(rect.X + padX, rect.Y + padY));
    }
}
