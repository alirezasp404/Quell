using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Quell.ViewModels;

namespace Quell.Components;

public partial class TerminalComponent : UserControl
{
    private TerminalViewModel? _viewModel;
    private readonly Dictionary<string, TerminalSessionView> _sessionViews = new();

    public TerminalComponent()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.Sessions.CollectionChanged -= OnSessionsCollectionChanged;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.RequestFocus -= OnRequestFocus;
        }

        _viewModel = DataContext as TerminalViewModel;

        ClearAllSessionViews();

        if (_viewModel != null)
        {
            _viewModel.Sessions.CollectionChanged += OnSessionsCollectionChanged;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.RequestFocus += OnRequestFocus;

            foreach (var session in _viewModel.Sessions)
            {
                AddSessionView(session);
            }

            UpdateActiveSessionView();
        }
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        UpdateActiveSessionView();
    }

    private void OnSessionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            ClearAllSessionViews();
            if (_viewModel != null)
            {
                foreach (var session in _viewModel.Sessions)
                {
                    AddSessionView(session);
                }
            }
        }
        else
        {
            if (e.OldItems != null)
            {
                foreach (TerminalSessionViewModel session in e.OldItems)
                {
                    RemoveSessionView(session);
                }
            }

            if (e.NewItems != null)
            {
                foreach (TerminalSessionViewModel session in e.NewItems)
                {
                    AddSessionView(session);
                }
            }
        }

        UpdateActiveSessionView();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TerminalViewModel.ActiveSession))
        {
            UpdateActiveSessionView();
        }
    }

    private void OnRequestFocus()
    {
        if (_viewModel?.ActiveSession != null &&
            _sessionViews.TryGetValue(_viewModel.ActiveSession.Id, out var activeView))
        {
            activeView.FocusTerminal();
        }
    }

    private void AddSessionView(TerminalSessionViewModel session)
    {
        if (_sessionViews.ContainsKey(session.Id)) return;

        var view = new TerminalSessionView
        {
            DataContext = session,
            IsVisible = (session == _viewModel?.ActiveSession)
        };

        _sessionViews[session.Id] = view;
        SessionsHost.Children.Add(view);
    }

    private void RemoveSessionView(TerminalSessionViewModel session)
    {
        if (_sessionViews.TryGetValue(session.Id, out var view))
        {
            SessionsHost.Children.Remove(view);
            _sessionViews.Remove(session.Id);
            view.DisposeSession();
        }
    }

    private void ClearAllSessionViews()
    {
        foreach (var view in _sessionViews.Values)
        {
            view.DisposeSession();
        }
        _sessionViews.Clear();
        SessionsHost?.Children.Clear();
    }

    private void UpdateActiveSessionView()
    {
        if (_viewModel == null) return;

        TerminalSessionView? currentActiveView = null;

        foreach (var kvp in _sessionViews)
        {
            var isCurrent = (kvp.Key == _viewModel.ActiveSession?.Id);
            kvp.Value.IsVisible = isCurrent;
            if (isCurrent)
            {
                currentActiveView = kvp.Value;
            }
        }

        if (currentActiveView != null && IsLoaded)
        {
            currentActiveView.FocusTerminal();
        }
    }
}
