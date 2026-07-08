# Better Custom Sprites

为 [Elin](https://store.steampowered.com/app/2135150/Elin/) 游戏中的 **Animated Custom Sprites** mod 增强版，新增角色状态感知的动画切换功能。

> 基于 [gottyduke/Elin.Plugins](https://github.com/gottyduke/Elin.Plugins) 中的 AnimatedCustomSprites 修改，遵循 MIT 协议。

## ✨ 新增功能

在原版 ACS 的基础上，扩展了状态前缀（prefix）判断逻辑，使角色能根据当前状态自动切换不同的动画表现：

| 前缀 | 触发条件 | 示例文件名 |
|------|---------|-----------|
| `acs_override` | 通过执行C#脚本`tg.mapStr.Set("acs_override", "")` （Drama）手动指定 | `chara_skin.idle#66_0-5.png` |
| `snow` | 站在雪地上（原版支持） | `chara_acs_snow.idle#66_0-5.png` |
| `wet` | 角色潮湿 | `chara_acs_wet.idle#66_0-5.png` |
| `drunk` | 醉酒状态 | `chara_acs_drunk.idle#66_0-5.png` |
| `confused` | 混乱状态 | `chara_acs_confused.idle#66_0-5.png` |
| `blind` | 失明状态 | `chara_acs_blind.idle#66_0-5.png` |

状态与战斗/待机状态可组合使用，例如：
- `chara_acs_wet.combat#66_0-5.png` — 湿漉漉的战斗姿态
- `chara_acs_drunk.idle#66_0-5.png` — 醉醺醺的待机动作

### 优先级

当一个角色同时满足多个状态时，按以下优先级返回（靠前的优先）：

```
acs_override > snow > wet > drunk > confused > blind
```

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
