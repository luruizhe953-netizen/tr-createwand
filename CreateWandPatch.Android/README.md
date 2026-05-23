# CreateWandPatch.Android

Android port of CreateWandPatch for Terraria (Unity IL2CPP). **Experimental — requires real ARM64 Android device.**

## Build

### Prerequisites
- .NET SDK 10.0+
- Android NDK r23+
- Il2CppDumper (for generating dummy DLLs)

### Step 1: Generate Dummy DLLs

The `lib/` directory contains Il2CppDumper-generated API stubs for compilation. These are NOT included in git (too large). Generate them from the game APK:

```bash
# Decompile the APK
jadx -d decompiled/ Terraria.apk

# Run Il2CppDumper
Il2CppDumper.exe libil2cpp.so global-metadata.dat output/

# Copy the DummyDll/*.dll files you need into lib/
```

Required DLLs:
- `Assembly-CSharp.dll` (Terraria game code)
- `UnityEngine.CoreModule.dll`
- `UnityEngine.IMGUIModule.dll`
- `UnityEngine.UIModule.dll`
- `UnityEngine.InputLegacyModule.dll`
- `UnityEngine.AudioModule.dll`
- `InControl.dll`

### Step 2: Build C# DLL

```bash
cd CreateWandPatch.Android
dotnet build -c Release
# Output: bin/Release/netstandard2.0/CreateWandPatch.Android.dll
```

### Step 3: Build native injector

```bash
cd ../native_loader
ndk-build NDK_PROJECT_PATH=. APP_BUILD_SCRIPT=jni/Android.mk APP_ABI=arm64-v8a
# Output: libs/arm64-v8a/libcwpatch.so
```

### Step 4: Package APK

See `../README.md` for full APK packaging instructions.

## Project Structure

```
CreateWandPatch.Android/
  Bootstrap.cs              Entry point Init()
  GlobalUsings.cs           XNA→Unity type aliases
  Content/                  Blueprint data structures
  Gameplay/                 Placement logic (23 files)
  Patches/                  Harmony patches (7 files)
  Infrastructure/           Item ID compat, Tag I/O
  Rendering/                Blueprint preview (Unity)
  UI/                       Touch UI (coordinate zones)
  lib/                      Dummy DLLs (generate separately)
```

## Known Limitation

x86 PC emulators (MuMu, LDPlayer, BlueStacks) cannot run this mod — their ARM→x86 translation layers prevent injected .so files from calling IL2CPP functions. A real ARM64 Android device is required.
