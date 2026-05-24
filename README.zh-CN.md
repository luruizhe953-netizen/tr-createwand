# CreateWand Patch（创造魔杖补丁）

面向泰拉瑞亚 **创造魔杖**（物品 ID **6147**）的 Harmony 补丁：支持 PNG 色图蓝图与精确蓝图（`cwmap` / qotstruct），联机（TShock）下尽量保持服务端可见的铺砖/铺墙，并附带进程注入加载器。

> **说明：** 你需要自行拥有**正版泰拉瑞亚**。本仓库**不**分发游戏本体或游戏资源文件。

**English:** [README.md](README.md)

---

## 下载即用（玩家）

1. 打开本仓库 **Releases**，下载 **`CreateWandPatch-TerrariaLoader-*.zip`**（整包，不要只下 DLL）。
2. 解压到**任意文件夹**（不必放进泰拉安装目录）。
3. 先启动泰拉瑞亚，或运行 `TerrariaLoader.exe` 并带上 `--launch` 由加载器启动游戏。
4. **以管理员身份**运行 `TerrariaLoader.exe`（EasyHook 注入常被杀毒拦截，需自行放行）。
5. 可选：第一个参数为泰拉路径，例如  
   `TerrariaLoader.exe "D:\SteamLibrary\steamapps\common\Terraria\Terraria.exe"`
6. 进世界后按 **`P`** 获取创造魔杖（物品 **6147**）；原版无掉落，补丁用 `Player.GetItem` 发放（见下方热键）。

**不需要**单独复制 `ReLogic.dll`、`FNA.dll` 到游戏目录——那些只在**从源码编译**时才需要。

压缩包内应包含（同一目录）：

| 文件 | 作用 |
|------|------|
| `TerrariaLoader.exe` | 注入启动器 |
| `TerrariaPatchInjector.dll` | 注入桥 |
| `CreateWandPatch.dll` | **补丁本体（改游戏逻辑）** |
| `0Harmony.dll` | Harmony 运行时 |
| `EasyHook*.dll` 等 | EasyHook 依赖 |

---

## 从源码编译（开发者）

1. 复制 `CreateWandPatch/Directory.Build.props.example` 为 `Directory.Build.props`，填写本机泰拉 Steam 路径（需有 `Terraria.exe`、`ReLogic.dll`）。
2. 编译补丁与加载器 — 详见 [docs/BUILD.md](docs/BUILD.md)。
3. 注入与热键 — 详见 [docs/INSTALL.md](docs/INSTALL.md)。

```powershell
cd CreateWandPatch
dotnet build -c Release

cd ..
dotnet build "TerrariaPatchLoader\TerrariaLoader\TerrariaLoader.csproj" -c Release
# 输出：TerrariaPatchLoader\TerrariaLoader\bin\Release\net472\
```

---

## 仓库结构

| 目录 | 说明 |
|------|------|
| `CreateWandPatch/` | 补丁源码：`Gameplay/` 放置与联机逻辑，`Patches/` Harmony 挂点，`UI/` 界面 |
| `TerrariaPatchLoader/` | `TerrariaLoader.exe` + 注入器，负责把 `CreateWandPatch.dll` 载入运行中的泰拉进程 |
| `docs/` | 编译、安装、贡献说明 |

实际改游戏行为的入口：`CreateWandPatch/Bootstrap.cs` → `Init()`，补丁类在 `CreateWandPatch/Patches/`。

---

## 更新日志

### 2026-05-24
- **新增 ImproveGamePatch**：6 个 QoL 功能 + `~` 控制面板（滑动开关 UI）
  - VeinMiner（连锁挖矿）、BannerPatch（背包旗帜）、FasterExtractinator（快速提炼）
  - PortableStation（便携制作站）、InfiniteBuff（带 1 瓶药水=永久 Buff）、HomeTeleport（H 键回家）
- **CreateWandPatch 热键调整**：
  - 移除 `1` `2` `3`（预设）和 `[`（逐格切换），改用 UI 面板设置
  - 恢复 `]` 键（清空模式）
  - 新增 `C` 键（材料消耗开关：开启后每格扣背包 1 个对应材料）
- 修复：`Player.GetItem` Harmony 歧义、性能卡顿（共享 Keyboard 状态）

### 更早版本
见 [Releases](https://github.com/luruizhe953-netizen/tr-createwand/releases)

---

## 功能概要

- 预设与 PNG 蓝图：`Documents\My Games\Terraria\CreateWand\*.png`
- **色图（Legacy）：** 按颜色分类铺砖（模板物块，联机已验证交换+发包链）
- **精确蓝图（cwmap / qot）：** 墙/物块/平台 **1:1** 类型与样式；联机与非精确路径共用 `TryPlaceLegacyServerCell`（背包材料交换 + 装备 sync5）
- **涂刷与线路：** 漆刷/滚刷 + 四色线/制动器，手持工具链分阶段执行
- **可调速逐格：** `<` / `>` 调节格间延迟（0~30 帧），进度条可视化
- **重复放置：** `O` 设定次数（最多 10×），自动重新入队防服务器回滚
- **快速清区：** `]` 三档切换（关/逐格/一键），`K` 一键全清含线路
- 联机逐格队列，降低 TShock 铺砖阈值触发（蛛网化）
- 调试日志：`CreateWandPatch-mp.log`（可选：F9 / Shift+F9 / Ctrl+F9）

### 游戏内热键

| 按键 | 是否需要手持魔杖 | 功能 |
|------|------------------|------|
| **`P`** | **否**（进世界即可） | 发放创造魔杖 **6147** |
| `\` / `OemPipe` | 是 | 放置总开关 |
| `N` | 是 | 联机放置模式 |
| `]` | 是 | 放置前清空模式：关 → 逐格 → 一键 |
| `;` | 是 | 精确蓝图开关 |
| `<` / `>` | 是 | 逐格速度 +/- |
| `O` | 是 | 重复放置次数：1×→2×→3×→5×→10× |
| `K` | 是 | 一键全清蓝图区域（仅「仅删除」模式） |
| `-` / `+` | 是 | 上一张 / 下一张 PNG 蓝图 |
| `B` | 是 | 框选导出 PNG 蓝图 |
| `C` | 是 | 材料消耗开关（开启后每格扣背包 1 个对应材料） |
| `F9` | 否（联机） | 网络调试日志 |
| `Shift+F9` | 否（联机） | 仅图格 msg 过滤 |
| `Ctrl+F9` | 否（联机） | 闪回检测日志 |

联机测试请用 **手持模式**，勿用「仅本地」验收服端是否同步。

---

## 联机（TShock）注意

- 实验性功能；服务端需适当调高 `TilePlaceThreshold` / `TileKillThreshold` / `TilePaintThreshold`，避免铺砖/涂漆过快被 Disable（蛛网）。
- **涂刷**：guest 组需有 `tshock.world.paint` 权限，否则正常漆刷也无法使用。
- 默认不在蓝图结束后发送大块 `SendTileSquare`（msg20），无权限时易被拒或断线。
- 家具放置依赖手持材料 + msg79/msg34 等，详见仓库内文档。

---

## 法律与许可

- 泰拉瑞亚 © Re-Logic。本项目为**非官方**爱好者补丁。
- 许可证：MIT — 见 [LICENSE](LICENSE)。

请勿将服务器 IP、SSH 密钥、内网交接文档（`HANDOFF_*.md`）提交到公开仓库。

---

## Android 移植（实验性）

CreateWandPatch 的 Android 移植正在开发中（私有工作区内）。

### 当前状态
- C# 源码：**100% 移植**（47 文件，0 编译错误）
- Smali/APK 修补：**完成**（Pairip 绕过、生命周期钩子）
- 原生注入器：**已编译**（ARM64 + x86_64 双架构，支持 JNI + DT_NEEDED）
- 真机测试：**待进行**（需要 ARM 真机）

### 技术路线

泰拉瑞亚 Android 版是 Unity IL2CPP 编译的。Windows 版通过 Harmony 在运行时修改 C# 代码；Android 版 IL2CPP 会把 C# 编译成原生 ARM 代码，注入链路如下：

```
APK 安装
  → Application.<clinit> 加载 libcwpatch.so（smali 注入）
  → libmain.so 依赖加载 libcwpatch.so（DT_NEEDED）
  → JNI_OnLoad 注册原生方法
  → onResume 回调调用 CWPatch.init()
  → init() 解析 IL2CPP 函数指针（dlopen + dlsym）
  → 调用 il2cpp_domain_get → il2cpp_thread_attach → il2cpp_runtime_invoke
  → 执行 Bootstrap.Init() → Harmony 补丁生效
```

### 已知限制

x86 PC 模拟器（MuMu、雷电、蓝叠）使用 ARM→x86 二进制翻译（Intel Houdini / Hyper-G）。这些翻译层为**每个 .so 单独分配 TLS 上下文**，导致注入的 .so 无法调用游戏本体库中的 IL2CPP 函数。三个主流模拟器全部测试过——结果一致：`il2cpp_domain_get()` 处 SIGSEGV。

**需要真机 ARM64 Android 设备。** 真机原生 ARM 硬件无翻译层，所有 .so 共享 TLS，注入应该正常运作。

### 技术栈（Android 端）
- C# `netstandard2.0` + Harmony 2.3.3
- C + NDK 编写原生注入器（libcwpatch.so）
- Smali 修补（APKTool）
- Il2CppDumper 分析 IL2CPP API
- LIEF + Python 做二进制补丁尝试

---

## 免责声明（Vibe Coding）

本仓库**全程在 AI 辅助编程（vibe coding）环境下迭代**：大量代码由人与 LLM 协作生成、修改与拼装，**未经系统化测试与安全审计**。

- 不保证无崩溃、无存档损坏、无联机封禁或反作弊误判；使用注入与内存修改**风险自负**。
- 联机、TShock、生存模式背包交换等路径为实验性质，可能与特定服务端规则冲突。
- 任何问题请先自行备份角色/世界；向 Issues 反馈时请附日志与复现步骤，勿期待商业级支持。

**若你不同意上述风险，请勿下载、编译或运行本补丁。**
