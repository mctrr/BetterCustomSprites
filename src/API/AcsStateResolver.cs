namespace ACS.API;

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public static class AcsStateResolver
{
    // greet 触发后延迟再激活，避免气泡一出就切动画
    private const float GreetDelay = 0.2f;

    /// <summary>PC 手动攻击时写入的战斗粘滞标记（mapStr key）。</summary>
    public const string CombatFlagKey = "acs_combat";

    /// <summary>
    /// 无明确敌对目标后，战斗粘滞最多再保持多久（秒）。
    /// 有人把 PC 当 enemy / PC 有 enemy / GoalCombat 时不看超时。
    /// </summary>
    private const float CombatStickySeconds = 8f;

    // 全图 charas 扫描降频：同窗口内按角色缓存结果（约 6 次/秒 @60fps）
    private const int HostileScanIntervalFrames = 10;
    private static int _hostileCacheFrame = -999;
    private static readonly Dictionary<int, bool> HostileTargetCache = new Dictionary<int, bool>();

    /// <summary>
    /// 基础状态优先级：战斗 > 移动(仅 PC) > greet(NPC) > 异常 > idle
    /// </summary>
    public static string GetState(Chara chara)
    {
        if (IsCombatVisual(chara)) {
            return "combat";
        }

        // 资源名 _acs_move；NPC 格子移动不需要 walk ACS
        if (chara.IsPC && IsPcMoving(chara)) {
            return "move";
        }

        if (!chara.IsPC && IsGreetActive(chara)) {
            return "greet";
        }

        if (chara.isWet || chara.wasInWater) {
            return "wet";
        }

        if (chara.isDrunk) {
            return "drunk";
        }

        if (chara.isConfused) {
            return "confused";
        }

        if (chara.isBlind) {
            return "blind";
        }

        return "idle";
    }

    /// <summary>
    /// 是否应播 combat ACS。
    /// 原版 IsInCombat = ai is GoalCombat；PC 手动点怪往往不挂 GoalCombat、GoHostile 对 IsPC 不写 enemy。
    /// 补充：combatCount、被敌对锁定、DoHostileAction 粘滞标记。
    /// </summary>
    public static bool IsCombatVisual(Chara chara)
    {
        if (chara is null) {
            return false;
        }

        if (chara.IsInCombat) {
            return true;
        }

        if (chara.combatCount > 0) {
            return true;
        }

        if (HasLiveEnemy(chara)) {
            return true;
        }

        if (IsTargetedByHostile(chara)) {
            return true;
        }

        if (chara.IsPC && HasCombatSticky(chara)) {
            return true;
        }

        return false;
    }

    /// <summary>PC 发起/卷入敌对时调用，开启 combat ACS 粘滞。</summary>
    public static void MarkCombat(Chara chara)
    {
        if (chara is null) {
            return;
        }

        chara.mapStr.Set(CombatFlagKey, Time.realtimeSinceStartup.ToString());
    }

    /// <summary>明确脱战时清粘滞（可选）。</summary>
    public static void ClearCombat(Chara chara)
    {
        chara?.mapStr.Remove(CombatFlagKey);
    }

    private static bool HasLiveEnemy(Chara chara)
    {
        var e = chara.enemy;
        if (e is null) {
            return false;
        }

        return e.IsAliveInCurrentZone && e.ExistsOnMap && !e.isDead;
    }

    private static bool IsTargetedByHostile(Chara chara)
    {
        if (chara is null) {
            return false;
        }

        int key = RuntimeHelpers.GetHashCode(chara);
        int fc = Time.frameCount;
        bool windowFresh = (fc - _hostileCacheFrame) < HostileScanIntervalFrames;
        if (windowFresh && HostileTargetCache.TryGetValue(key, out bool cached)) {
            return cached;
        }

        if (!windowFresh) {
            HostileTargetCache.Clear();
            _hostileCacheFrame = fc;
        }

        bool hit = ScanTargetedByHostile(chara);
        HostileTargetCache[key] = hit;
        return hit;
    }

    /// <summary>控制台重载等：丢掉敌对扫描缓存，下次立刻全图扫。</summary>
    public static void ClearHostileScanCache()
    {
        HostileTargetCache.Clear();
        _hostileCacheFrame = -999;
    }

    private static bool ScanTargetedByHostile(Chara chara)
    {
        try {
            var map = EClass._map;
            if (map?.charas is null) {
                return false;
            }

            foreach (Chara c in map.charas) {
                if (c is null || c == chara || c.isDead) {
                    continue;
                }

                if (c.enemy != chara) {
                    continue;
                }

                if (!c.IsAliveInCurrentZone || !c.ExistsOnMap) {
                    continue;
                }

                // 友军互指不算战斗演出
                if (chara.IsPCFactionOrMinion && c.IsPCFactionOrMinion) {
                    continue;
                }

                return true;
            }
        }
        catch {
            // 主菜单 / 地图未就绪
        }

        return false;
    }

    private static bool HasCombatSticky(Chara chara)
    {
        if (!chara.mapStr.TryGetValue(CombatFlagKey, out string value)
            || !float.TryParse(value, out float started)) {
            return false;
        }

        float elapsed = Time.realtimeSinceStartup - started;
        if (elapsed < 0f) {
            ClearCombat(chara);
            return false;
        }

        // 仍有明确敌对：保持 combat，并刷新起点避免长战中途断
        if (HasLiveEnemy(chara) || IsTargetedByHostile(chara) || chara.IsInCombat) {
            MarkCombat(chara);
            return true;
        }

        if (elapsed <= CombatStickySeconds) {
            return true;
        }

        ClearCombat(chara);
        return false;
    }

    /// <summary>
    /// PC 是否应播 move ACS。
    /// 仅看 CharaRenderer.isMoving 会在格子衔接瞬间变 false，长按 WASD 会闪 idle。
    /// 点地：路径 PathReady 且未到终点时 isMoving 可能尚未置位，补未走完的 HasPath。
    /// 勿用 DestDist：那是寻路到达容差，站着也可能 &gt; 0，会导致 idle 一直播 move。
    /// 补充：GoalManualMove + EInput.axis（勿 ConvertAxis）。
    /// </summary>
    public static bool IsPcMoving(Chara chara)
    {
        if (chara.renderer is CharaRenderer cr && cr.IsMoving) {
            return true;
        }

        if (chara.ai is GoalManualMove) {
            return true;
        }

        // 格子衔接时 isMoving/AI 可能瞬时 false，轴仍非零则保持 move
        if (EInput.axis != Vector2.zero) {
            return true;
        }

        // 点地起步：仅「路径就绪且尚未到达终点」才算移动（裸 HasPath/DestDist 会粘死）
        try {
            PathProgress path = chara.path;
            if (path is not null
                && path.HasPath
                && path.nodes is not null
                && path.nodeIndex < path.nodes.Count
                && !path.IsDestinationReached(chara.pos)) {
                return true;
            }
        }
        catch {
            // 主菜单 / 地图未就绪
        }

        return false;
    }

    /// <summary>
    /// 特殊状态前缀优先级：手动 override > 雪地 > 已婚
    /// </summary>
    public static string? GetPrefix(Chara chara, bool snow = false)
    {
        if (chara.mapStr.TryGetValue("acs_override", out string overrideClip)) {
            return overrideClip;
        }

        if (snow) {
            return "snow";
        }

        if (chara.IsMarried) {
            return "married";
        }

        return null;
    }

    /// <summary>
    /// acs_greet 存激活时间戳；当前时间 >= 该值才算激活（实现 GreetDelay 延迟）。
    /// </summary>
    public static bool IsGreetActive(Chara chara)
    {
        if (!chara.mapStr.TryGetValue("acs_greet", out string value)) {
            return false;
        }

        if (!float.TryParse(value, out float activateAt)) {
            return false;
        }

        return Time.realtimeSinceStartup >= activateAt;
    }

    /// <summary>触发问候（延迟 <see cref="GreetDelay"/> 秒后激活）。</summary>
    public static void StartGreet(Chara chara)
    {
        chara.mapStr.Set("acs_greet",
            (Time.realtimeSinceStartup + GreetDelay)
            .ToString());
    }

    public static void StopGreet(Chara chara)
    {
        chara.mapStr.Remove("acs_greet");
    }
}
