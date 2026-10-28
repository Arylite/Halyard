using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Halyard.Core.Diagnostics;
using Halyard.Core.Logging;
using Halyard.Core.Storage;
using Halyard.Models.Logging;
using Halyard.UI;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;

namespace Halyard.ViewModels;

public sealed partial class LogsViewModel : ObservableObject
{
    private static readonly LogLevel[] Levels =
    [
        LogLevel.Trace,
        LogLevel.Debug,
        LogLevel.Information,
        LogLevel.Warning,
        LogLevel.Error,
        LogLevel.Critical,
    ];

    private readonly InMemoryLogStore _store;
    private readonly IClipboardService _clipboard;
    private readonly IShellService _shell;
    private readonly IPickerService _pickers;
    private readonly IDiagnosticsExporter _diagnostics;
    private readonly INotificationService _notifications;
    private readonly AppPaths _paths;

    public LogsViewModel(
        InMemoryLogStore store,
        IUiDispatcher dispatcher,
        IClipboardService clipboard,
        IShellService shell,
        IPickerService pickers,
        IDiagnosticsExporter diagnostics,
        INotificationService notifications,
        AppPaths paths)
    {
        _store = store;
        _clipboard = clipboard;
        _shell = shell;
        _pickers = pickers;
        _diagnostics = diagnostics;
        _notifications = notifications;
        _paths = paths;

        SearchText = string.Empty;
        AutoScroll = true;
        MinimumLevelIndex = Array.IndexOf(Levels, LogLevel.Information);

        store.EntryWritten += (_, entry) => dispatcher.Post(() => Append(entry));
        store.Cleared += (_, _) => dispatcher.Post(Entries.Clear);
    }

    public IReadOnlyList<string> LevelNames { get; } = [.. Levels.Select(level => level.ToString())];

    public ObservableCollection<LogEntryItem> Entries { get; } = [];

    [ObservableProperty]
    public partial int MinimumLevelIndex { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; }

    [ObservableProperty]
    public partial bool AutoScroll { get; set; }

    partial void OnMinimumLevelIndexChanged(int value) => Rebuild();

    partial void OnSearchTextChanged(string value) => Rebuild();

    [RelayCommand]
    private void Clear() => _store.Clear();

    [RelayCommand]
    private void CopyVisible() =>
        _clipboard.SetText(string.Join(Environment.NewLine, Entries.Select(item => item.FullText)));

    [RelayCommand]
    private void OpenLogsFolder() => _shell.OpenFolder(_paths.LogsDirectory);

    /// <summary>One zip to attach to a bug report: logs, settings, system info, no tokens.</summary>
    [RelayCommand]
    private async Task ExportReportAsync()
    {
        var path = await _pickers.PickSaveFileAsync($"halyard-report-{DateTime.Now:yyyyMMdd-HHmm}", "Zip archive", ".zip");
        if (path is null)
        {
            return;
        }

        try
        {
            await _diagnostics.ExportAsync(path, CancellationToken.None);
            _notifications.Show("Report exported", "Attach the zip to your bug report. It contains no passwords or tokens.", InfoBarSeverity.Success);
            _shell.OpenFolder(Path.GetDirectoryName(path)!);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _notifications.Show("Couldn't export the report", ex.Message, InfoBarSeverity.Error);
        }
    }

    private void Rebuild()
    {
        Entries.Clear();
        foreach (var entry in _store.Snapshot().Where(Matches))
        {
            Entries.Add(new LogEntryItem(entry));
        }
    }

    private void Append(LogEntry entry)
    {
        if (!Matches(entry))
        {
            return;
        }

        Entries.Add(new LogEntryItem(entry));
        if (Entries.Count > InMemoryLogStore.Capacity)
        {
            Entries.RemoveAt(0);
        }
    }

    private bool Matches(LogEntry entry)
    {
        var minimumLevel = Levels[Math.Clamp(MinimumLevelIndex, 0, Levels.Length - 1)];
        if (entry.Level < minimumLevel)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(SearchText)
            || entry.Message.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || entry.Category.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }
}
