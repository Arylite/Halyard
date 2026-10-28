using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Halyard.UI;

namespace Halyard.Platform;

/// <summary>The progress bar drawn over the app's taskbar button (ITaskbarList3).</summary>
public sealed partial class TaskbarProgress(IWindowContext window)
{
    private ITaskbarList3? _taskbar;

    public void Show(double fraction, bool paused)
    {
        var taskbar = GetTaskbar();
        taskbar.SetProgressState(window.Handle, paused ? TaskbarState.Paused : TaskbarState.Normal);
        taskbar.SetProgressValue(window.Handle, (ulong)(Math.Clamp(fraction, 0, 1) * 1000), 1000);
    }

    public void ShowIndeterminate() => GetTaskbar().SetProgressState(window.Handle, TaskbarState.Indeterminate);

    public void Clear() => GetTaskbar().SetProgressState(window.Handle, TaskbarState.None);

    private ITaskbarList3 GetTaskbar()
    {
        if (_taskbar is null)
        {
            var clsid = new Guid("56FDF344-FD6D-11d0-958A-006097C9A090");
            var iid = typeof(ITaskbarList3).GUID;
            Marshal.ThrowExceptionForHR(CoCreateInstance(clsid, 0, ClassContextInprocServer, iid, out var pointer));

            try
            {
                var taskbar = (ITaskbarList3)new StrategyBasedComWrappers().GetOrCreateObjectForComInstance(pointer, CreateObjectFlags.None);
                taskbar.HrInit();
                _taskbar = taskbar;
            }
            finally
            {
                Marshal.Release(pointer);
            }
        }

        return _taskbar;
    }

    private const uint ClassContextInprocServer = 1;

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(in Guid clsid, nint outer, uint context, in Guid iid, out nint instance);

    internal enum TaskbarState
    {
        None = 0,
        Indeterminate = 0x1,
        Normal = 0x2,
        Paused = 0x8,
    }

    [GeneratedComInterface]
    [Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
    internal partial interface ITaskbarList3
    {
        void HrInit();

        void AddTab(nint hwnd);

        void DeleteTab(nint hwnd);

        void ActivateTab(nint hwnd);

        void SetActiveAlt(nint hwnd);

        void MarkFullscreenWindow(nint hwnd, int fullscreen);

        void SetProgressValue(nint hwnd, ulong completed, ulong total);

        void SetProgressState(nint hwnd, TaskbarState state);
    }
}
