using System.Collections.Generic;

namespace ACS.API;

public static class AcsController
{
    public const string ReservedSuffix = "__acs_internal_reserved__";

    public static SpriteData? GetAcsClip(this Chara chara, string? clipName, bool snow = false)
    {
        // 1. 确定基础状态
        string state = AcsStateResolver.GetState(chara);
        // 2. 获取前缀（皮肤/特殊状态，包括雪地）
        string? prefix = AcsStateResolver.GetPrefix(chara, snow);
        // 3. 获取后缀表（ACS 动画片段经 ReloadSuffixPatch 解析后存放在此）
        var suffixes = chara.sourceCard.replacer.suffixes;

        // 3. 有前缀时的查找
        if (prefix != null)
        {
            // 3.1 新格式: "前缀.状态"
            if (suffixes.TryGetValue($"_acs_{prefix}.{state}", out var prefixedState))
            {
                return prefixedState;
            }

            // 3.2 旧格式: "前缀"（通用动画，兼容旧版）
            if (suffixes.TryGetValue($"_acs_{prefix}", out var prefixGeneric))
            {
                return prefixGeneric;
            }
        }

        // 4. 基础状态
        if (suffixes.TryGetValue($"_acs_{state}", out var baseState))
        {
            return baseState;
        }

        // 5. greet 状态没有对应动画时，立即清除标记，回退到 idle
        if (state == "greet") {
            AcsStateResolver.StopGreet(chara);
            return GetAcsClip(chara, clipName, snow);
        }

        // 6. 都没有，返回null（保持原样）
        return null;
    }

    public static void PlayAcsClip(this Card owner, string clipName)
    {
        owner.mapStr.Set("acs_override", clipName);
    }

    public static void StopAcsClip(this Card owner)
    {
        owner.mapStr.Remove("acs_override");
    }

    public static SpriteData? GetAcsClip(this Thing thing, string? clipName, bool snow = false)
    {
        // TODO
        return null;
    }
}