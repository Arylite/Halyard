using System.Collections.Specialized;
using Halyard.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Halyard.Views;

public sealed partial class LogsPage : Page
{
    public LogsPage()
    {
        ViewModel = App.GetRequiredService<LogsViewModel>();
        InitializeComponent();
    }

    public LogsViewModel ViewModel { get; }

    // Auto-scroll is pure view behaviour, so it lives here rather than in the view model.
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Entries.CollectionChanged += OnEntriesChanged;
        ScrollToEnd();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) =>
        ViewModel.Entries.CollectionChanged -= OnEntriesChanged;

    private bool _scrollPending;

    /// <summary>
    /// One scroll per burst, not per entry: a filter change re-adds up to 5,000 entries, and scrolling
    /// after each of them froze the page.
    /// </summary>
    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && !_scrollPending)
        {
            _scrollPending = DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                _scrollPending = false;
                ScrollToEnd();
            });
        }
    }

    private void ScrollToEnd()
    {
        if (ViewModel.AutoScroll && ViewModel.Entries.Count > 0)
        {
            EntriesList.ScrollIntoView(ViewModel.Entries[^1]);
        }
    }
}
