namespace ACS.API;

public static class AcsStateResolver
{
    /// <summary>
    /// 获取角色的基础状态
    /// 优先级：战斗 > greet > 异常状态 > 待机
    /// </summary>
    public static string GetState(Chara chara)
    {
        // 1. 战斗状态优先级最高
        if (chara.IsInCombat) {
            return "combat";
        }

        // 2. 问候状态（NPC 看见玩家时触发，动画播完一轮后自动结束）
        if (IsGreetActive(chara)) {
            return "greet";
        }

        // 3. 异常状态（按视觉显著性排序，作为 idle 的替代状态）
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

        // 3. 默认待机状态
        return "idle";
    }

    /// <summary>
    /// 获取角色的特殊状态前缀
    /// 优先级：手动设置 > 雪地
    /// </summary>
    public static string? GetPrefix(Chara chara, bool snow = false)
    {
        // 1. 手动设置的acs_override优先级最高
        if (chara.mapStr.TryGetValue("acs_override", out string overrideClip)) {
            return overrideClip;
        }

        // 2. 雪地（人物在雪地时的特殊外观）
        if (snow) {
            return "snow";
        }

        return null;
    }

    /// <summary>
    /// 检查角色是否处于问候状态
    /// </summary>
    public static bool IsGreetActive(Chara chara)
    {
        return chara.mapStr.ContainsKey("acs_greet");
    }

    /// <summary>
    /// 触发问候状态
    /// </summary>
    public static void StartGreet(Chara chara)
    {
        chara.mapStr.Set("acs_greet", "1");
    }

    /// <summary>
    /// 停止问候状态
    /// </summary>
    public static void StopGreet(Chara chara)
    {
        chara.mapStr.Remove("acs_greet");
    }
}
