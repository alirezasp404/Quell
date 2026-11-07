using System.Text.RegularExpressions;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Quell.Services;

public class SqlKeywordColorizer : DocumentColorizingTransformer
{
    protected override void ColorizeLine(DocumentLine line)
    {
        try
        {
            if (line == null || CurrentContext?.Document == null) return;

            var regex = SqlKeywordsService.Instance.KeywordRegex;
            if (regex == null) return;

            int lineStartOffset = line.Offset;
            int lineEndOffset = line.EndOffset;
            string text = CurrentContext.Document.GetText(line);

            // Dynamically query active keyword color brush based on user settings and light/dark theme
            var keywordBrush = ThemeService.Instance.CurrentKeywordBrush;

            var matches = regex.Matches(text);
            foreach (Match match in matches)
            {
                int start = lineStartOffset + match.Index;
                int end = start + match.Length;

                // Ensure bounds are strictly within the line to prevent AvaloniaEdit out-of-range exceptions
                if (start >= lineStartOffset && end <= lineEndOffset && start < end)
                {
                    ChangeLinePart(start, end, element =>
                    {
                        element.TextRunProperties.SetForegroundBrush(keywordBrush);
                    });
                }
            }
        }
        catch
        {
            // Colorizing must never throw or crash the editor
        }
    }
}
