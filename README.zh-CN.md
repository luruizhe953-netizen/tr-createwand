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

## 功能概要

- 预设与 PNG 蓝图：`Documents\My Games\Terraria\CreateWand\*.png`
- **色图（Legacy）：** 按颜色分类铺砖（模板物块，联机已验证交换+发包链）
- **精确蓝图（cwmap / qot）：** 墙/物块/平台 **1:1** 类型与样式；联机与非精确路径共用 `TryPlaceLegacyServerCell`（背包材料交换 + 装备 sync5）
- 联机逐格队列，降低 TShock 铺砖阈值触发（蛛网化）
- 调试日志：`CreateWandPatch-mp.log`（可选：F9 / Shift+F9 / Ctrl+F9）

### 游戏内热键

| 按键 | 是否需要手持魔杖 | 功能 |
|------|------------------|------|
| **`P`** | **否**（进世界即可） | 发放创造魔杖 **6147**；背包已有则提示位置；有短冷却防连按 |
| `\` / `OemPipe` | 是 | 放置总开关 |
| `N` | 是 | 联机放置模式（手持·失败同逐格） |
| `[` | 是 | 快速 / 逐格 |
| `1` / `2` / `3` | 是 | 三种预设 |
| `-` / `+` | 是 | 上一张 / 下一张 PNG 蓝图 |
| `B` | 是 | 框选导出 PNG 蓝图 |
| `;` | 是 | 切换精确放置（**默认开**；cwmap / qotstruct / PNG+同名 cwmap） |
| `F9` | 否（联机） | 开关网络调试日志 |
| `Shift+F9` | 否（联机） | 仅图格相关 msg 过滤 |
| `Ctrl+F9` | 否（联机） | 开关铺砖「闪回」检测日志 |

联机测试请用 **手持模式**，勿用「仅本地」验收服端是否同步。

---

## 联机（TShock）注意

- 实验性功能；服务端需适当调高 `TilePlaceThreshold` / `TileKillThreshold`，避免铺砖过快被 Disable（蛛网）。
- 默认不在蓝图结束后发送大块 `SendTileSquare`（msg20），无权限时易被拒或断线。
- 家具放置依赖手持材料 + msg79/msg34 等，详见仓库内文档。

---

## 法律与许可

- 泰拉瑞亚 © Re-Logic。本项目为**非官方**爱好者补丁。
- 许可证：MIT — 见 [LICENSE](LICENSE)。

请勿将服务器 IP、SSH 密钥、内网交接文档（`HANDOFF_*.md`）提交到公开仓库。

---

## 免责声明（Vibe Coding）

本仓库**全程在 AI 辅助编程（vibe coding）环境下迭代**：大量代码由人与 LLM 协作生成、修改与拼装，**未经系统化测试与安全审计**。

- 不保证无崩溃、无存档损坏、无联机封禁或反作弊误判；使用注入与内存修改**风险自负**。
- 联机、TShock、生存模式背包交换等路径为实验性质，可能与特定服务端规则冲突。
- 任何问题请先自行备份角色/世界；向 Issues 反馈时请附日志与复现步骤，勿期待商业级支持。

**若你不同意上述风险，请勿下载、编译或运行本补丁。**
