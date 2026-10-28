using CommunityToolkit.Mvvm.ComponentModel;
using Halyard.Formatting;
using Halyard.Models.Installs;

namespace Halyard.ViewModels;

public sealed partial class InstallOptionViewModel : ObservableObject
{
    public InstallOptionViewModel(InstallOption option, bool isSelected)
    {
        Option = option;
        IsSelected = isSelected;
    }

    public InstallOption Option { get; }

    public string Name => InstallTagName.Humanize(Option.Tag);

    public string Tag => Option.Tag;

    public string SizeText => $"+{ByteSize.Format(Option.ExtraInstallSize)}";

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
