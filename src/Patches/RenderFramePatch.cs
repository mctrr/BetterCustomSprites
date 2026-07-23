using System.Collections.Generic;
using System.Reflection.Emit;
using ACS.API;
using HarmonyLib;

namespace ACS.Patches;

/// <summary>
/// 原版 CardActor.OnRender 只播放 replacer.data 的多帧动画。
/// 本 patch 在帧推进前把 SpriteData 替换为当前 ACS clip，使 NPC/非 PCC 角色能播状态动画。
/// 主角（PCC）由 <see cref="PccRenderPatch"/> 单独处理。
/// </summary>
[HarmonyPatch]
internal class RenderFramePatch
{
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(CardActor), nameof(CardActor.OnRender))]
    internal static IEnumerable<CodeInstruction> OnRenderMpb(IEnumerable<CodeInstruction> instructions)
    {
        var cm = new CodeMatcher(instructions);
        return cm
            .End()
            .MatchStartBackwards(
                new CodeMatch(i => i.IsLdloc()),
                new CodeMatch(i => i.opcode == OpCodes.Ldfld && i.operand.ToString().Contains("frame")),
                new CodeMatch(OpCodes.Ldc_I4_1))
            .ThrowIfInvalid("replace sprite data")
            .Advance(1)
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_1),
                Transpilers.EmitDelegate(GetCurrentAnimatedData),
                new CodeInstruction(OpCodes.Dup),
                new CodeInstruction(OpCodes.Stloc_S, cm.InstructionAt(-1).operand))
            .InstructionEnumeration();
    }

    private static SpriteData GetCurrentAnimatedData(SpriteData current, CardActor actor, RenderParam p)
    {
        var owner = actor.owner;
        if (owner is null) {
            return current;
        }

        // PCC 主角由 PccRenderPatch 覆盖，避免双重推进
        if (actor is CharaActorPCC) {
            return current;
        }

        if (owner is not Chara chara) {
            return current;
        }

        var suffixes = AcsSuffixes.Resolve(chara);
        if (suffixes is null) {
            return current;
        }

        var data = chara.GetAcsClip(p.snow, suffixes);
        if (data is not null) {
            if (AcsStateResolver.IsGreetActive(chara) && data.frame > 0) {
                int spriteIndex = AccessTools.FieldRefAccess<CardActor, int>("spriteIndex")(actor);
                if (spriteIndex >= data.frame - 1) {
                    AcsStateResolver.StopGreet(chara);
                }
            }
            return data;
        }

        return current;
    }
}
