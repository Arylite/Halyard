<img src="assets/logo.png" width="96" alt="Halyard logo" />

# Halyard

An open-source, Windows 11-native launcher for your Epic Games library, written in C# and WinUI 3.

> Halyard is not affiliated with or endorsed by Epic Games.

> **Beta.** Halyard is in public beta: expect rough edges, and keep the official launcher around for
> anything critical. Bug reports are very welcome (see [Beta testing](#beta-testing)).

## Beta testing

1. Download `Halyard-beta-Setup.exe` from the [latest release](https://github.com/Arylite/Halyard/releases).
2. Run it. Halyard installs for your user only (no administrator rights). The installer is not
   code-signed yet (this is a free project), so Windows SmartScreen may warn: choose
   **More info > Run anyway**. Some antivirus tools are wary of unsigned apps that download and start
   programs; the installer is built by GitHub Actions straight from this repository, and you can
   build it yourself (see [Build, test, run](#build-test-run)).
3. Halyard updates itself. To follow pre-release builds, turn on **Settings > Receive beta updates**.

Found a bug? Open **Logs > Export report**, then [file an issue](https://github.com/Arylite/Halyard/issues/new/choose)
and attach the archive. It never contains your sign-in tokens or cookies, but logs and settings can
contain folder paths: have a look before sharing.

Security issues (anything touching tokens, sessions or downloaded files): please report them
privately, see [SECURITY.md](SECURITY.md).

## Features

- **Library**: every game your Epic account owns, with cover art, search and filters, plus games
  installed by the official launcher and any executable you add yourself. Served instantly from a
  local cache, refreshed in the background.
- **Installs from Epic's CDN** with **selective content**: the install dialog lists the build's
  optional content (Epic install tags) with its real size, so you only download what you pick.
- **Updates** download only the files that changed; **Verify & repair** re-hashes everything and
  fixes what's damaged; **Uninstall** removes the files.
- **Download queue**: ordered, pausable, resumable across restarts (a journal tracks finished files).
- **Performance**: parallel chunk downloads (configurable), a bounded memory cache with pooled
  buffers, chunks and files verified with SHA-1 as they stream, a global speed limit, and files
  pre-allocated to stay contiguous.
- **Sign-in in an embedded browser**: no codes to copy. Tokens are encrypted with Windows DPAPI.
- **Logs**: live, filterable viewer; daily files in `%LOCALAPPDATA%\Halyard\logs`.
- **Experimental multi-CDN**: spreads chunks across every CDN by measured speed and re-requests slow
  chunks from another CDN; live per-CDN stats in the download details.
- **Windows integration**: tray icon (close to tray, pause/resume all), launch at startup, single
  instance, notifications, taskbar progress.
- **Quality of life**: play time, per-game launch arguments and offline mode, automatic game updates,
  downloads paused while you play, import an existing install folder, library sort and filters,
  Ctrl+K quick search in the title bar.
- **Self-update** of Halyard itself (Velopack + GitHub Releases).
- **Clean look**: flat surfaces, hairline borders and subtle motion, light and dark themes, adaptive
  layout from wide to narrow windows.

## Use at your own risk

Halyard talks to Epic's servers the way the official launcher does, including presenting itself as
the launcher's OAuth client (see below). Epic doesn't support third-party launchers, and nothing
guarantees it never will act on accounts that use one. Projects like Legendary and Heroic have worked
this way for years, but you are the one who decides whether to use it with your account.

## Data and privacy

- No telemetry, no analytics: Halyard only talks to Epic (sign-in, library, downloads) and to GitHub
  (its own updates).
- Everything lives in `%LOCALAPPDATA%\Halyard`: settings, library cache, download queue, logs, and the
  sign-in browser profile. Your Epic tokens are in `session.bin`, encrypted with Windows DPAPI (only
  your Windows account can read them).
- Each game folder gets a small `.halyard` folder (its manifest and resume journal).
- **Logs > Export report** builds a zip for bug reports. It never includes tokens or cookies, but it
  does contain folder paths: check it before sharing.
- To erase everything, uninstall Halyard (Windows Settings > Apps), or sign out and delete
  `%LOCALAPPDATA%\Halyard`.

## How sign-in works

You sign in on epicgames.com inside Halyard. That web session mints an *exchange code*, which
Halyard trades for launcher tokens. Epic's library and download APIs only accept tokens issued to the
Epic Games Launcher's public OAuth client, so, like Legendary and Heroic, Halyard presents itself as
that client (`EpicLauncherClient.cs`). Games are launched with their own exchange code, exactly as
the official launcher does.

## Layout

```
src/
  Halyard.Models     Plain data: Game, BuildManifest, InstallRequest, EpicSession, settings...
  Halyard.Core       Contracts (interfaces) + cross-cutting infrastructure: logging, JSON storage, paths
  Halyard.Services   Implementations: Epic auth and APIs, manifest parser, install pipeline, queue...
  Halyard            WinUI 3 app: views, view models (CommunityToolkit.Mvvm), UI services
tests/
  Halyard.Services.Tests   Manifest/chunk formats, planner, and the full install pipeline on a fake CDN
```

Dependencies only point downward: `Halyard → Services → Core → Models`.

## Build, test, run

Requirements: Windows 10 1809+ (Windows 11 recommended), .NET 10 SDK, the WebView2 runtime (built
into Windows 11).

```bash
dotnet build Halyard.slnx
dotnet test --project tests/Halyard.Services.Tests
dotnet run --project src/Halyard
```

The app is unpackaged and self-contained (no MSIX, no Windows App SDK runtime installer).

### VS Code

Install the recommended extensions (C# Dev Kit), then build once with **Ctrl+Shift+B**.
WinUI's XAML compiler doesn't run during the editor's design-time builds; `Directory.Build.targets`
feeds those builds the generated code from your last real build, so after that first build the
`InitializeComponent` / `x:Name` errors disappear. **F5** builds and launches with the debugger.

## Releases

CI builds and tests every push (`.github/workflows/ci.yml`). Pushing a tag runs `release.yml`, which
publishes a self-contained build, packs it with Velopack (installer + delta updates) and uploads it
to GitHub Releases, where installed copies pick it up automatically:

- `v0.2.0` is a stable release.
- `v0.2.0-beta.1` (any tag with a `-` suffix) is a pre-release on the `beta` channel.

`tools/Publish-Release.ps1` does the same build locally. Remember to update `CHANGELOG.md`.

The logo is drawn by `tools/New-AppIcon.ps1`.

## Known limits

- **No cloud saves.** Halyard doesn't sync Epic cloud saves. Games that save on their own servers are
  fine, but for the others, progress made through Halyard stays on this PC, and progress from other
  PCs doesn't come down. Keep this in mind before switching from the official launcher.
- DLC and add-ons aren't listed or installable on their own.
- Builds still using Epic's legacy JSON manifest format can't be installed yet (binary manifests,
  which current games use, are supported). Encrypted manifests and chunks aren't supported either.
- Features of the official launcher itself (friends, achievements view, Epic overlay) aren't provided.
- Updating or repairing a game installed by the official launcher has to be done there.

## License

MIT
