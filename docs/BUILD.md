# Build

## Requirements

- Windows
- [.NET SDK](https://dotnet.microsoft.com/download) (project targets **net472**)
- Terraria install (Steam x64): `Terraria.exe`, `FNA.dll`, `ReLogic.dll`

## Configure Terraria path

Pick one:

**A)** Copy `CreateWandPatch/Directory.Build.props.example` to `Directory.Build.props` and set `TerrariaSteam`.

**B)** One-off build:

```powershell
cd CreateWandPatch
dotnet build -c Release -p:TerrariaSteam="D:\SteamLibrary\steamapps\common\Terraria"
```

**C)** Copy the three DLLs into `CreateWandPatch/lib/` (see `lib/说明.txt`).

## Build patch

```powershell
cd CreateWandPatch
dotnet build -c Release
```

Output:

- `CreateWandPatch/bin/Release/net472/CreateWandPatch.dll`
- `CreateWandPatch/bin/Release/net472/0Harmony.dll`

## Build injector (copies DLLs into loader folder)

From repository root:

```powershell
dotnet build "TerrariaPatchLoader/TerrariaLoader/TerrariaLoader.csproj" -c Release
```

Output folder:

`TerrariaPatchLoader/TerrariaLoader/bin/Release/net472/`

Should contain: `TerrariaLoader.exe`, `TerrariaPatchInjector.dll`, `CreateWandPatch.dll`, `0Harmony.dll`, EasyHook binaries.

## Optional: blueprint verifier tool

```powershell
dotnet build "CreateWandPatch/tools/BlueprintVerifier/BlueprintVerifier.csproj" -c Release
```
