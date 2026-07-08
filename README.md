# Better Custom Sprites

为 [Elin](https://store.steampowered.com/app/2135150/Elin/) 游戏中的 **Animated Custom Sprites** mod 增强版，新增角色状态感知的动画切换功能。

> 基于 [gottyduke/Elin.Plugins](https://github.com/gottyduke/Elin.Plugins) 中的 AnimatedCustomSprites 修改，遵循 MIT 协议。

## ✨ 新增功能

在原版 ACS 的基础上，扩展了状态判断逻辑，使角色能根据当前状态自动切换不同的动画表现。

### 命名格式

```
_acs_{前缀}.{状态}#{帧间隔ms}_{起始帧}-{结束帧}
```

- **前缀**（可选）：皮肤 / 雪地等外观修饰
- **状态**：角色当前状态，`idle` 为默认，**无需明确写出**
- **帧格式**：`#66_0-5` 表示 66ms/帧，第 0~5 帧循环

> 💡 因为 `idle` 是默认状态，不写状态名就等于 idle。只有 `combat` 等非默认状态才需要写。

### 前缀（Prefix）

前缀是外观修饰，可与任意状态组合。通过 `GetPrefix` 判断，**优先级从高到低**：

| 优先级 | 前缀 | 触发条件 | 示例 |
|--------|------|---------|------|
| 1 | `acs_override` | 通过 Drama 脚本 `tg.mapStr.Set("acs_override", "")` 手动指定 | `_acs_skin.combat#66_0-5` |
| 2 | `snow` | 站在雪地上（原版支持） | `_acs_snow#66_0-5` |
| — | 无 | 默认 | `_acs_wet#66_0-5` |

### 状态（State）

状态代表角色当前的姿态，通过 `GetState` 判断，**优先级从高到低**，取第一个匹配的：

| 优先级 | 状态 | 触发条件 | 示例 |
|--------|------|---------|------|
| 1 | `combat` | 战斗中 | `_acs_combat#66_0-5` |
| 2 | `wet` | 潮湿 / 曾在水中 | `_acs_wet#66_0-5` |
| 3 | `drunk` | 醉酒 | `_acs_drunk#66_0-5` |
| 4 | `confused` | 混乱 | `_acs_confused#66_0-5` |
| 5 | `blind` | 失明 | `_acs_blind#66_0-5` |
| 6 | `idle` | 待机（默认，无需写） | `_acs#66_0-5` |

### 完整查找顺序

每一帧渲染时，mod 会先确定当前的 **前缀**（`GetPrefix`）和 **状态**（`GetState`），然后按以下顺序在后缀表中查找第一个存在的动画：

```
1. _acs_{前缀}.{状态}     ← 最精确：指定前缀 + 指定状态
2. _acs_{前缀}            ← 指定前缀的通用动画（相当于该前缀的 idle）
3. _acs_{状态}            ← 无前缀的指定状态
（都没有则保持原版外观）
```

> 💡 前缀和状态是**独立判断**的：前缀由 `GetPrefix` 决定，状态由 `GetState` 决定，两者再组合查找。异常状态（wet/drunk 等）只在不战斗时作为状态返回，战斗中一定返回 `combat`。

**示例**：站在雪地里的醉酒角色（前缀=snow，状态=drunk）：

| 查找顺序 | 文件名 | 含义 |
|----------|--------|------|
| 1 | `_acs_snow.drunk#66_0-5` | ✅ 命中：雪地 + 醉酒 |
| 2 | `_acs_snow#66_0-5` | 若上面不存在，用雪地通用 |
| 3 | `_acs_drunk#66_0-5` | 若上面都不存在，用醉酒（无雪地） |

**示例**：潮湿的待机角色（无前缀，状态=wet）：`_acs_wet#66_0-5`

### 🔮 后续计划

状态判断逻辑（`GetState` / `GetPrefix`）会持续扩展，例如：

- **见面打招呼** — 检测附近 NPC 触发问候动画
- **睡眠 / 坐下** — 根据角色动作切换姿态
- **天气** — 雨天 / 晴天的不同外观

具体支持的触发条件会随版本更新而增加。

## 🔧 构建

### 环境要求

- [.NET SDK 10.0.x](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- 环境变量 `ElinGamePath` 指向 Elin 游戏根目录，例如：
  ```
  E:\SteamLibrary\steamapps\common\Elin
  ```

游戏目录结构需包含：
```
ElinGamePath/
├─ BepInEx/
│  ├─ core/        (*.dll)
├─ Elin_Data/
│  ├─ Managed/     (*.dll)
```

### 编译

```powershell
dotnet restore
dotnet build src/BetterCustomSprites.csproj -c Release
```

构建产物输出到 `Package/Mod_BetterCustomSprites/`。

## 📦 安装

将构建产物（或从 Release 下载的压缩包）解压到游戏的 BepInEx 插件目录：

```
ElinGamePath/BepInEx/plugins/Mod_BetterCustomSprites/
```

## 📄 许可证

MIT License — 2024-present DK（原作者），本衍生作品同样遵循 MIT 协议。

详见 [LICENSE](./LICENSE)。
