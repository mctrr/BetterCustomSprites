using System.Collections.Generic;
using System.Reflection.Emit;
using ACS.API;
using HarmonyLib;

namespace ACS.Patches;

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
        if (!owner.sourceCard.replacer.suffixes.ContainsKey(AcsController.ReservedSuffix)) {
            return current;
        }

        if (owner is Chara chara) {
            var data = chara.GetAcsClip(null, p.snow);
            if (data is not null) {
                return data;
            }
        }

        return current;
    }
}