using ACS.API;
using HarmonyLib;

namespace ACS.Patches;

[HarmonyPatch]
internal class GreetPatch
{
    /// <summary>
    /// 当 NPC 触发 fov 对话气泡时，延迟 0.1 秒后播放一次 greet 动画
    /// 延迟由 AcsStateResolver.IsGreetActive 的时间戳比较实现，无需额外线程
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

        // 设置延迟激活时间，0.2 秒后 IsGreetActive 才返回 true
        AcsStateResolver.StartGreet(__instance);
    }
}



