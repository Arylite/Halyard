using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Halyard.UI;

/// <summary>Shell file pickers. Unpackaged apps must parent them to a window handle explicitly.</summary>
public sealed class PickerService(IWindowContext window) : IPickerService
{
    public async Task<string?> PickExecutableAsync()
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder,
            FileTypeFilter = { ".exe" },
        };
        InitializeWithWindow.Initialize(picker, window.Handle);

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    public async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder,
            FileTypeFilter = { "*" },
        };
        InitializeWithWindow.Initialize(picker, window.Handle);

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    public async Task<string?> PickSaveFileAsync(string suggestedName, string fileTypeName, string extension)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.Desktop,
            SuggestedFileName = suggestedName,
        };
        picker.FileTypeChoices.Add(fileTypeName, [extension]);
        InitializeWithWindow.Initialize(picker, window.Handle);

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }
}
