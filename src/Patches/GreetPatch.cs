using ACS.API;
using HarmonyLib;

namespace ACS.Patches;

[HarmonyPatch]
internal class GreetPatch
{
    /// <summary>
    /// 当 NPC 触发 fov 对话气泡时，播放一次 greet 动画
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Chara), nameof(Chara.TalkTopic))]
    internal static void Postfix(Chara __instance, string topic)
    {
        if (topic != "fov") {
            return;
        }

        // 已在问候中，不重复触发
        if (AcsStateResolver.IsGreetActive(__instance)) {
            return;
        }

        AcsStateResolver.StartGreet(__instance);
    }
}
