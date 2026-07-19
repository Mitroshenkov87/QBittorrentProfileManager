# qBittorrent Profile Manager

**Free and Open Source Software (FOSS)** — MIT License.

A Windows utility that manages **named qBittorrent profile snapshots**
(`%LOCALAPPDATA%\qBittorrent` + `%APPDATA%\qBittorrent`).

## Download (program)

Pre-built Windows x64 package:

- **[Releases](../../releases)** — latest `QBittorrentProfileManager-*-win-x64.zip`
- Or from the repo: [`dist/QBittorrentProfileManager-v1.0.0-win-x64.zip`](dist/QBittorrentProfileManager-v1.0.0-win-x64.zip)

Unpack and run `QBittorrentProfileManager.exe`.

Requires [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (x64).

## Features

- Multiple named profiles (e.g. All, Entertainment, Software, XXX)
- First-run wizard (detect live folders, choose store location)
- Activate / save live / create / delete profiles
- Optional **Recommended settings** menu (separate; applies careful `qBittorrent.ini` presets)
- Languages: **English, Russian, Belarusian, Ukrainian, Kazakh**
- App icon, header banner, toolbar icons
- ZIP backup before risky operations
- No telemetry

## Requirements

- Windows x64
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- qBittorrent installed (run it **at least once** so it creates config files)

## Recommended workflow

1. Install and **start qBittorrent once**, then quit.
2. Run Profile Manager → complete the first-run wizard.
3. Create profiles from that clean live state (or import live into a profile).
4. Use **Recommended settings** only on clean post-first-run configs (see in-app warning).
5. Switch profiles with **Activate** (qBittorrent must be closed).

## Profile storage

| Mode | Location |
|------|----------|
| Portable | Next to the executable: `profiles\{id}\local` + `roaming` |
| AppData | `%LOCALAPPDATA%\qBittorrent\Profiles\{id}\` and `%APPDATA%\qBittorrent\Profiles\{id}\` (excluded from live mirror) |
| Custom | User-selected folder |

Config and log live next to the executable: `config.json`, `app.log`.

## Build from source

```bat
dotnet publish src\QBittorrentProfileManager.csproj -c Release -r win-x64 --self-contained false -o publish
```

Output: `publish\QBittorrentProfileManager.exe` (+ `Assets`, `Localization`).

## Project layout

```
src/                 C# source (WinForms, .NET 8)
  Assets/            Icons, header, toolbar graphics
  Forms/             Main, setup wizard, presets
  Localization/      en, ru, be, uk, kk
  Services/          profiles, copy, backup, presets, UI assets
dist/                Packaged Windows release zip
LICENSE              MIT
```

## Disclaimer

Presets and file mirroring are **best-effort**. The program cannot guarantee a correct
qBittorrent configuration on arbitrary or heavily customized trees. Clean files after
qBittorrent’s first launch give better odds. Always close qBittorrent before switching.

## License

MIT — see [LICENSE](LICENSE).
