<div align="center">

# WwTool

A Wuthering Waves toolbox · Account details, Resonators and Convene statistics

![Version 1.2.1](https://img.shields.io/badge/version-1.2.1-blue)
![Windows x64](https://img.shields.io/badge/platform-Windows%2010%2B%20x64-0078D4)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
[![MIT License](https://img.shields.io/badge/license-MIT-green)](../../LICENSE.txt)

[简体中文](../../README.md) | English | [日本語](README_ja.md)

[Features](#features) · [Quick start](#quick-start) · [Usage](#usage) · [Settings](#settings) · [Data & privacy](#data) · [Limitations](#limitations) · [Preview](#preview) · [Docs](#docs) · [Changelog](../../CHANGELOG.md)

</div>

WwTool is a Windows desktop tool for viewing Wuthering Waves account details and organizing Convene history. It supports multiple accounts and UIDs, local records, and Simplified Chinese, English and Japanese interfaces.

<a id="features"></a>
## Features

| Feature | Details |
| --- | --- |
| Account overview | Nickname, UID, level, SOL3 Phase, activity, weekly boss rewards and Pioneer Podcast details |
| Resonator details | Owned Resonators, activated Resonance Chains and equipped weapons; a hover card with artwork and available attributes from the guide service |
| Convene statistics | Automatic or manual history URL import, pulls by banner, current pity, average pulls per 5-star and overall summaries |
| Charts | Banner comparisons, rarity distribution, pull timelines and activity heatmaps, with filters |
| Exploration & motorcycle | Collection counts, chest and Tidal Heritage records returned by the API, plus motorcycle, cosmetic and music unlocks |
| Game catalog | Separate Resonator, weapon, motorcycle and album catalogs synced from this repository; images cached on demand |
| Appearance | Multiple themes, accent colors, frosted glass, opacity and reduced motion options |

Account details currently target the global service and require email/password login. Convene history supports both the Chinese and global services and can be used independently.

<a id="quick-start"></a>
## Quick start

1. Use Windows 10 or later, x64, and install [.NET Desktop Runtime 10 for Windows x64](https://dotnet.microsoft.com/en-us/download/dotnet/10.0). Select the desktop runtime on the download page.
2. Download `WwTool.zip` from [Releases](https://github.com/conFess233/WwTool/releases) and extract the entire archive to a writable folder.
3. Run `WwTool.exe`. Keep the accompanying resources and local data folders with the executable.
4. Add an account or import Convene history as described below. See the [usage guide](Help_en.md) for detailed steps.

<details>
<summary>Build from source</summary>

Install the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), then run on Windows:

```powershell
git clone https://github.com/conFess233/WwTool.git
cd WwTool
dotnet build WwTool/WwTool.csproj -c Release
```

Run `WwTool/bin/Release/net10.0-windows/WwTool.exe`. You can also open `WwTool.slnx` in a development environment that supports .NET 10.

</details>

<a id="usage"></a>
## Usage

### View accounts and Resonators

1. Add an account on the home page using your email and password. Complete verification in the browser if prompted.
2. Select the relevant UID and fetch cloud data, then open the account, Resonator, exploration or motorcycle page.
3. Hover over a Resonator avatar for **0.5 seconds** to fade in its card. Artwork appears on the left with the name below; available attributes and the equipped weapon appear on the right, with the acquisition time in the lower-right corner when known.

Keyboard focus also opens the card; press `Esc` to close it. The card uses the latest locally synced snapshot. Hovering does not fetch fresh character data; fetch cloud data again to update it.

### Import Convene history

1. For automatic import, select the game folder in Settings and open Convene history once in the game.
2. Read the history URL from the game log on the statistics page, or paste it manually and select the server.
3. Fetch the records and view the overview and charts. Later, select a UID to load its saved local records.

Repeated imports do not add the same record again. Ten-pulls sharing a timestamp retain their source order, and legitimate duplicate results are preserved. Average pulls per 5-star stops at the latest 5-star; current pity is shown separately. The average is left empty when no 5-star is recorded.

<a id="settings"></a>
## Settings

| Setting | Purpose |
| --- | --- |
| Game path | Read history URLs automatically; select the launcher folder containing `Wuthering Waves Game` or the game folder |
| Language & appearance | Switch languages, themes, accent colors, frosted glass, opacity and motion |
| Game catalog sync | Check all four categories, inspect versions, counts, results and last successful sync times, or cancel syncing |
| Image cache | Clear cached images; they will be downloaded again as needed |

After startup, each catalog category is checked when 24 hours have elapsed since its last successful sync. Syncing uses catalog files from this repository's default branch and retains existing data on failure. Catalog versions such as `3.7.0` are maintained separately from the tool version, `1.2.1`.

<a id="data"></a>
## Data & privacy

- Configuration, account snapshots and Convene records stay in the application directory. Close the tool and back up local data before updating; avoid overwriting saved data with archive contents.
- Saved login credentials use Windows DPAPI encryption for the current Windows user. Moving to another computer or Windows user may require logging in again. This does not mean the entire database is encrypted.
- Login and cloud data retrieval contact the relevant game services. Catalog syncing and missing icon downloads contact GitHub; half-body artwork uses official image URLs saved in the guide snapshot.
- History URLs may contain authentication information. Remove passwords, tokens, complete history URLs and personal account details before sharing diagnostics.
- Images are cached in `Local/Cache/Images`; the default log folder is `Local/Logs`. Clearing the image cache does not delete Convene records.

<a id="limitations"></a>
## Limitations

- **Partial data:** only values returned by the API are displayed. Complete character stats and Echo substats are not guaranteed; unknown values are not treated as zero. Exploration counts are not full map completion percentages.
- **Unknown acquisition time:** a time inferred from imported pulls is shown only when determinable. Missing history or inapplicable characters are shown as unknown.
- **Limited history:** only records currently provided by the server can be imported. A local archive cannot recover records that were never imported and are no longer available.
- **Sync failures:** network issues, login expiry or API changes can prevent retrieval. Failed catalog and image updates retain valid previous data; retry later.
- **Missing images or names:** cached images, avatars or placeholders provide fallbacks. Unfinished catalog entries without usable names are preserved in the source data and temporarily omitted from display.

See the [usage guide](Help_en.md) for more instructions and troubleshooting.

<a id="preview"></a>
## Preview

These screenshots are from existing versions. Layout and content may differ from the latest release.

![Home](Img/1.png)
![Convene statistics](Img/2.png)

<details>
<summary>More screenshots</summary>

![Page preview 3](Img/3.png)
![Page preview 4](Img/4.png)
![Page preview 5](Img/5.png)
![Page preview 6](Img/6.png)

</details>

<a id="docs"></a>
## Documentation & feedback

- [Usage guide](Help_en.md)
- [Changelog (Chinese)](../../CHANGELOG.md)
- [Game API notes](API/WW_API.md) · [Guide API notes](API/Guide.md)
- [Resonator resources](Resource/Characters.md) · [Weapon resources](Resource/Weapons.md) · [Motorcycle resources](Resource/Motorcycle.md)
- [Catalog sync rules](Resource/CatalogSync.md)

Report problems through [Issues](https://github.com/conFess233/WwTool/issues), including the tool version, system environment, steps to reproduce and sanitized error logs. API notes are project reference material and may become outdated as services change. Technical documents are primarily in Chinese.

## License

Project code is licensed under the [MIT License](../../LICENSE.txt). WwTool is an unofficial tool and is not affiliated with the game's publisher. Game names, images and other assets belong to their respective rights holders.
