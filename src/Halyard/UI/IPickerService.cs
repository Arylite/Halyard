namespace Halyard.UI;

public interface IPickerService
{
    Task<string?> PickExecutableAsync();

    Task<string?> PickFolderAsync();

    /// <param name="extension">Including the dot, e.g. ".zip".</param>
    Task<string?> PickSaveFileAsync(string suggestedName, string fileTypeName, string extension);
}
