using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
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

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        // 1. Draw Architectural CAD-Style Graph Paper Grid
        DrawGrid(context, bounds);

        // 2. Draw ER Relationship Lines (Orthogonal Stepped Lines with Rounded Corners)
        var vm = ViewModel ?? (DataContext as DatabaseDiagramModalViewModel);
        if (vm == null || vm.Relations.Count == 0) return;

        DrawRelationships(context, vm);
    }

    private void DrawGrid(DrawingContext context, Rect bounds)
    {
        // Draw fine grid lines with 32px spacing for high-tech architectural graph look
        double spacing = 32.0;
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(14, 148, 163, 184)), 1.0);
        var majorGridPen = new Pen(new SolidColorBrush(Color.FromArgb(28, 148, 163, 184)), 1.0);

        int index = 0;
        for (double x = 0; x <= bounds.Width; x += spacing)
        {
            var pen = (index % 5 == 0) ? majorGridPen : gridPen;
            context.DrawLine(pen, new Point(x, 0), new Point(x, bounds.Height));
            index++;
        }

        index = 0;
        for (double y = 0; y <= bounds.Height; y += spacing)
        {
            var pen = (index % 5 == 0) ? majorGridPen : gridPen;
            context.DrawLine(pen, new Point(0, y), new Point(bounds.Width, y));
            index++;
        }
    }

    private void DrawRelationships(DrawingContext context, DatabaseDiagramModalViewModel vm)
    {
        var normalPen = new Pen(new SolidColorBrush(Color.FromArgb(210, 56, 189, 248)), 1.8, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var highlightPen = new Pen(new SolidColorBrush(Color.FromRgb(0, 229, 255)), 2.8, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var anchorBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
        var highlightAnchorBrush = new SolidColorBrush(Color.FromRgb(0, 229, 255));
        var badgeBgBrush = new SolidColorBrush(Color.FromArgb(235, 15, 23, 42)); // Slate 900
        var badgeBorderPen = new Pen(new SolidColorBrush(Color.FromArgb(140, 56, 189, 248)), 1.0);
        var badgeTextBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));

        foreach (var rel in vm.Relations)
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

            // Build Orthogonal Stepped Path with Rounded Corners
            var geom = new StreamGeometry();
            using (var ctx = geom.Open())
            {
                ctx.BeginFigure(start, false);

                if (startDir == endDir)
                {
                    // Inter-column stepped routing (left-to-right or right-to-left)
                    if (Math.Abs(start.Y - end.Y) < 4.0)
                    {
                        // Straight horizontal line
                        ctx.LineTo(end);
                    }
                    else
                    {
                        double dy = end.Y - start.Y;
                        double signY = Math.Sign(dy);
                        if (signY == 0) signY = 1;
                        double signX = startDir; // +1 or -1
                        double r = Math.Min(8.0, Math.Min(Math.Abs(dy) / 2.0, Math.Abs(midX - start.X) / 2.0));

                        // 1. Horizontal out from source card
                        ctx.LineTo(new Point(midX - signX * r, start.Y));

                        // 2. Corner 1: turn toward midX and vertical
                        ctx.QuadraticBezierTo(new Point(midX, start.Y), new Point(midX, start.Y + signY * r));

                        // 3. Vertical segment along midX
                        ctx.LineTo(new Point(midX, end.Y - signY * r));

                        // 4. Corner 2: turn from vertical into horizontal toward end
                        ctx.QuadraticBezierTo(new Point(midX, end.Y), new Point(midX + signX * r, end.Y));

                        // 5. Horizontal into target card
                        ctx.LineTo(end);
                    }
                }
                else
                {
                    // Gutter loop routing around table cards (same column or overlapping)
                    double dy = end.Y - start.Y;
                    double signY = Math.Sign(dy);
                    if (signY == 0) signY = 1;
                    double r = Math.Min(8.0, Math.Min(Math.Max(Math.Abs(dy) / 2.0, 4.0), Math.Abs(midX - start.X) / 2.0));

                    // 1. Horizontal out to gutter
                    ctx.LineTo(new Point(midX - r, start.Y));

                    // 2. Corner 1: turn from moving right (+X) to vertical along midX
                    ctx.QuadraticBezierTo(new Point(midX, start.Y), new Point(midX, start.Y + signY * r));

                    // 3. Vertical segment in gutter along midX
                    ctx.LineTo(new Point(midX, end.Y - signY * r));

                    // 4. Corner 2: turn from vertical to moving left (-X) toward target card
                    ctx.QuadraticBezierTo(new Point(midX, end.Y), new Point(midX - r, end.Y));

                    // 5. Horizontal left into target card
                    ctx.LineTo(end);
                }
            }

            // Draw connecting path
            context.DrawGeometry(null, pen, geom);

            // Draw anchor circle on Source FK Column
            context.DrawEllipse(anchor, null, start, 3.5, 3.5);

            // Draw Arrowhead pointing directly into Target Card
            bool pointingRight = (endDir == 1);
            DrawArrowhead(context, anchor, end, pointingRight);

            // Draw Relation Badge
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
                DrawRelationBadge(context, badgeText, new Point(labelX, labelY), badgeBgBrush, badgeBorderPen, badgeTextBrush, isHigh);
            }
        }
    }

    private void DrawArrowhead(DrawingContext context, IBrush brush, Point end, bool pointingRight)
    {
        double size = 6.0;
        var geom = new StreamGeometry();
        using (var ctx = geom.Open())
        {
            ctx.BeginFigure(end, true);
            if (pointingRight)
            {
                ctx.LineTo(new Point(end.X - size * 1.6, end.Y - size));
                ctx.LineTo(new Point(end.X - size * 1.6, end.Y + size));
            }
            else
            {
                ctx.LineTo(new Point(end.X + size * 1.6, end.Y - size));
                ctx.LineTo(new Point(end.X + size * 1.6, end.Y + size));
            }
        }
        context.DrawGeometry(brush, null, geom);
    }

    private void DrawRelationBadge(DrawingContext context, string text, Point center, IBrush bg, IPen border, IBrush fg, bool isHigh)
    {
        var typeface = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);
        var formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface,
            9.0,
            fg);

        double padX = 6.0;
        double padY = 2.5;
        double w = formatted.Width + padX * 2;
        double h = formatted.Height + padY * 2;
        var rect = new Rect(center.X - w / 2, center.Y - h / 2, w, h);

        var activeBorder = isHigh ? new Pen(new SolidColorBrush(Color.FromRgb(0, 229, 255)), 1.4) : border;
        context.DrawRectangle(bg, activeBorder, rect, 4.0, 4.0);
        context.DrawText(formatted, new Point(rect.X + padX, rect.Y + padY));
    }
}
