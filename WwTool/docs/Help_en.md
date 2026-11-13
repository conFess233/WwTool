# WwTool User Guide

[简体中文](Help.md) | English | [日本語](Help_ja.md) · [Back to README](README_en.md)

This guide is organized by task. Each section gives the steps first, then what to do if something goes wrong. Button names follow the English interface.

## Contents

1. [First run](#first-run)
2. [Accounts](#accounts)
3. [Home](#home)
4. [Character, Exploration and Vehicle data](#data-pages)
5. [Convene records](#pulls)
6. [Settings reference](#settings)
7. [Data, backups and updates](#data)
8. [FAQ](#faq)

---

<a id="first-run"></a>
## 1. First run

### 1.1 Choose an archive and unzip it

Pick an archive on [Releases](https://github.com/conFess233/WwTool/releases). The differences are listed in [README · Download](README_en.md#download). If unsure, choose `single-runtime`; it runs right after unzipping.

1. Unzip the archive somewhere you can write to, such as `D:\Tools`; this creates the `D:\Tools\WwTool` folder. Don't put it under `C:\Program Files`, and don't take out `WwTool.exe` on its own.
2. Keep the `portable` file that comes with it. With it, settings and data are stored in this folder. Without it, WwTool uses `%LocalAppData%\WwTool` instead, and your earlier data seems to disappear.
3. Run `WwTool.exe`. If you are told .NET is missing, either install the [.NET Desktop Runtime 10 (x64)](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) or switch to an archive with `runtime` in its name.

### 1.2 Set the game installation directory

Reading the Convene link automatically, **Launch game**, and the Screenshots / Game folder / Log folder quick actions all need the game directory.

1. Click the gear at the right end of the top navigation to open **Settings**.
2. Under **General Game Settings → Game Installation Directory**, click **Select Path**.
3. Choose either the launcher folder (for example `D:\Wuthering Waves`) or the game folder inside it (`D:\Wuthering Waves\Wuthering Waves Game`). Both work.

Changes are saved automatically about one second later; **Save settings** at the bottom right saves immediately.

### 1.3 Language, theme and background

All under **Settings → UI Settings**:

- **Software Language**: Simplified Chinese, English or Japanese. Takes effect immediately.
- **Theme / Accent color**: light and dark themes, plus an accent color.
- **Window background**: by default, the game launcher's background image (a pattern is shown if it can't be fetched). You can use your own image instead.
- **Animated background**: with the default background, plays the launcher's background video. It pauses when the window is minimized or loses focus, uses about 150–200 MB of extra memory while playing, and is off by default.
- **Peek at background**: rest the pointer on an empty area (the window margins or a gap between cards) and everything except the background gradually fades so you can see it; move onto any component and it comes back right away. The transparency is adjustable, 80% by default; 0% turns it off.
- **Window glass effect**: needs a restart to take effect. When off, WwTool uses software rendering and less memory.

---

<a id="accounts"></a>
## 2. Accounts

> Account data currently supports the **International** server only. If you only want Convene records, skip to [Convene records](#pulls).

### 2.1 Add an account

1. On Home, click **Add Game Account**. If you already have an account, click the **+** button at the bottom right and choose **Add Account**.
2. Enter your game account's **Email** and **Password**, then click **Login**.
3. If you see "Risk control triggered, opening a web page for security verification…", complete the check in the web page that opens. Login continues automatically.
4. After logging in, WwTool lists the UIDs on this account and fetches their data once.

The authorization code from login is encrypted with Windows DPAPI and stored locally. WwTool does not store your password.

### 2.2 Switch and manage accounts

Click the **account capsule** (avatar and nickname) at the top right to open **Account Data**:

- Each locally saved UID has a card; click **Switch to this account** to switch.
- **+ → Refresh data** at the bottom right fetches the current account's data again.
- **Delete account** also deletes that account's local snapshots, Convene records and login credentials. **This cannot be undone.**

Use **Settings → Default Game Account (UID)** to choose which account is selected at startup.

### 2.3 Expired login

When authorization expires, the page shows "…'s login has expired":

- Click **Log in again** and log in with the same email; the data is refreshed right away.
- If you no longer need the account, click **Remove account**.

---

<a id="home"></a>
## 3. Home

### 3.1 Account info and resources

- At the top: nickname, level and **UID**. The small button after the UID copies the full UID; a green check mark appears briefly when it's copied.
- The row below shows active days, creation date, SOL3 Phase, server and number of Resonators.
- Five resource cards: **Waveplate** (with time until full), **Crystal Solvent**, **Activity**, **Pioneer Podcast** and **Weekly rewards** (reset Monday 04:00). Countdowns update every second.

This data is a snapshot from the last sync. To update it, click **+ → Refresh data** at the bottom right of Home (Account Data, Exploration Data and Vehicle Data have the same button). The **+** button on Home also holds **Refresh Account List** and **Add Account**.

### 3.2 Quick actions

| Button | What it does |
| --- | --- |
| Launch game | Starts the game from the directory set in Settings |
| Screenshots / Game folder / Log folder | Opens that folder in File Explorer |
| Copy gacha link | Reads the Convene link from the game log and copies it to the clipboard |
| Sync game data | Checks for updates to Resonator, weapon, vehicle and music album data now |
| Data folder | Opens the folder where WwTool stores its data |
| Official guide | Opens the Kurobbs guide site in your browser |

### 3.3 Announcements

- The right side shows events, notices and news from the game launcher, with a carousel on top. Click any item to open it in your browser.
- The **China / International** slider next to the title switches the source and is remembered. Until you choose, it follows the current account's server.
- **All / Event / Notice / News** filter the list. **View all on the official site** at the bottom opens the news page for the selected server.
- The last cached announcements are shown first and refreshed in the background. If fetching fails, the old ones stay.

---

<a id="data-pages"></a>
## 4. Character, Exploration and Vehicle data

These pages show the data **last synced to your PC**. To update, click **+ → Refresh data** at the bottom right of the page.

- **Character Data**: owned Resonators (rarity, Resonance Chain) and their equipped weapons, with sorting. Click a card to open its details next to it, with half-body art and attributes from the guide site (the avatar is shown if there is no art); click it again or click elsewhere to close.
- **Exploration Data**: opened chests and Tidal Remnants by tier. These are counts from the API, not the in-game exploration percentage.
- **Vehicle Data**: vehicle level and experience, current equipment, and unlocked skins, stickers, ornaments, frames and in-vehicle music albums.

---

<a id="pulls"></a>
## 5. Convene records

Convene statistics support both the China and International servers and **do not require logging in**.

### 5.1 Get records

1. Start the game, open **Convene**, click **History** and view at least one page. This writes the Convene link to the game log.
2. In WwTool, open **Convene Statistics** and click **Get records**.
3. WwTool first looks for the link in the game log:
   - Found: records for all banners are fetched.
   - Not found: the **Import pull history URL** window opens; see the next section.
4. When done, the records are saved locally and the account matching the link's UID is selected.

Later fetches only add new records. Existing records are never counted twice.

### 5.2 Import the Convene link

In the **Import pull history URL** window (also available via **+ → Import URL** at the bottom right):

- Click **Read from game log** to search again, or
- **Paste** the link into the box. A whole log line also works; WwTool extracts the link.
- **Server** (China / International) is usually detected from the link; choose it yourself if not.

You can also **drag** a Convene link or the `Client.log` file onto the Convene Statistics page. When you drop it, the link is imported and records are fetched.

To find the link by hand: open `Client\Saved\Logs\Client.log` in the game folder with a text editor, search for `gacha/record/query`, and copy the whole URL that contains it.

> [!WARNING]
> The Convene link contains your account's authentication data. Don't share it or post it publicly.

### 5.3 Read the statistics

Tabs at the top switch between **Overview**, each banner and **Other banners**.

- **Overview**: pulls, 5-stars, average pulls per 5-star, 50/50 win rate and Astrite cost across all banners, plus the latest 5-star and current pity for each banner. The 50/50 win rate only counts the character event banner.
- **A single banner**: every record on that banner, current pity and how many pulls remain until the 80-pull guarantee.
- **Filters and view**: filter by time range, show only 5-stars, or switch between **List** and **Compact**. Compact view shows each 5-star as an avatar tile.
- **Right-click a 5-star record**: Copy this record, View resonator details, Show in detailed charts, or Export as share image.
- **Detailed charts**: 5-star pity distribution, share of pulls by banner, pulls spent on each 5-star, rarity breakdown with 50/50 win rate, and a daily pull heatmap.

### 5.4 Use local data only

- To view saved records without going online: **+ → Load Local Data** at the bottom right.
- With **Settings → Load local data automatically** on, local records are loaded the first time you open Convene Statistics.

### 5.5 Clear Convene records

**Hold to clear pull data** at the bottom of **Settings**: hold the button until it finishes to delete all Convene records of the **current account**. Other accounts are not affected. This cannot be undone, and records older than 6 months can't be fetched from the official API again.

---

<a id="settings"></a>
## 6. Settings reference

| Group | Setting | What it does |
| --- | --- | --- |
| General Game Settings | Game Installation Directory | Reading the Convene link, launching the game, opening game folders |
| | Default Game Account (UID) | Account selected at startup |
| | Load local data automatically | Loads local records the first time you open Convene Statistics |
| UI Settings | Theme / Accent color | Color scheme |
| | Window background | Default launcher background or a custom image |
| | Animated background | Plays the launcher's background video (default background only) |
| | Peek at background | How transparent the interface becomes when the pointer rests on an empty area; 80% by default, 0% turns it off |
| | Window glass effect / Background opacity | Translucent blurred background; needs a restart after switching |
| | Software Language | Simplified Chinese / English / Japanese |
| | Notification level | How often non-essential notifications appear; background refreshes are always silent |
| | Reduce motion | Removes movement and scaling, keeping only short fades |
| Game data and image cache | Sync game data / Clear image cache | Checks for data updates now; deletes downloaded images, which are downloaded again when needed |
| Logging | Enable File Logging / Minimum Log Level | Writes diagnostic information to `Local/Logs` for troubleshooting |
| Network & verification | Log API Match Keyword | Keyword used to find the Convene link in `Client.log`; usually leave as is |
| | Geetest Service Port | Local port for the login security check (default 5000; moves to the next free port if taken) |
| | Retries / delay | Retries failed requests automatically |

---

<a id="data"></a>
## 7. Data, backups and updates

### 7.1 Where your data is

With the `portable` file present, all data is in the program folder (the **Data folder** button on Home opens it):

| Location | Contents | Can I delete it? |
| --- | --- | --- |
| `Config\` | Settings | Settings return to defaults |
| `Local\Data\LocalData.db` | Account snapshots, Convene records, encrypted login credentials | **No**, this is your data |
| `Local\Data\*.json`, `Local\Language\`, `Local\Icons\` | Bundled game data, languages and icons | Replaced on update |
| `Local\Cache\` | Cached images, launcher background, announcements | Yes, any time |
| `Local\Logs\` | Logs | Yes |
| `Local\Backups\` | Automatic backups made before database upgrades | Yes, once everything works |

### 7.2 Back up

Close WwTool and copy `Config` and `Local\Data\LocalData.db`.

### 7.3 Update to a new version

1. Close WwTool.
2. Unzip the new archive to the place that holds your existing `WwTool` folder (for `D:\Tools\WwTool`, unzip to `D:\Tools`) and overwrite files with the same name. Release archives contain no database or settings, so your data is kept. If your existing folder has a different name (such as a 1.x folder), copy everything inside the new `WwTool` folder into it and overwrite.
3. Run the new `WwTool.exe`.

Updating from 1.x works the same way: 2.0 reads the 1.2.1 database and login credentials directly.

### 7.4 Move to another PC or Windows user

Copy the whole program folder. Login credentials can only be decrypted by the original Windows user, so you need to **Log in again** for each account afterwards. Convene records and other data are unaffected.

---

<a id="faq"></a>
## 8. FAQ

**After clicking "Get records", the import window keeps appearing**

- Check that the game installation directory in Settings is correct.
- Make sure you opened **Convene → History** in the game and viewed a page. After restarting the game, open it again.
- If it still fails, copy the link from `Client.log` by hand as described in [5.2](#pulls).

**Getting records fails**

- Convene links expire. Open **Convene → History** in the game again, then click **Get records**.
- "The convene link belongs to account A, not the selected account B": switch to the account the link belongs to, or reopen the history in game with the current account.

**The security check page doesn't open when logging in**

Check whether a firewall or security software is blocking local ports, then pick a different **Geetest Service Port** under **Settings → Network & verification** and try again.

**Icons show a placeholder (Lite build or a new Resonator)**

Icons are downloaded from GitHub the first time they're shown. Without a connection a placeholder is shown, and the icon is filled in once you're online. You can also click **Sync game data** in Settings.

**The background or announcements don't update**

The last cache is shown first and refreshing takes a moment. Without a connection the cache stays.

**The animated background doesn't move**

It pauses while the window is minimized or not focused; click back into the window. It is only available with the default background, and falls back to the still image if playback fails.

**Switching the glass effect does nothing**

This setting needs a restart of WwTool.

**Reporting a problem**

Open an [Issue](https://github.com/conFess233/WwTool/issues) with a description and the logs from `Local\Logs`. Before posting, remove your email, the full Convene link, tokens and other personal details.
