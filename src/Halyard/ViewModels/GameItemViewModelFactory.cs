using Halyard.Core.Downloads;
using Halyard.Core.Installs;
using Halyard.Core.Launching;
using Halyard.Core.Library;
using Halyard.Models.Library;
using Halyard.UI;

namespace Halyard.ViewModels;

public sealed class GameItemViewModelFactory(
    IGameLauncher launcher,
    ILibraryService library,
    IDownloadQueue queue,
    IGameUninstaller uninstaller,
    IDialogService dialogs,
    IShellService shell,
    IPickerService pickers,
    INotificationService notifications,
    INavigationService navigation,
    IUiDispatcher dispatcher)
{
    public GameItemViewModel Create(Game game) =>
        new(game, launcher, library, queue, uninstaller, dialogs, shell, pickers, notifications, navigation, dispatcher);
}
