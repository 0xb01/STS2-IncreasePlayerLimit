<div align="center">

# IncreasePlayerLimit

[**Changelog**](docu/Changelog.md)

![Version](https://img.shields.io/badge/Version-0.1.3-blue.svg)
![Game](https://img.shields.io/badge/Slay_The_Spire_2-v0.107.1+-red.svg)
![Platform](https://img.shields.io/badge/Platform-Windows%20|%20macOS%20|%20Linux-lightgrey.svg)
![Development](https://img.shields.io/badge/Development-AI--Assisted-blueviolet.svg)
![Author](https://img.shields.io/badge/Developer-0xb1-success.svg)

*An AI-assisted Slay the Spire 2 multiplayer mod that lifts the vanilla 4-player lobby cap. Gather your party and climb the Spire together!*

</div>

**IncreasePlayerLimit** is an open-source, AI-assisted mod expanding cooperative multiplayer capacity in Slay the Spire 2. By default, it seamlessly supports **8 players** and can be configured up to **16 players** directly from the in-game settings screen.

<br>

<div align="center">
  <img src="docu/combat.png" alt="Combat" width="800"/>
  <br><br>
  <img src="docu/shop.png" alt="Shop" width="800"/>
  <br><br>
  <img src="docu/campfire.png" alt="Campfire" width="800"/>
  <br><br>
  <img src="docu/treasure.png" alt="Treasure" width="800"/>
</div>

<br>

## ✨ Core Features

* 👥 **Expanded Multiplayer:** Increases the maximum lobby size up to 16 players (default is 8).
* 🏕️ **Expanded Campfire Seating:** Player character models no longer overlap when more than 4 players are present. Campfires generate front and back rows complete with additional background seating logs.
* 💰 **Organized Shop Layout:** Visiting the merchant with a large group automatically arranges player models into neat multi-row grids to prevent crowding and clipping.
* 🎁 **Adaptive Treasure Room:** The relic selection screen scales dynamically, splitting relic holders into centered rows and providing a dedicated "Skip" relic button. Safeguards against empty pool crashes by falling back to Strawberry.
* 📝 **In-Game Settings:** Configure the maximum player count (4–16) directly in the General Settings menu.
* ⚔️ **Dynamic Difficulty Scaling:** When enabled, monster HP and block continue to scale beyond the vanilla 4-player cap using the game's official formulas to maintain challenge.
* 🔒 **Cross-Platform Compatibility:** Native support for Windows, macOS (with automated TLS handshake fixes), and Linux (automated dependency preloading).

## 🎮 Installation

Pre-packaged releases are separated per platform with one-click installers included.

### Windows

1. Download `IncreasePlayerLimit-v[version]-Windows.zip` from [**Releases**](../../releases).
2. Extract the archive.
3. Run `Install.bat` to install automatically (or manually copy the `IncreasePlayerLimit` folder to `<Slay the Spire 2>/mods/`).
4. Launch the game.

### macOS (Apple Silicon)

macOS requires placing the mod inside the `.app` bundle and launching under Rosetta 2.

1. Download `IncreasePlayerLimit-v[version]-macOS.zip` from [**Releases**](../../releases).
2. Extract the archive.
3. Double-click `Install.command` (or run `./install.sh` in Terminal) for automatic installation and `steam_appid.txt` configuration.
   - *Manual alternative:* Copy the `IncreasePlayerLimit` folder to:
     ```
     <Slay the Spire 2>/SlayTheSpire2.app/Contents/MacOS/mods/
     ```
     and create `steam_appid.txt` containing `2868840` in `<Slay the Spire 2>/SlayTheSpire2.app/Contents/MacOS/`.
4. Run the game under Rosetta 2 (right-click `SlayTheSpire2.app` > **Get Info** > check **"Open using Rosetta"**).

### Linux (Steam Deck / Desktop)

1. Download `IncreasePlayerLimit-v[version]-Linux.zip` from [**Releases**](../../releases).
2. Extract the archive.
3. Run `./install.sh` in Terminal for automatic installation (or manually copy the `IncreasePlayerLimit` folder to `<Slay the Spire 2>/mods/`).
4. Launch the game via Steam. Native dependencies (`libgcc-s1`, `libstdc++6`, `libunwind8`) are handled automatically.

> **Note:** All players in the party must install the mod. Local preferences can differ; the host's configured limit governs the active lobby capacity.

## ⚙️ Configuration

Settings can be changed in-game under **Settings > General** or by editing `mods/IncreasePlayerLimit/config.ini`:

```ini
[macos]
tls_workaround=true

[multiplayer]
max_player_limit=8
difficulty_scaling=true
```

## 🛠️ Building from Source

Prerequisites:
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download)
- [Godot 4.5.1 .NET](https://godotengine.org/)
- Slay the Spire 2 (v0.107.1+)

```bash
# Clone the repository
git clone https://github.com/0xb01/STS2-IncreasePlayerLimit.git
cd STS2-IncreasePlayerLimit

# Windows: Build all release packages
pwsh ./tools/build_release.ps1

# Linux / macOS: Build all release packages
./tools/build_release.sh
```

Packages are generated under `build/`:
- `IncreasePlayerLimit-v[version]-Windows.zip` (with `Install.bat` & `helper.ps1`)
- `IncreasePlayerLimit-v[version]-macOS.zip` (with `Install.command` & `install.sh`)
- `IncreasePlayerLimit-v[version]-Linux.zip` (with `install.sh`)
- `IncreasePlayerLimit-v[version].zip` (universal manual folder)

## 🤖 AI Assistance

This project is developed with AI assistance—architecture, Harmony patch transpilers, cross-platform compatibility layers, and UI layout algorithms were designed and iterated collaboratively with AI tooling.

## 📜 License

This project is licensed under [CC0 1.0 Universal](LICENSE).
