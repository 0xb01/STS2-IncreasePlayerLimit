# Changelog

## 0.1.4 Version Changelog

### Fixes & Compatibility
* **Game Update Support:** Full compatibility with Slay the Spire 2 **v0.111.1+**.
* **Type Refactor Fix:** Updated network serialization patches to target `StartRunLobbyPlayer` after upstream `LobbyPlayer` refactor.
* **Lobby Constructor & MaxPlayers:** Updated `StartRunLobby` patches to support new 5-param constructor and private `_maxPlayers` field.
* **Network Idempotency:** Added protection against duplicate message handler registration.
* **Treasure Skip Polish:** Unified relic room patches and used native `SkipRelicLocally`.
* **Simplified Documentation:** Rewrote `README.md` in simple, plain English.

---

## 0.1.3 Version Changelog (Revamped by 0xb1)

### Features & Updates
* **Rebrand & Overhaul:** Complete repository revamp to **IncreasePlayerLimit** maintained by **0xb1**.
* **Compatibility:** Full compatibility with Slay the Spire 2 **v0.107.1+** (.NET 9 + Godot 4.5.1).
* **Architecture:** Modularized codebase into dedicated namespaces (`IncreasePlayerLimit.Configuration`, `IncreasePlayerLimit.Network`, `IncreasePlayerLimit.Localization`, and `IncreasePlayerLimit.Patches`).
* **Localization & Documentation:** Entire codebase, line-by-line comments, build tools, and documentation converted to clean English.
* **Treasure Room Skip Button:** Integrated native relic skip action to prevent softlocks when players choose not to take a relic.
* **Relic Pool Safeguard:** Automatically provides Strawberry when relic pools are exhausted in high player-count games.
* **Dynamic Difficulty Scaling:** Seamlessly scales monster HP and block beyond 4 players according to official formulas with an in-game toggle.

---

## 0.0.6 Version Changelog

### Features
* Added monster difficulty scaling for 5+ players: monster HP, block, and power amounts continue to scale beyond the vanilla 4-player cap using official formulas.
* Added "Difficulty Scaling" toggle in the Settings screen.

### Improvements
* Introduced independent mod network protocol channel running concurrently alongside the official packet system.

---

## 0.0.5A Version Changelog

### Features
* Added in-game settings entry allowing players to adjust lobby limits (4-16 players) in real time.
* Added Linux platform support with native dependency preloading.
* Added macOS platform support with TLS handshake bypass for multiplayer sockets.

### Improvements
* Migrated configuration format to `config.ini`.
* Relic pool exhaustion fallback to Strawberry.
* Improved multiplayer packet synchronization and bit width alignment.
