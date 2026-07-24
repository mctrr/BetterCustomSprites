using System.Collections.Generic;
using System.Reflection.Emit;
using ACS.API;
using HarmonyLib;

namespace ACS.Patches;

/// <summary>
/// 原版 <see cref="CardActor.OnRender"/> 只播放 replacer.data 的多帧动画。
/// 本 patch 用 IL 注入，在帧推进逻辑读到 SpriteData 时，换成当前 ACS clip，
/// 让 NPC / 非 PCC 角色也能按 combat、greet 等状态播动画。
///
/// 主角（PCC）由 <see cref="PccRenderPatch"/> 单独覆盖 SpriteRenderer，
/// 这里对 CharaActorPCC 直接 return current，避免两套逻辑双重推进帧。
/// </summary>
[HarmonyPatch]
internal class RenderFramePatch
{
    /// <summary>
    /// 缓存 private 字段访问器。
    /// 若每次 AccessTools.FieldRefAccess 都会反射查找，greet 结束检测会变贵。
    /// </summary>
    private static readonly AccessTools.FieldRef<CardActor, int> SpriteIndexRef =
        AccessTools.FieldRefAccess<CardActor, int>("spriteIndex");

    /// <summary>
    /// Transpiler：在原版「读取 data.frame」附近插入委托调用，
    /// 把局部变量里的 SpriteData 替换为 GetCurrentAnimatedData 的返回值。
    /// 具体 IL 匹配依赖原版方法结构；游戏大版本更新后若 patch 失败会 ThrowIfInvalid。
    /// </summary>
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

    /// <summary>
    /// 运行时：给定原版 current 数据，返回应播放的 ACS 数据；没有 ACS 则原样返回 current。
    /// 参数 actor / p 由 IL 注入的 Ldarg 传入。
    /// </summary>
    private static SpriteData GetCurrentAnimatedData(SpriteData current, CardActor actor, RenderParam p)
    {
        var owner = actor.owner;
        if (owner is null) {
            return current;
        }

        // PCC 主角整段渲染由 PccRenderPatch 接管
        if (actor is CharaActorPCC) {
            return current;
        }

        if (owner is not Chara chara) {
            return current;
        }

        // 无真实 _acs_* 时不要改原版动画
        var suffixes = AcsSuffixes.Resolve(chara);
        if (suffixes is null) {
            return current;
        }

        var data = chara.GetAcsClip(p.snow, suffixes);
        if (data is not null) {
            // greet 播到最后一帧附近时清标记，下次回到 idle/其它状态
            if (AcsStateResolver.IsGreetActive(chara) && data.frame > 0) {
                int spriteIndex = SpriteIndexRef(actor);
                if (spriteIndex >= data.frame - 1) {
                    AcsStateResolver.StopGreet(chara);
                }
            }

            return data;
        }

        return current;
    }
}
