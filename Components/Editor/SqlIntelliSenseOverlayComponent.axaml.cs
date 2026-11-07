using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Quell.Services;
using Quell.Services.DatabaseProviders;

namespace Quell.Components;

public partial class SqlIntelliSenseOverlayComponent : UserControl
{
    private readonly List<SqlCompletionData> _currentMatches = new();
    private string _activeFilterWord = string.Empty;

    public event EventHandler<SqlCompletionData>? ItemCommitted;
    public event EventHandler? Dismissed;

    public int ItemCount => _currentMatches.Count;
    public SqlCompletionData? SelectedItem => ItemsListBox.SelectedItem as SqlCompletionData;
    public string ActiveFilterWord => _activeFilterWord;

    private static readonly HashSet<string> HighPriorityKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "FROM", "WHERE", "INSERT", "UPDATE", "DELETE", "JOIN", "INNER JOIN", "LEFT JOIN", 
        "CREATE", "TABLE", "GROUP BY", "ORDER BY", "HAVING", "LIMIT", "OFFSET", "UNION", "VALUES",
        "ALTER", "DROP", "EXEC", "WITH", "SET", "AND", "OR", "NOT", "IN", "AS", "BY", "ON"
    };

    private static readonly HashSet<string> TableContextKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "FROM", "JOIN", "INTO", "UPDATE", "TABLE", "TRUNCATE", "REFERENCES", "DESCRIBE", "DESC"
    };

    public static bool IsTableContext(string? textBeforeWord)
    {
        if (string.IsNullOrWhiteSpace(textBeforeWord)) return false;

        var trimmed = textBeforeWord.TrimEnd();
        int end = trimmed.Length;
        int start = end;
        while (start > 0 && (char.IsLetterOrDigit(trimmed[start - 1]) || trimmed[start - 1] == '_'))
        {
            start--;
        }

        if (start < end)
        {
            string lastKeyword = trimmed.Substring(start, end - start);
            return TableContextKeywords.Contains(lastKeyword);
        }
        return false;
    }

    public SqlIntelliSenseOverlayComponent()
    {
        InitializeComponent();

        ItemsListBox.SelectionChanged += OnSelectionChanged;
        ItemsListBox.DoubleTapped += (s, e) => CommitSelected();
        ItemsListBox.PointerPressed += OnListBoxPointerPressed;
    }

    private static double GetKeywordPriority(string kw, string query)
    {
        double score = 100.0;
        if (kw.Equals("SELECT", StringComparison.OrdinalIgnoreCase)) score += 1000.0;
        else if (kw.Equals("FROM", StringComparison.OrdinalIgnoreCase)) score += 900.0;
        else if (kw.Equals("WHERE", StringComparison.OrdinalIgnoreCase)) score += 800.0;
        else if (kw.Equals("INSERT", StringComparison.OrdinalIgnoreCase) || kw.Equals("INSERT INTO", StringComparison.OrdinalIgnoreCase)) score += 750.0;
        else if (kw.Equals("UPDATE", StringComparison.OrdinalIgnoreCase)) score += 700.0;
        else if (kw.Equals("DELETE", StringComparison.OrdinalIgnoreCase) || kw.Equals("DELETE FROM", StringComparison.OrdinalIgnoreCase)) score += 650.0;
        else if (HighPriorityKeywords.Contains(kw)) score += 500.0;

        if (kw.StartsWith(query, StringComparison.OrdinalIgnoreCase)) score += 300.0;
        return score;
    }

    private static double GetSnippetPriority(string snipKeyword, string query)
    {
        double score = 150.0;
        if (snipKeyword.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)) score += 950.0;
        else if (snipKeyword.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase)) score += 700.0;
        else if (snipKeyword.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)) score += 680.0;

        if (snipKeyword.StartsWith(query, StringComparison.OrdinalIgnoreCase)) score += 250.0;
        return score;
    }

    public bool UpdateFilter(string currentWord, bool showAll = false, string? textBefore = null)
    {
        try
        {
            var keywordsService = SqlKeywordsService.Instance;
            var activeTables = keywordsService.ActiveTables.Count > 0
                ? keywordsService.ActiveTables
                : DatabaseSessionService.Instance.GetActiveCachedTables().ToList();

            bool inTableContext = IsTableContext(textBefore);

            // 1. Find matching tables from active database
            var matchingTables = showAll
                ? activeTables
                : activeTables.Where(t =>
                    string.IsNullOrEmpty(currentWord) ||
                    t.TableName.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase) ||
                    t.FullName.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase) ||
                    (currentWord.Length >= 2 && (t.TableName.IndexOf(currentWord, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                 t.FullName.IndexOf(currentWord, StringComparison.OrdinalIgnoreCase) >= 0)))
                    .ToList();

            // 2. Find matching keywords (Prefix match first, then substring match for length >= 2)
            var matchingKeywords = showAll
                ? keywordsService.Keywords
                : keywordsService.Keywords
                    .Where(k => k.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase) ||
                                (currentWord.Length >= 2 && k.IndexOf(currentWord, StringComparison.OrdinalIgnoreCase) >= 0))
                    .OrderByDescending(k => GetKeywordPriority(k, currentWord))
                    .ThenBy(k => k.Length)
                    .ThenBy(k => k)
                    .ToList();

            // 3. Find matching snippets
            var matchingSnippets = showAll
                ? keywordsService.Snippets
                : keywordsService.Snippets
                    .Where(s => s.Keyword.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase) ||
                                (currentWord.Length >= 2 && (s.Keyword.IndexOf(currentWord, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                             s.Description.IndexOf(currentWord, StringComparison.OrdinalIgnoreCase) >= 0)))
                    .OrderByDescending(s => GetSnippetPriority(s.Keyword, currentWord))
                    .ThenBy(s => s.Keyword.Length)
                    .ToList();

            _currentMatches.Clear();

            // Add active database tables
            foreach (var table in matchingTables)
            {
                double priority = inTableContext ? 3000.0 : 1200.0;
                if (table.TableName.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase))
                {
                    priority += 400.0;
                }

                string dbInfo = !string.IsNullOrEmpty(keywordsService.ActiveDatabaseName)
                    ? $" [DB: {keywordsService.ActiveDatabaseName}]"
                    : "";

                _currentMatches.Add(new SqlCompletionData(
                    text: table.TableName,
                    description: $"{table.DisplayType}: {table.FullName}{dbInfo}\nActive Database Table",
                    priority: priority,
                    isSnippet: false,
                    insertionText: table.TableName,
                    category: table.DisplayType,
                    isTable: true
                ));

                // If non-standard schema (not dbo/main), also offer full qualified name
                if (!string.IsNullOrEmpty(table.SchemaName) &&
                    !table.SchemaName.Equals("dbo", StringComparison.OrdinalIgnoreCase) &&
                    !table.SchemaName.Equals("main", StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrEmpty(currentWord) || table.FullName.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase)))
                {
                    _currentMatches.Add(new SqlCompletionData(
                        text: table.FullName,
                        description: $"{table.DisplayType}: {table.FullName}{dbInfo}\nActive Database Table (Qualified)",
                        priority: priority - 10.0,
                        isSnippet: false,
                        insertionText: table.FullName,
                        category: table.DisplayType,
                        isTable: true
                    ));
                }
            }

            // Add keywords
            foreach (var kw in matchingKeywords)
            {
                double priority = GetKeywordPriority(kw, currentWord);
                if (inTableContext)
                {
                    priority = Math.Min(priority, 500.0);
                }
                _currentMatches.Add(new SqlCompletionData(kw, description: $"SQL Keyword: {kw}", priority: priority, isSnippet: false));
            }

            // Add snippets
            foreach (var snip in matchingSnippets)
            {
                double priority = GetSnippetPriority(snip.Keyword, currentWord);
                if (inTableContext)
                {
                    priority = Math.Min(priority, 400.0);
                }
                _currentMatches.Add(new SqlCompletionData(
                    text: snip.Keyword,
                    description: $"{snip.Keyword} — {snip.Description}\nTemplate: {snip.Snippet}",
                    priority: priority,
                    isSnippet: true,
                    insertionText: snip.Snippet,
                    category: "Snippet"
                ));
            }

            if (_currentMatches.Count == 0)
            {
                Hide();
                return false;
            }

            // Sort combined list by priority descending
            _currentMatches.Sort((a, b) => b.Priority.CompareTo(a.Priority));

            _activeFilterWord = currentWord;
            ItemsListBox.ItemsSource = null;
            ItemsListBox.ItemsSource = _currentMatches.Take(60).ToList();
            ItemsListBox.SelectedIndex = 0;

            IsVisible = true;
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SqlIntelliSenseOverlay.UpdateFilter Exception]: {ex}");
            Hide();
            return false;
        }
    }

    public void PositionAt(Point canvasPoint, double lineHeight, Size canvasSize)
    {
        try
        {
            double boxWidth = CardContainer.Width > 0 ? CardContainer.Width : 340.0;
            double boxHeight = 260.0;
            double canvasW = canvasSize.Width;
            double canvasH = canvasSize.Height;

            // Compute Left
            double left = canvasPoint.X;
            if (canvasW > 0 && left + boxWidth > canvasW)
            {
                left = Math.Max(6, canvasW - boxWidth - 12);
            }
            left = Math.Max(6, left);

            // Compute Top (flip above caret if near bottom)
            double top = canvasPoint.Y + 2;
            if (canvasH > 0 && top + boxHeight > canvasH && canvasPoint.Y > (boxHeight + 25))
            {
                double lh = lineHeight > 0 ? lineHeight : 18.0;
                top = Math.Max(6, canvasPoint.Y - boxHeight - lh - 4);
            }
            top = Math.Max(6, top);

            Canvas.SetLeft(this, left);
            Canvas.SetTop(this, top);
        }
        catch
        {
        }
    }

    public void SelectNext()
    {
        if (_currentMatches.Count == 0) return;
        int nextIndex = Math.Min(_currentMatches.Count - 1, ItemsListBox.SelectedIndex + 1);
        ItemsListBox.SelectedIndex = nextIndex;
        if (ItemsListBox.SelectedItem != null)
        {
            ItemsListBox.ScrollIntoView(ItemsListBox.SelectedItem);
        }
    }

    public void SelectPrevious()
    {
        if (_currentMatches.Count == 0) return;
        int prevIndex = Math.Max(0, ItemsListBox.SelectedIndex - 1);
        ItemsListBox.SelectedIndex = prevIndex;
        if (ItemsListBox.SelectedItem != null)
        {
            ItemsListBox.ScrollIntoView(ItemsListBox.SelectedItem);
        }
    }

    public void CommitSelected()
    {
        if (SelectedItem != null)
        {
            var item = SelectedItem;
            Hide();
            ItemCommitted?.Invoke(this, item);
        }
        else
        {
            Hide();
        }
    }

    public void Hide()
    {
        IsVisible = false;
        _currentMatches.Clear();
        _activeFilterWord = string.Empty;
        Dismissed?.Invoke(this, EventArgs.Empty);
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (SelectedItem != null && !string.IsNullOrWhiteSpace(SelectedItem.Description?.ToString()))
        {
            DescriptionText.Text = SelectedItem.Description.ToString();
            DescriptionBorder.IsVisible = true;
        }
        else
        {
            DescriptionBorder.IsVisible = false;
        }
    }

    private void OnListBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (SelectedItem != null)
            {
                CommitSelected();
            }
        }, DispatcherPriority.Input);
    }
}
