# Contributing

Thanks for helping improve CreateWand Patch.

## Scope

- **CreateWandPatch/** — gameplay, Harmony patches, UI
- **TerrariaPatchLoader/** — injection only; keep changes minimal

Please avoid committing decompiled Terraria sources, full `TerrariaServer/` trees, or personal server credentials.

## Pull requests

1. Fork and branch from `main`.
2. Keep diffs focused; match existing C# style in `Gameplay/`.
3. Run `dotnet build -c Release` for `CreateWandPatch` before opening a PR.
4. Describe MP vs single-player impact in the PR text.

## Reporting bugs

Include:

- Patch build date / DLL version
- Terraria version (Steam)
- Single-player or TShock (+ mod list if any)
- Blueprint type: legacy PNG vs precise `cwmap`
- Relevant lines from `CreateWandPatch-mp.log` (redact IPs)
