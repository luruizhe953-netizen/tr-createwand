# Install & run

## Injector workflow (recommended)

1. Build both projects — see [BUILD.md](BUILD.md).
2. Copy everything from `TerrariaPatchLoader/TerrariaLoader/bin/Release/net472/` to one folder.
3. Start Terraria (main menu or in-world).
4. Run `TerrariaLoader.exe` as administrator if injection fails.
5. In-game: obtain Create Wand (item **6147**), load a blueprint, use patch hotkeys.

Details: `TerrariaPatchLoader/使用说明.txt` (Chinese).

## In-game controls (patch)

| Input | Action |
|-------|--------|
| `P` | Grant Create Wand (6147) — **no wand required**; short cooldown |
| `\` / `OemPipe` | Placement master toggle |
| `N` | Multiplayer placement mode (handheld + fallback msg17) |
| `[` | Fast / staggered placement |
| `1` / `2` / `3` | Presets (when holding wand) |
| `[` / `]` | Previous / next PNG blueprint |
| F9 / Shift+F9 / Ctrl+F9 | MP debug logging |

## Blueprint files

- PNG color maps: `Documents\My Games\Terraria\CreateWand\*.png`
- Precise maps: `.cwmap` / qotstruct-compatible formats (see patch loader UI)

## Multiplayer (TShock)

- Use **handheld staggered** mode (`N`), not local-only, to verify server visibility.
- Raise `TilePlaceThreshold` / `TileKillThreshold` on the server if players get webbed.
- Large `SendTileSquare` (msg20) region sync is **off** by default (TShock rejects >4×4 for guests).

Client log (optional): `%USERPROFILE%\Documents\My Games\Terraria\CreateWandPatch-mp.log` or env `CREATEWAND_MP_LOG_PATH`.

## Troubleshooting

| Issue | Hint |
|-------|------|
| Injection blocked | Allow EasyHook in antivirus; run loader as admin |
| Build missing FNA | Point `TerrariaSteam` at the correct Steam Terraria folder |
| MP tiles rollback | Check log for `PlaceThingFailedAll`; ensure materials exist in inventory |
| Wrong block types (precise) | Update to build with shared `TryPlaceLegacyServerCell` + item scan |
