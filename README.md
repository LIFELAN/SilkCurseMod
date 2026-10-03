# SilkCurseMod 使用说明（畸芽诅咒切换）

**简体中文** | [English](README.en.md)

一个《空洞骑士：丝之歌》BepInEx 模组：按热键即可切换 **畸芽诅咒 / Cursed 状态**，
**无需真的装备诅咒纹章**。

---

## 安装

1. 先安装 **BepInExPack Silksong**。
2. 将 `SilkCurseMod.dll` 放入：

   ```
   <游戏目录>/BepInEx/plugins/lifelan-SilkCurseMod/SilkCurseMod.dll
   ```

   （用模组管理器安装，或自行 `dotnet build -c Release` 构建，构建脚本会自动复制。）
3. 启动游戏。首次运行会在 `BepInEx/config/io.github.lifelan.silkcursemod.cfg`
   生成配置文件。

---

## 两种诅咒模式

模组提供两个热键，分别对应两种模式：

| 按键 | 默认 | 模式 | 解除方式 |
| --- | --- | --- | --- |
| `General/Hotkey` | **F6** | 手动诅咒 | 只能再次按同一按键解除。 |
| `General/KillModeHotkey` | **F5** | 杀敌解咒 | 累计击杀足够敌人后自动解除（F6 也可手动取消）。 |

### F6 —— 手动诅咒

- 若当前未受诅咒：开启诅咒，一直持续到再次按 F6。
- 若当前已受诅咒（无论哪种模式）：按 F6 立刻解除。

### F5 —— 杀敌解咒

- 以**杀敌模式**开启诅咒。此模式下只能通过击杀敌人解除；
  按 F6 仍然可以手动取消。
- 按下 F5 时会弹出原版风格提示，显示当前进度（`0 / N`），
  并且**每次按 F5 都会把进度重置为 `0 / N`**。
- 诅咒期间每击杀一个**被猎人日志记录的敌人**，计数 +1；
  达到 `Curse/KillsToUncurse`（默认 **10**）后自动解除诅咒。

击杀判定使用游戏自身的 `EnemyDeathEffects.RecordKillForJournal`：
只有**有猎人日志条目、且确实被大黄蜂造成伤害**的敌人才会计数。
因此脚本击杀、环境击杀（尖刺、岩浆、其他敌人等）都不计入。

---

## 诅咒效果（与原版一致）

- 丝线上限降为 **3**，并显示原版诅咒丝线外观。
- 禁止缚丝治疗。
- 卸下当前装备的所有工具与技能；解除诅咒时恢复原装备（`Curse/DisableTools`，默认开启）。
- **不改动你的真实纹章与招式**——模组只是“伪装”成诅咒状态，
  而不会把 `CurrentCrestID` 切换成诅咒纹章。

诅咒状态（是否激活、模式、击杀进度、工具备份）按存档栏位
（`PlayerData.profileID`）保存在
`BepInEx/config/SilkCurseMod.state.txt` 中，
因此在某个存档开启诅咒不会影响其他存档。

---

## 配置项

配置文件：`BepInEx/config/io.github.lifelan.silkcursemod.cfg`

| 配置项 | 默认值 | 说明 |
| --- | --- | --- |
| `General/Hotkey` | `F6` | 手动诅咒开关热键（`KeyCode` 名称）。装有 ConfigurationManager 时可点击按钮改键。 |
| `General/KillModeHotkey` | `F5` | 杀敌解咒模式热键；同时会重置进度。 |
| `Curse/KillsToUncurse` | `10` | 杀敌模式下解除诅咒所需击杀数（1–999）。 |
| `Curse/DisableTools` | `true` | 诅咒期间卸下工具/技能。 |
| `General/DebugLogging` | `false` | 输出额外调试日志到 BepInEx 控制台/日志。 |

---

## 工作原理

游戏里“是否受诅咒”这一状态会扩散到几乎所有相关系统：

| 游戏成员 | 作用 |
| --- | --- |
| `PlayerData.IsAnyCursed` | 丝线外观、丝线上限（`CurrentSilkMaxBasic`）、缚丝消耗、死亡茧 |
| `ToolCrest.IsEquipped`（诅咒纹章） | HUD 边框、英雄动画、`ToolItemManager.IsCursed`、悬浮工具栏 |

`CursePatches` 用 Harmony 对这些 getter 加 Postfix，
在模组标志激活时返回“受诅咒”，于是所有下游的视觉/逻辑表现都与原版一致。
`CurseController` 另外会清空当前纹章的工具槽（因为投掷工具**并不**受诅咒标志限制），
并按存档栏位持久化全部状态。

由于伪装 `ToolCrest.IsEquipped` 也会影响游戏的 PlayMaker 纹章检测，
这些路径被排除在外：当 `CheckIfCrestEquipped` 求值期间，
以及 `HeroController.Recoil` / `RecoilDown` 读取纹章期间，
会临时关闭伪装。否则 `Sprint` 状态机的冲刺斩分支
（`Reaction Type`）会把猎人纹章的冲刺斩错误地导向女巫/诅咒的后续招式，
导致只有后续动作看起来像诅咒状态。

---

## 重要提示

因为模组会**真的清空 `PlayerData` 中的工具槽**（这是唯一可靠地阻止投掷工具的方法），
所以在诅咒解除之前，这些改动都属于存档内容。
备份的装备配置保存在 `SilkCurseMod.state.txt`，
**诅咒期间请勿删除该文件**，否则可能无法恢复原装备。

---

## 构建

```sh
dotnet build -c Release
```

构建完成后会将 `SilkCurseMod.dll` 复制到 `BepInEx/plugins/lifelan-SilkCurseMod/`。
游戏路径在 `SilksongPath.props` 中配置。
