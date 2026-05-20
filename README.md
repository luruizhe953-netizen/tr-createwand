# CreateWand Patch

Harmony patch for Terraria **Create Wand** (item 6147): blueprint placement (PNG datamap + precise `cwmap` / qotstruct), multiplayer-friendly server-visible tiles on TShock, and an optional process injector.

> **Note:** You need a legal copy of Terraria. This project does not redistribute game assets or binaries.

## What is included

| Component | Path | Role |
|-----------|------|------|
| **CreateWandPatch** | `CreateWandPatch/` | Main patch (Gameplay, Patches, UI) |
| **TerrariaPatchLoader** | `TerrariaPatchLoader/` | Injects `CreateWandPatch.dll` into a running Terraria process |

Optional (not copied by default): `ImproveGame/` (QoT mod), `TerrariaServer/` (server source mirror for reference).

## Quick start

1. **Build** — see [docs/BUILD.md](docs/BUILD.md)
2. **Inject & play** — see [docs/INSTALL.md](docs/INSTALL.md)
3. **Multiplayer** — in-game mode `N` = handheld + explicit msg17; see docs for TShock notes

## Features (summary)

- Presets and PNG blueprint library under `My Games\Terraria\CreateWand\`
- **Legacy (color map):** sorted placement template
- **Precise (`cwmap` / qotstruct):** 1:1 tile / wall / platform style on multiplayer via shared placement chain (inventory swap + equipment sync)
- Staggered placement queue for TShock rate limits
- Debug log: `CreateWandPatch-mp.log` (optional hotkeys F9 / Shift+F9 / Ctrl+F9)

## Repository layout (after `scripts/prepare-public-repo.ps1`)

```
.
├── CreateWandPatch/
├── TerrariaPatchLoader/
├── docs/
├── LICENSE
├── README.md
└── .gitignore
```

## Legal

- Terraria is © Re-Logic. This is an unofficial fan patch.
- MIT License — see [LICENSE](LICENSE).

## Internal / dev docs

Development handoff notes with server-specific details stay in the private workspace (`HANDOFF_*.md` at repo root). Do **not** commit secrets, SSH keys, or production server IPs to GitHub.
