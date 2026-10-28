using Halyard.Models.Library;
using Halyard.Services.Library;

namespace Halyard.Services.Tests.Library;

public sealed class LibraryMergerTests
{
    private static readonly Game LauncherGame = new()
    {
        Id = Game.EpicId("Fortnite"),
        Title = "Fortnite",
        Installation = new GameInstallation
        {
            InstallDirectory = @"C:\Epic\Fortnite",
            Executable = "Fortnite.exe",
            Source = InstallationSource.EpicLauncher,
        },
    };

    [Fact]
    public void Merge_ForgetsALauncherInstallTheLauncherNoLongerReports()
    {
        var merged = LibraryMerger.Merge([LauncherGame], [], launcherScanned: true);

        Assert.Null(Assert.Single(merged).Installation);
    }

    [Fact]
    public void Merge_DropsUninstalledGamesTheAccountNoLongerOwns()
    {
        var owned = new Game { Id = Game.EpicId("Owned"), Title = "Owned", EpicAppName = "Owned" };
        var previousAccount = new Game { Id = Game.EpicId("Other"), Title = "Other", EpicAppName = "Other" };
        var manual = Game.FromExecutable(@"C:\Games\Tool\tool.exe");

        var merged = LibraryMerger.Merge([owned, previousAccount, manual, LauncherGame], [owned, LauncherGame], ownedScanned: true);

        Assert.Equal(new[] { owned.Id, manual.Id, LauncherGame.Id }.Order(), merged.Select(game => game.Id).Order());
    }

    [Fact]
    public void Merge_KeepsEveryGameWhenTheAccountCouldNotBeListed()
    {
        var previous = new Game { Id = Game.EpicId("Other"), Title = "Other", EpicAppName = "Other" };

        Assert.Single(LibraryMerger.Merge([previous], [], ownedScanned: false));
    }

    [Fact]
    public void Merge_KeepsLauncherInstallsWhenTheLauncherCouldNotBeRead()
    {
        var merged = LibraryMerger.Merge([LauncherGame], [], launcherScanned: false);

        Assert.Equal(LauncherGame.Installation, Assert.Single(merged).Installation);
    }
}
