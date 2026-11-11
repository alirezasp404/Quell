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

            bool goingRight = end.X >= start.X;
            double d1 = goingRight ? 32.0 : -32.0;
            double d2 = goingRight ? -32.0 : 32.0;
            double midX = (start.X + d1 + end.X + d2) / 2.0;

            // Build Orthogonal Stepped Path with Rounded Corners
            var geom = new StreamGeometry();
            using (var ctx = geom.Open())
            {
                ctx.BeginFigure(start, false);

                if (Math.Abs(start.Y - end.Y) < 6)
                {
                    // Nearly horizontal
                    ctx.LineTo(end);
                }
                else
                {
                    double r = 8.0; // corner curve radius
                    double dy = end.Y - start.Y;
                    double signY = Math.Sign(dy);
                    double signX1 = Math.Sign(midX - (start.X + d1));
                    double signX2 = Math.Sign(end.X - midX);

                    // 1. Horizontal out from source table
                    Point p1 = new Point(start.X + d1, start.Y);
                    ctx.LineTo(p1);

                    // 2. Corner 1: turn toward midX and vertical
                    Point pCorner1Start = new Point(midX - signX1 * r, start.Y);
                    Point pCorner1Mid = new Point(midX, start.Y);
                    Point pCorner1End = new Point(midX, start.Y + signY * r);

                    ctx.LineTo(pCorner1Start);
                    ctx.QuadraticBezierTo(pCorner1Mid, pCorner1End);

                    // 3. Vertical segment to Corner 2
                    Point pCorner2Start = new Point(midX, end.Y - signY * r);
                    Point pCorner2Mid = new Point(midX, end.Y);
                    Point pCorner2End = new Point(midX + signX2 * r, end.Y);

                    ctx.LineTo(pCorner2Start);
                    ctx.QuadraticBezierTo(pCorner2Mid, pCorner2End);

                    // 4. Horizontal into target table
                    ctx.LineTo(end);
                }
            }

            // Draw connecting path
            context.DrawGeometry(null, pen, geom);

            // Draw anchor circle on Source FK Column
            context.DrawEllipse(anchor, null, start, 3.5, 3.5);

            // Draw Arrowhead on Target PK Column
            DrawArrowhead(context, anchor, end, goingRight);

            // Draw Relation Badge at midpoint
            if (!string.IsNullOrWhiteSpace(rel.RelationText))
            {
                double labelX = midX;
                double labelY = (start.Y + end.Y) / 2.0;
                DrawRelationBadge(context, rel.RelationText, new Point(labelX, labelY), badgeBgBrush, badgeBorderPen, badgeTextBrush, isHigh);
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
