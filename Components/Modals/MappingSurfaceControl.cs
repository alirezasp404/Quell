using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Quell.ViewModels;

namespace Quell.Components;

public class MappingSurfaceControl : Control
{
    public static readonly StyledProperty<ImportDataModalViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<MappingSurfaceControl, ImportDataModalViewModel?>(nameof(ViewModel));

    public ImportDataModalViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public MappingSurfaceControl()
    {
        ClipToBounds = false;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        // 1. Draw CAD-Style Blueprint Graph Paper Grid
        DrawGrid(context, bounds);

        // 2. Draw Bezier Connecting Cables between Table and Excel
        var vm = ViewModel ?? (DataContext as ImportDataModalViewModel);
        if (vm == null || vm.ColumnMappings.Count == 0) return;

        DrawConnections(context, vm);
    }

    private void DrawGrid(DrawingContext context, Rect bounds)
    {
        double spacing = 32.0;
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(14, 148, 163, 184)), 1.0);
        var majorGridPen = new Pen(new SolidColorBrush(Color.FromArgb(28, 148, 163, 184)), 1.0);

        int idx = 0;
        for (double x = 0; x <= bounds.Width; x += spacing)
        {
            var pen = (idx % 5 == 0) ? majorGridPen : gridPen;
            context.DrawLine(pen, new Point(x, 0), new Point(x, bounds.Height));
            idx++;
        }

        idx = 0;
        for (double y = 0; y <= bounds.Height; y += spacing)
        {
            var pen = (idx % 5 == 0) ? majorGridPen : gridPen;
            context.DrawLine(pen, new Point(0, y), new Point(bounds.Width, y));
            idx++;
        }
    }

    private void DrawConnections(DrawingContext context, ImportDataModalViewModel vm)
    {
        // Geometric parameters matching the cards layout on the WorldCanvas
        const double LeftCardX = 50.0;
        const double LeftCardY = 40.0;
        const double LeftCardWidth = 320.0;
        const double LeftHeaderHeight = 44.0;
        const double LeftRowHeight = 36.0;

        const double RightCardX = 640.0;
        const double RightCardY = 40.0;
        const double RightHeaderHeight = 44.0;
        const double RightRowHeight = 36.0;

        double leftPortX = LeftCardX + LeftCardWidth;
        double rightPortX = RightCardX;

        var mappedPen = new Pen(new SolidColorBrush(Color.FromArgb(220, 16, 185, 129)), 2.2, lineCap: PenLineCap.Round);
        var exactPen = new Pen(new SolidColorBrush(Color.FromArgb(240, 56, 189, 248)), 2.4, lineCap: PenLineCap.Round);
        var warningPen = new Pen(new SolidColorBrush(Color.FromArgb(220, 245, 158, 11)), 2.0, lineCap: PenLineCap.Round);

        var mappedPortBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
        var exactPortBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
        var warningPortBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
        var disabledPortBrush = new SolidColorBrush(Color.FromArgb(90, 100, 116, 139));

        var badgeBgBrush = new SolidColorBrush(Color.FromArgb(240, 15, 23, 42)); // Dark Slate
        var badgeBorderPen = new Pen(new SolidColorBrush(Color.FromArgb(160, 56, 189, 248)), 1.0);
        var badgeTextBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));

        var typeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.SemiBold);

        for (int i = 0; i < vm.ColumnMappings.Count; i++)
        {
            var m = vm.ColumnMappings[i];
            double startY = LeftCardY + LeftHeaderHeight + i * LeftRowHeight + LeftRowHeight / 2.0;
            var start = new Point(leftPortX, startY);

            if (!m.IsIncluded)
            {
                // Draw dimmed excluded port
                context.DrawEllipse(disabledPortBrush, null, start, 4.0, 4.0);
                continue;
            }

            if (m.IsConstant)
            {
                // Draw constant value port
                context.DrawEllipse(exactPortBrush, null, start, 5.0, 5.0);
                continue;
            }

            if (m.IsSkipped)
            {
                if (m.HasWarning)
                {
                    // Draw warning pulsing port for required missing column
                    context.DrawEllipse(warningPortBrush, new Pen(new SolidColorBrush(Color.FromArgb(80, 239, 68, 68)), 4.0), start, 5.5, 5.5);
                }
                else
                {
                    context.DrawEllipse(disabledPortBrush, null, start, 4.0, 4.0);
                }
                continue;
            }

            // Mapped to an Excel column
            int excelIdx = vm.ExtractExcelColumnIndex(m.SelectedExcelColumn);
            if (excelIdx < 0 || excelIdx >= vm.ExcelHeaders.Count)
            {
                context.DrawEllipse(mappedPortBrush, null, start, 5.0, 5.0);
                continue;
            }

            double endY = RightCardY + RightHeaderHeight + excelIdx * RightRowHeight + RightRowHeight / 2.0;
            var end = new Point(rightPortX, endY);

            bool isExact = m.MatchQuality == "Exact Match";
            var activePen = isExact ? exactPen : mappedPen;
            var activePortBrush = isExact ? exactPortBrush : mappedPortBrush;

            // Draw Cubic Bezier Wire
            double dx = rightPortX - leftPortX;
            var cp1 = new Point(leftPortX + dx * 0.42, startY);
            var cp2 = new Point(rightPortX - dx * 0.42, endY);

            var geom = new StreamGeometry();
            using (var ctx = geom.Open())
            {
                ctx.BeginFigure(start, false);
                ctx.CubicBezierTo(cp1, cp2, end);
            }
            context.DrawGeometry(null, activePen, geom);

            // Draw Port Endpoints
            context.DrawEllipse(activePortBrush, new Pen(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), 2.5), start, 5.0, 5.0);
            context.DrawEllipse(activePortBrush, new Pen(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), 2.5), end, 5.0, 5.0);

            // Draw status badge at curve midpoint
            var mid = new Point((start.X + end.X) / 2.0, (start.Y + end.Y) / 2.0);
            string badgeText = m.MatchQuality ?? "Linked";
            var formattedText = new FormattedText(
                badgeText,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                9.0,
                badgeTextBrush);

            double badgeW = formattedText.Width + 12.0;
            double badgeH = 16.0;
            var badgeRect = new Rect(mid.X - badgeW / 2.0, mid.Y - badgeH / 2.0, badgeW, badgeH);

            context.DrawRectangle(badgeBgBrush, badgeBorderPen, new RoundedRect(badgeRect, 8));
            context.DrawText(formattedText, new Point(badgeRect.X + 6.0, badgeRect.Y + (badgeH - formattedText.Height) / 2.0));
        }
    }
}
