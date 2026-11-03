using System;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;

namespace Fdb.Services;

public class SqlCompletionData : ICompletionData
{
    public string Text { get; }
    public string InsertionText { get; }
    public string Category { get; }
    public bool IsSnippet { get; }
    public bool IsTable { get; }
    public bool IsKeyword => !IsSnippet && !IsTable;
    public object Content { get; }
    public object? Description { get; }
    public double Priority { get; }
    public Avalonia.Media.IImage? Image => null;

    public SqlCompletionData(string text, string? description = null, double priority = 0, bool isSnippet = false, string? insertionText = null, string? category = null, bool isTable = false)
    {
        Text = text;
        IsSnippet = isSnippet;
        IsTable = isTable;
        InsertionText = insertionText ?? text;
        Category = category ?? (isSnippet ? "Snippet" : (isTable ? "Table" : "Keyword"));
        Priority = priority;
        Description = description ?? (isSnippet ? $"{text}\nCode Snippet Template" : (isTable ? $"Table: {text}" : $"SQL Keyword: {text}"));

        // Return formatted string for Content to avoid control visual parent conflicts in ListBox
        Content = isSnippet ? $"{text}  [Snippet]" : (isTable ? $"{text}  [Table]" : text);
    }

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        try
        {
            if (completionSegment != null && textArea?.Document != null)
            {
                textArea.Document.Replace(completionSegment, InsertionText);
            }
        }
        catch
        {
        }
    }

    public override string ToString() => Text;
}
