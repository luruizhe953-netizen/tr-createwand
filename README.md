# CreateWand Patch

**中文说明：** [README.zh-CN.md](README.zh-CN.md)

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
3. In a world, press **`P`** to receive the Create Wand (item **6147**); vanilla has no drop for this item.
4. **Multiplayer** — in-game mode `N` = handheld + explicit msg17; see docs for TShock notes

## Features (summary)

- Presets and PNG blueprint library under `My Games\Terraria\CreateWand\`
- **Legacy (color map):** sorted placement template
- **Precise (`cwmap` / qotstruct):** 1:1 tile / wall / platform style on multiplayer via shared placement chain (inventory swap + equipment sync)
- **Paint & wiring:** paintbrush/paint roller + wire/actuator placement via handheld tool chain; supports all 4 wire colors
- **Adjustable stagger speed:** `<` / `>` hotkeys with visual speed bar (0–30 frames between cells)
- **Repeat placement:** `O` cycles repeat count (up to 10×); blueprint re‑queues automatically after completion to counter server rollback
- **Fast area clear:** `]` selects off / staggered / instant clear; `K` triggers one‑click full‑area delete with wire removal in DeleteOnly mode
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

## Hotkeys (summary)

| Key | Needs wand in hand? | Action |
|-----|---------------------|--------|
| **`P`** | **No** (in-world) | Grant Create Wand (6147); cooldown if spammed |
| `\` / `OemPipe` | Yes | Placement master toggle |
| `[` | Yes | Fast / staggered placement toggle (shows current speed) |
| `]` | Yes | Pre‑placement clear mode: Off → Staggered → Fast (includes wires) |
| `;` | Yes | Toggle precise blueprint placement (1:1 cwmap/qotstruct) |
| `N` | Yes | MP placement mode (LocalOnly / Handheld+msg17 / DeleteOnly) |
| `<` / `>` | Yes | Adjust staggered speed (frames between cells, 0–30) |
| `O` | Yes | Repeat placement count: 1× → 2× → 3× → 5× → 10× (anti‑rollback) |
| `K` | Yes | Instant full‑area delete + wire clear (only in DeleteOnly mode) |
| `1`–`3` | Yes | Built‑in presets |
| `-` / `+` | Yes | Previous / next PNG blueprint |
| `B` | Yes | Box‑select export region to PNG |
| `F9` / Shift+F9 / Ctrl+F9 | **No** | Net / tile‑filter / rollback trace logs |

## Legal

- Terraria is © Re-Logic. This is an unofficial fan patch.
- MIT License — see [LICENSE](LICENSE).

## Disclaimer (Vibe Coding)

This project was developed **entirely through AI-assisted “vibe coding”** (human + LLM iteration). It is **not** professionally audited for stability, security, or anti-cheat compatibility.

- Use at your own risk: crashes, save corruption, MP kicks, or antivirus blocking injection are possible.
- Multiplayer / TShock paths are experimental.
- Back up your characters and worlds before use. **Do not use if you do not accept these risks.**

## Android Port (Experimental)

An Android port of CreateWandPatch is under development in the private workspace.

### Current Status
- C# source code: **100% ported** (47 files, 0 build errors)
- Smali/APK patching: **complete** (Pairip bypass, lifecycle hooks)
- Native injector: **compiled** (ARM64 + x86_64, JNI + DT_NEEDED)
- On-device test: **pending** (requires real ARM device)

### Architecture

The Android version of Terraria is a Unity IL2CPP build. The Windows mod uses Harmony to patch C# at runtime; on Android, IL2CPP compiles C# to native ARM code, so the injection path is:

```
APK Install
  → Application.<clinit> loads libcwpatch.so (smali injection)
  → libmain.so loads libcwpatch.so as dependency (DT_NEEDED)
  → JNI_OnLoad registers native methods
  → onResume callback calls CWPatch.init()
  → init() resolves IL2CPP function pointers (dlopen + dlsym)
  → Calls il2cpp_domain_get → il2cpp_thread_attach → il2cpp_runtime_invoke
  → Executes Bootstrap.Init() → Harmony patches go live
```

### Known Limitation

x86 PC emulators (MuMu, LDPlayer, BlueStacks) use ARM→x86 binary translation (Intel Houdini / Hyper-G). These translation layers assign **per-library TLS contexts**, preventing an injected .so from calling IL2CPP functions in the game's own libraries. All three major emulators tested — same result: SIGSEGV at `il2cpp_domain_get()`.

**A real ARM64 Android device is required.** On native ARM hardware, there is no translation layer, TLS is shared across all .so files, and the injection should work.

### Tech Stack (Android)
- C# `netstandard2.0` with Harmony 2.3.3
- C + NDK for native injector (libcwpatch.so)
- Smali patching (APKTool)
- Il2CppDumper for API analysis
- LIEF + Python for binary patching attempts

## Internal / dev docs

Development handoff notes with server-specific details stay in the private workspace (`HANDOFF_*.md` at repo root). Do **not** commit secrets, SSH keys, or production server IPs to GitHub.
