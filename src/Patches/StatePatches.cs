using ACS.API;
using HarmonyLib;

namespace ACS.Patches;

/// <summary>
/// 状态触发：战斗粘滞、问候动画。
/// </summary>
[HarmonyPatch]
internal class CombatPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Chara), nameof(Chara.DoHostileAction))]
    internal static void OnDoHostileAction(Chara __instance, Card _tg)
    {
        if (__instance is null) {
            return;
        }

        // PC 发起攻击（含 ActMelee/ActRanged）
        if (__instance.IsPC) {
            AcsStateResolver.MarkCombat(__instance);
        }

        // PC 被攻击：原版会设 combatCount，这里同步粘滞
        if (_tg != null && _tg.isChara) {
            var target = _tg.Chara;
            if (target != null && target.IsPC) {
                AcsStateResolver.MarkCombat(target);
            }
        }
    }

    /// <summary>
    /// 部分路径只 SetEnemy 不经 DoHostileAction；PC 被设为敌对目标时也开粘滞。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Chara), nameof(Chara.SetEnemy))]
    internal static void OnSetEnemy(Chara __instance, Chara c)
    {
        if (c is null) {
            return;
        }
        if (c.IsPC) {
            AcsStateResolver.MarkCombat(c);
        }
        if (__instance != null && __instance.IsPC) {
            AcsStateResolver.MarkCombat(__instance);
        }
    }
}

[HarmonyPatch]
internal class GreetPatch
{
    /// <summary>
    /// NPC 触发 fov 对话气泡时，延迟后播放一次 greet 动画。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Chara), nameof(Chara.TalkTopic))]
    internal static void Postfix(Chara __instance, string topic)
    {
        if (topic != "fov") {
            return;
        }

        if (AcsStateResolver.IsGreetActive(__instance)) {
            return;
        }

        AcsStateResolver.StartGreet(__instance);
    }
}
