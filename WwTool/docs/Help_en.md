# Detailed Help Guide

> ⚠️ **Note**: This document was translated from Chinese by AI.

---

## Table of Contents

- [1. Configure Game Installation Directory and Language](#1-configure-game-installation-directory-and-language)
- [2. Account Login and Data Synchronization](#2-account-login-and-data-synchronization)
  - [1. Account Login](#1-account-login)
  - [2. Security Verification (Geetest Captcha)](#2-security-verification-geetest-captcha)
  - [3. Sync Resonator and Exploration Data](#3-sync-resonator-and-exploration-data)
- [3. Feature Overview](#3-feature-overview)
  - [1. Home (Overview)](#1-home-overview)
  - [2. Exploration Data](#2-exploration-data)
  - [3. Vehicle (Motorcycle) Data](#3-vehicle-motorcycle-data)
- [4. Convene History Import and Analysis](#4-convene-history-import-and-analysis)
  - [1. In-Game Steps](#1-in-game-steps)
  - [2. Auto Import Link (Recommended)](#2-auto-import-link-recommended)
  - [3. Manual Import Link](#3-manual-import-link)
  - [4. Retrieve Data and Statistics](#4-retrieve-data-and-statistics)

---

## 1. Configure Game Installation Directory and Language

### 1.1 Configure Game Installation Directory

The feature to automatically read convene logs depends on the game installation directory. Please follow these steps for initial setup:

1. Launch **WwTool.exe**.
2. Select **Settings** in the left navigation bar.
3. Find **Game Installation Directory** under **General Game Settings**.
4. Click the **Select Path** button on the right, and choose your main Wuthering Waves directory.
   - **Locating the Directory**:
     - Usually the folder where the game launcher is located (e.g., `D:\Wuthering Waves`), or the directory containing the core game files (e.g., `D:\Wuthering Waves\Wuthering Waves Game`).
5. Click **Save Settings** at the bottom right. Once the "Settings Saved!" message appears, configuration is complete.

### 1.2 Configure Display Language

If you need to change the application language:

1. Select **Settings** in the left navigation bar.
2. Find **Display Language** under **Interface Settings**.
3. Select your preferred language (e.g., English, 日本語, 简体中文) from the dropdown list.
4. Click **Save Settings** at the bottom right to apply.

---

## 2. Account Login and Data Synchronization

> This tool requires your Kuro Games account email to retrieve the corresponding Oauth authorization code.

### 1. Account Login

1. Switch to the **Home** page.
2. Click **Add Account** at the top.
3. Enter your account **Email** and **Password** in the pop-up dialog, then click **Login**.

### 2. Security Verification

If security verification (captcha) is triggered, complete the verification in the pop-up browser window. Once verified, the software will automatically complete the login and save your auth token.

### 3. Sync Resonator and Exploration Data

- After logging in, the resonator list will automatically refresh. Select the UID you wish to inspect and click **Get Cloud Data**.
- The retrieved authorization code is encrypted and saved in `./Local/Data/LocalData.db`.

---

## 3. Feature Overview

The program displays account information in several sections:

### 1. Home (Overview)

Displays basic account status and resource recovery:

- **Basic Info**: UID, Nickname, Level, SOL-3 Phase, Active Days, Creation Date, and Server Region.
- **Real-time Waveplates (Energy)**: Current Waveplate value, maximum limit, and remaining recovery time to full.
- **Solitary Waveplate (Overflow)**: Overflow Waveplates accumulation status.
- **Pioneer Podcast (Battle Pass)**: Current Podcast level, weekly Podcast EXP progress and limit.
- **Weekly Challenges**: Completed Weekly Challenge claims for this week.
- **Daily Activity**: Completion status of today's Daily Activity.

### 2. Exploration Data

Exploration progress synced from the world:

- **Sonance Casket Progress**: Shows the total count of collected Sonance Caskets.
- **Chests (Boxes)**: Counts of **Plain Chests**, **Basic Chests**, **Advanced Chests**, and **Premium Chests** opened in the world.
- **Tide Heritages**: Opened Tide Heritages statistics.

### 3. Vehicle (Motorcycle) Data

Vehicle customization and music collection status:

- **Vehicle Level & EXP**: Shows vehicle level and EXP bar.
- **Appearance Unlocks**: Unlocked skin, sticker, ornament, and frame count.
- **Equipped Skin**: Shows the currently equipped skin and quality.
- **Skins / Stickers / Pendants / Frames / Albums tabs**:
  - Click on each tab to inspect specific unlocked item names, qualities, parts, etc.
  - The **Album** tab shows details of unlocked car music albums and track unlock progress.

---

## 4. Convene History Import and Analysis

### 1. In-Game Steps

Since the Wuthering Waves client stores query data in local temporary logs, you must perform these steps before syncing:

1. Launch Wuthering Waves.
2. Open the **Convene** (gacha) page in-game.
3. Click the **History** button, and view at least one page of convene records.

### 2. Auto Import Link (Recommended)

1. Select **Convene Stats** in the WwTool sidebar.
2. Once the game installation directory is configured on the **Settings** page, click the **Auto Import Link** button.

### 3. Manual Import Link

If auto-import fails, try manually copying the link using this method:

1. Locate `Client.log` and open it with a text editor.
   > Typically located under `Wuthering Waves Game\Client\Saved\Logs\Client.log`
2. Search for `gacha/record/query` or `https://gmserver-api.aki-game2.net` in the log file.
3. Copy the entire query URL.
4. Paste it into the **Import Link** box in WwTool and click **Manual Import Link**.

### 4. Retrieve Data and Statistics

1. After confirming the UID is parsed and selected in the dropdown menu, click **Get Cloud Data**.
   > Note: Since the official server only retains logs for 6 months, you can only retrieve data from within the past 6 months.
2. After pulling successfully, the tool will display statistics for each pool:
   - **Overview**: Total pulls, Astrite cost, total 5-star items, won 50/50s, lost 50/50s, and win rate.
   - **Average Pulls per 5-Star**: The average number of pulls to get a 5-star character/weapon.
   - **Pull History & Pity**: Clearly displays the names of obtained 5-star resonators/weapons, the pity count they were pulled at, and the current accumulated (pity) pulls in each pool.
