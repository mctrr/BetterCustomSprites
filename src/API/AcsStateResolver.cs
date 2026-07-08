namespace ACS.API;

public static class AcsStateResolver
{
    /// <summary>
    /// 获取角色的基础状态（combat / idle）
    /// </summary>
    public static string GetState(Chara chara)
    {
        return chara.IsInCombat ? "combat" : "idle";
    }

    /// <summary>
    /// 获取角色的特殊状态前缀
    /// 优先级：手动设置 > 雪地 > 异常状态 > 其他
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

        // 3. 异常状态（按视觉显著性排序，靠前的优先）
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

        return null;
    }
}
