using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaEdit.Rendering;
using Fdb.Services;
using Fdb.ViewModels;

namespace Fdb.Components;

public partial class EditorComponent : UserControl
{
    private EditorViewModel? _viewModel;

    public EditorComponent()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // 1. Install SQL Keyword Syntax Colorizer (Dynamic Highlighting)
        CodeEditor.TextArea.TextView.LineTransformers.Add(new SqlKeywordColorizer());
        ThemeService.Instance.KeywordColorChanged += (s, e) => CodeEditor.TextArea?.TextView?.Redraw();
        ThemeService.Instance.ThemeChanged += (s, e) => CodeEditor.TextArea?.TextView?.Redraw();

        // 2. Install Code Completion & Keyboard Handlers
        CodeEditor.TextArea.TextEntered += OnTextEntered;
        CodeEditor.TextArea.KeyDown += OnKeyDown;
        CodeEditor.TextArea.PointerPressed += (s, e) => HideCompletion();
        CodeEditor.PointerWheelChanged += OnPointerWheelChanged;

        // Tunneling key handler ensures zoom shortcuts & completion navigation are intercepted
        AddHandler(KeyDownEvent, OnTunnelKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as EditorViewModel;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            LoadActiveTabContent();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorViewModel.SelectedTab))
        {
            HideCompletion();
            LoadActiveTabContent();
        }
    }

    private void LoadActiveTabContent()
    {
        if (_viewModel?.SelectedTab == null || CodeEditor == null) return;

        if (CodeEditor.Document != _viewModel.SelectedTab.Document)
        {
            CodeEditor.Document = _viewModel.SelectedTab.Document;
            CodeEditor.TextArea.Focus();
        }
    }

    private void OnTabStripTabSelected(object? sender, EditorTabItem tab)
    {
        HideCompletion();
    }

    public void OnTabPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        HideCompletion();
        if ((sender as Border)?.DataContext is EditorTabItem clickedTab && _viewModel != null)
        {
            _viewModel.SelectTab(clickedTab);
        }
    }

    #region Zoom Implementation (Ctrl + Plus / Minus / 0)

    public void ZoomIn()
    {
        if (CodeEditor != null)
        {
            CodeEditor.FontSize = Math.Min(40.0, Math.Round(CodeEditor.FontSize + 1.0));
            if (IntelliSenseOverlay?.IsVisible == true) PositionIntelliSense();
        }
    }

    public void ZoomOut()
    {
        if (CodeEditor != null)
        {
            CodeEditor.FontSize = Math.Max(8.0, Math.Round(CodeEditor.FontSize - 1.0));
            if (IntelliSenseOverlay?.IsVisible == true) PositionIntelliSense();
        }
    }

    public void ResetZoom()
    {
        if (CodeEditor != null)
        {
            CodeEditor.FontSize = 13.0;
            if (IntelliSenseOverlay?.IsVisible == true) PositionIntelliSense();
        }
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (e.Delta.Y > 0)
            {
                ZoomIn();
                e.Handled = true;
            }
            else if (e.Delta.Y < 0)
            {
                ZoomOut();
                e.Handled = true;
            }
        }
    }

    private void OnTunnelKeyDown(object? sender, KeyEventArgs e)
    {
        // 1. First priority: Handle Zoom shortcuts (Ctrl++, Ctrl+-, Ctrl+0)
        if (HandleZoomKeys(e)) return;

        // 2. Second priority: If IntelliSense overlay is visible, intercept navigation and commit keys
        if (IntelliSenseOverlay?.IsVisible == true)
        {
            if (e.Key == Key.Escape)
            {
                IntelliSenseOverlay.Hide();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Down)
            {
                IntelliSenseOverlay.SelectNext();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Up)
            {
                IntelliSenseOverlay.SelectPrevious();
                e.Handled = true;
                return;
            }

            if (e.Key is Key.Tab or Key.Enter)
            {
                IntelliSenseOverlay.CommitSelected();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Back)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    int caretOffset = CodeEditor.CaretOffset;
                    string currentWord = GetWordBeforeOffset(caretOffset);
                    if (currentWord.Length >= 1)
                    {
                        ShowIntelliSense(currentWord, showAll: false);
                    }
                    else
                    {
                        IntelliSenseOverlay.Hide();
                    }
                }, DispatcherPriority.Input);
                return;
            }
        }
    }

    private bool HandleZoomKeys(KeyEventArgs e)
    {
        bool isCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (!isCtrl) return false;

        // Zoom In: Ctrl + Plus, Ctrl + Add, Ctrl + '=' (OemPlus)
        if (e.Key is Key.OemPlus or Key.Add)
        {
            ZoomIn();
            e.Handled = true;
            return true;
        }

        // Zoom Out: Ctrl + Minus, Ctrl + Subtract, Ctrl + '-' (OemMinus)
        if (e.Key is Key.OemMinus or Key.Subtract)
        {
            ZoomOut();
            e.Handled = true;
            return true;
        }

        // Reset Zoom: Ctrl + 0 or Ctrl + NumPad0
        if (e.Key is Key.D0 or Key.NumPad0)
        {
            ResetZoom();
            e.Handled = true;
            return true;
        }

        return false;
    }

    #endregion

    #region IntelliSense Integration

    private void OnTextEntered(object? sender, TextInputEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(e.Text)) return;

            char enteredChar = e.Text[0];

            // Trigger or update completion when typing alphanumeric or underscore
            if (char.IsLetterOrDigit(enteredChar) || enteredChar == '_')
            {
                int caretOffset = CodeEditor.CaretOffset;
                string currentWord = GetWordBeforeOffset(caretOffset);

                if (currentWord.Length >= 1)
                {
                    ShowIntelliSense(currentWord, showAll: false);
                }
                else
                {
                    IntelliSenseOverlay?.Hide();
                }
            }
            else
            {
                // Typing spaces, newlines, semicolons, or punctuation closes the suggestion box
                IntelliSenseOverlay?.Hide();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EditorComponent.OnTextEntered Exception]: {ex}");
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        try
        {
            // 0. Handle Zoom Shortcuts
            if (HandleZoomKeys(e)) return;

            // 1. Explicit Ctrl+Z Undo
            if (e.Key == Key.Z && e.KeyModifiers == KeyModifiers.Control)
            {
                HideCompletion();
                if (CodeEditor.CanUndo)
                {
                    CodeEditor.Undo();
                    e.Handled = true;
                    return;
                }
            }

            // 2. Explicit Ctrl+Y or Ctrl+Shift+Z Redo
            if ((e.Key == Key.Y && e.KeyModifiers == KeyModifiers.Control) ||
                (e.Key == Key.Z && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift)))
            {
                HideCompletion();
                if (CodeEditor.CanRedo)
                {
                    CodeEditor.Redo();
                    e.Handled = true;
                    return;
                }
            }

            // 3. Ctrl+Space to trigger IntelliSense manually
            if (e.Key == Key.Space && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                int caretOffset = CodeEditor.CaretOffset;
                string currentWord = GetWordBeforeOffset(caretOffset);
                ShowIntelliSense(currentWord, showAll: true);
                e.Handled = true;
                return;
            }
        }
        catch
        {
            // Safety guard: key handling must never crash the editor
        }
    }

    public void ShowIntelliSense(string currentWord, bool showAll = false)
    {
        if (IntelliSenseOverlay == null || CodeEditor?.Document == null) return;

        bool hasMatches = IntelliSenseOverlay.UpdateFilter(currentWord, showAll);
        if (hasMatches)
        {
            PositionIntelliSense();
        }
    }

    public void PositionIntelliSense()
    {
        try
        {
            if (CompletionOverlayCanvas == null || IntelliSenseOverlay == null || CodeEditor == null) return;

            Point visualPos;
            try
            {
                visualPos = CodeEditor.TextArea.TextView.GetVisualPosition(CodeEditor.TextArea.Caret.Position, VisualYPosition.LineBottom);
            }
            catch
            {
                var loc = CodeEditor.Document.GetLocation(CodeEditor.CaretOffset);
                double lh = CodeEditor.FontSize * 1.5;
                visualPos = new Point(45 + Math.Max(0, loc.Column - 1) * (CodeEditor.FontSize * 0.65), loc.Line * lh);
            }

            Point canvasPoint = visualPos;
            if (CodeEditor.TextArea.TextView != null)
            {
                var relative = CodeEditor.TextArea.TextView.TranslatePoint(visualPos, CompletionOverlayCanvas);
                if (relative.HasValue)
                {
                    canvasPoint = relative.Value;
                }
            }

            double lineHeight = (CodeEditor.TextArea.TextView?.DefaultLineHeight ?? 0) > 0
                ? CodeEditor.TextArea.TextView!.DefaultLineHeight
                : (CodeEditor.FontSize * 1.5);

            IntelliSenseOverlay.PositionAt(canvasPoint, lineHeight, CompletionOverlayCanvas.Bounds.Size);
        }
        catch
        {
        }
    }

    public void OnIntelliSenseItemCommitted(object? sender, SqlCompletionData selectedItem)
    {
        try
        {
            if (CodeEditor?.Document == null || selectedItem == null) return;

            int caretOffset = CodeEditor.CaretOffset;
            string currentWord = GetWordBeforeOffset(caretOffset);
            int startOffset = Math.Max(0, caretOffset - currentWord.Length);

            // Keywords get a clean trailing space for smooth typing flow (e.g. "SELECT ")
            string textToInsert = selectedItem.IsSnippet
                ? selectedItem.InsertionText
                : (selectedItem.InsertionText + " ");

            CodeEditor.Document.Replace(startOffset, currentWord.Length, textToInsert);
            CodeEditor.CaretOffset = startOffset + textToInsert.Length;

            CodeEditor.TextArea.Focus();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OnIntelliSenseItemCommitted Exception]: {ex}");
        }
    }

    public void UpdateAndShowCompletion(string currentWord, bool showAll = false)
    {
        ShowIntelliSense(currentWord, showAll);
    }

    public void CommitSelectedCompletion()
    {
        IntelliSenseOverlay?.CommitSelected();
    }

    public void HideCompletion()
    {
        IntelliSenseOverlay?.Hide();
    }

    public string GetWordBeforeOffset(int offset)
    {
        if (CodeEditor?.Document == null || offset <= 0) return string.Empty;

        int maxOffset = Math.Min(offset, CodeEditor.Document.TextLength);
        int start = maxOffset;
        while (start > 0)
        {
            char c = CodeEditor.Document.GetCharAt(start - 1);
            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                break;
            }
            start--;
        }

        return CodeEditor.Document.GetText(start, maxOffset - start);
    }

    #endregion
}
