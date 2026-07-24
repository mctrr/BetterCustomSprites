using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ACS.API;
using HarmonyLib;
using UnityEngine;

namespace ACS.Patches;

/// <summary>
/// Skin 扫描与后缀加载：ListSkins 合并 ACS 文件、Reload 解析 _acs_ 后缀。
/// </summary>
[HarmonyPatch]
internal class SkinListPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SpriteReplacer), nameof(SpriteReplacer.ListSkins))]
    internal static void OnListSkins(Dictionary<string, SpriteReplacer> __result)
    {
        var acsSkinIds = __result.Keys
            .Where(id => id.Contains("_acs_"))
            .ToArray();
        foreach (var acsSkinId in acsSkinIds) {
            MergeAcsSkinIntoBase(acsSkinId, __result);
        }

        foreach (var entry in __result) {
            NormalizeAcsSuffixes(entry.Value, entry.Key);
        }

        // ListSkins 完成后立刻给已在场的 PC 重绑
        try {
            PcSkinBinder.Ensure("ListSkins", forceListSkins: false);
        }
        catch (Exception) {
            // 主菜单等尚未有 pc
        }
    }

    /// <summary>
    /// 把 ACS 格式的 skin（如 pcr_jinghua_acs_idle#200_0-3）合并到基础 skin（pcr_jinghua）的 suffixes 中。
    /// </summary>
    private static void MergeAcsSkinIntoBase(string acsSkinId, Dictionary<string, SpriteReplacer> dictSkins)
    {
        int acsIdx = acsSkinId.IndexOf("_acs_");
        if (acsIdx < 0) {
            return;
        }

        string baseId = acsSkinId.Substring(0, acsIdx);
        string acsPart = acsSkinId.Substring(acsIdx);

        var clip = AcsClip.CreateFromFormat(acsPart);
        if (clip is not { Length: > 0 }) {
            return;
        }

        if (!dictSkins.TryGetValue(acsSkinId, out var acsSkin)) {
            return;
        }

        var acsData = acsSkin.suffixes.TryGetValue("");
        if (acsData is null) {
            return;
        }

        // 文件名优先；覆盖错误/过期 ini，避免整条图当 1 帧导致巨大
        ApplyClipMeta(acsData, clip);

        if (!dictSkins.TryGetValue(baseId, out var baseSkin)) {
            // 无底图时用 ACS skin 充当 base，保证 LayerEditSkin 列表可显示
            baseSkin = acsSkin;
            baseSkin.data = acsData;
            dictSkins[baseId] = baseSkin;
            dictSkins.Remove(acsSkinId);
        }

        string suffixKey = $"_acs_{clip.Name}";
        baseSkin.suffixes[suffixKey] = acsData;
        baseSkin.suffixes[AcsController.ReservedSuffix] = null;

        dictSkins.Remove(acsSkinId);

        AcsMod.Log($"skin '{baseId}' clip '{clip.Name}' with {clip.Length} frames @ {clip.Interval}ms (from {acsSkinId})");
    }

    /// <summary>
    /// 长格式 _acs_idle#41_0-31 → 短 key _acs_idle + ApplyClipMeta。
    /// 短 key 解析失败绝不能 Remove，否则只剩 ReservedSuffix、画面仍是 PCC。
    /// 短 key 仍可从 data.path 文件名恢复 #interval_begin-end（Reload 后常见）。
    /// </summary>
    internal static void NormalizeAcsSuffixes(SpriteReplacer replacer, string logId)
    {
        if (replacer?.suffixes is null || replacer.suffixes.Count == 0) {
            return;
        }

        var acsSuffixes = replacer.suffixes
            .Where(kv => kv.Key != null && kv.Key.StartsWith("_acs_"))
            .ToArray();
        if (acsSuffixes.Length == 0) {
            return;
        }

        foreach (var entry in acsSuffixes) {
            var suffix = entry.Key;
            var data = entry.Value;
            var clip = ResolveClip(suffix, data);
            if (clip is not { Length: > 0 }) {
                // 短 key 且 path 也解析不出：保留条目，勿 Remove
                continue;
            }

            if (data is not null) {
                ApplyClipMeta(data, clip);
            }

            string shortKey = $"_acs_{clip.Name}";
            replacer.suffixes[shortKey] = data;
            replacer.suffixes[AcsController.ReservedSuffix] = null;

            if (suffix != shortKey) {
                replacer.suffixes.Remove(suffix);
            }

            AcsMod.Log($"skin '{logId}' clip '{clip.Name}' with {clip.Length} frames @ {clip.Interval}ms");
        }
    }

    /// <summary>
    /// 优先后缀键；短键（_acs_move）则从 path 文件名取 _acs_move#41_0-14。
    /// </summary>
    internal static AcsClip? ResolveClip(string suffix, SpriteData? data)
    {
        var clip = AcsClip.CreateFromFormat(suffix);
        if (clip is { Length: > 0 }) {
            return clip;
        }

        return ResolveClipFromPath(data);
    }

    /// <summary>
    /// 仅从 path 文件名解析 ACS clip（显示热路径 / LoadAnimationIni 后校正用）。
    /// </summary>
    internal static AcsClip? ResolveClipFromPath(SpriteData? data)
    {
        if (data is null || data.path.IsEmpty()) {
            return null;
        }

        try {
            string file = Path.GetFileNameWithoutExtension(data.path);
            int acsIdx = file.IndexOf("_acs_", StringComparison.Ordinal);
            if (acsIdx < 0) {
                return null;
            }

            return AcsClip.CreateFromFormat(file.Substring(acsIdx));
        }
        catch (Exception) {
            return null;
        }
    }

    /// <summary>
    /// 文件名解析的帧数优先于 .ini。
    /// 错误 frame（常见 1）会把整条横图当单帧 → move 1920 宽整条显示，远大于 idle 128 格。
    ///
    /// 关键：禁止 data.GetSprites() 做强制重切。
    /// GetSprites → Load → LoadAnimationIni 会先读 ini 再 LoadSprites；
    /// 若 ini 缺失/解析失败会把 frame 打成 1，随后 sprites 按整条 strip 创建，
    /// 即使之后再改 data.frame，已有 sprites 也不会自动重切。
    /// 正确做法：写好 frame 后直接 LoadSprites()。
    /// </summary>
    internal static void ApplyClipMeta(SpriteData data, AcsClip clip)
    {
        if (data is null || clip is null || clip.Length <= 0) {
            return;
        }

        data.frame = clip.Length;
        data.time = clip.Interval / 1000f;
        data.scale = 100;

        try {
            if (!data.path.IsEmpty()) {
                File.WriteAllLines($"{data.path}.ini", [
                    $"frame={data.frame}",
                    $"time={data.time}",
                    $"scale={data.scale}",
                ]);
            }
        }
        catch (Exception) {
            // 只读路径等：内存 frame 仍正确
        }

        ForceReloadSprites(data);
    }

    /// <summary>
    /// 按当前 data.frame 直接重切，不经过 LoadAnimationIni。
    /// </summary>
    internal static void ForceReloadSprites(SpriteData data)
    {
        if (data is null) {
            return;
        }

        try {
            data.sprites = null;
            data.LoadSprites();
            // 功能3：ACS 纹理用 Point，避免 Bilinear 软边（不改 scale/位置）
            ApplyPointFilter(data);
        }
        catch (Exception) {
            // path 未就绪等：留给显示路径再试
        }
    }

    /// <summary>
    /// 像素风：ACS/静态 skin 纹理强制 Point。
    /// 与 PCC 部件 SpriteVariation 的 Point 路径对齐；不改尺寸与 pivot。
    /// </summary>
    internal static void ApplyPointFilter(SpriteData data)
    {
        if (data is null) {
            return;
        }

        try {
            if (data.tex is not null && data.tex.filterMode != FilterMode.Point) {
                data.tex.filterMode = FilterMode.Point;
            }

            var sprites = data.sprites;
            if (sprites is null) {
                return;
            }

            for (int i = 0; i < sprites.Length; i++) {
                var s = sprites[i];
                if (s is null || s.texture is null) {
                    continue;
                }

                if (s.texture.filterMode != FilterMode.Point) {
                    s.texture.filterMode = FilterMode.Point;
                }
            }
        }
        catch (Exception) {
        }
    }

    /// <summary>
    /// 显示前：用文件名校正 frame/scale，并在切片缺失或长度不符时重切。
    /// 解决「frame 已改但 sprites 仍是整条 strip」的残留状态。
    /// </summary>
    internal static void EnsureAcsSprites(SpriteData data)
    {
        if (data is null || data.path.IsEmpty()) {
            return;
        }

        else {
            // 已切片：仍补一次 Point（冷启动/原版 Load 可能是 Bilinear）
            ApplyPointFilter(data);
        }
        var clip = ResolveClipFromPath(data);
        if (clip is not { Length: > 0 }) {
            return;
        }

        bool metaDirty = data.frame != clip.Length
            || data.scale != 100
            || data.time <= 0f;

        if (metaDirty) {
            data.frame = clip.Length;
            data.time = clip.Interval / 1000f;
            data.scale = 100;
        }

        bool needSlice = data.sprites is null
            || data.sprites.Length != data.frame
            || data.sprites.Length == 0
            || data.sprites[0] is null;

        // 额外：单帧但纹理宽明显是多格横条（frame 曾为 1 时切出的整条）
        if (!needSlice && data.sprites is { Length: 1 } && data.sprites[0] is not null) {
            try {
                var s0 = data.sprites[0];
                if (s0.texture is not null
                    && s0.rect.width > s0.texture.height * 1.5f
                    && clip.Length > 1) {
                    needSlice = true;
                }
            }
            catch (Exception) {
            }
        }

        if (metaDirty || needSlice) {
            ForceReloadSprites(data);
        }
    }
}

/// <summary>
/// 原版 LoadAnimationIni 无文件时 frame=1；有文件也可能被错误 ini 覆盖。
/// ACS 路径在读完 ini 后立刻用文件名帧数盖回去，避免后续 LoadSprites 切成整条。
/// </summary>
[HarmonyPatch(typeof(SpriteData), nameof(SpriteData.LoadAnimationIni))]
internal static class SpriteDataLoadAnimationIniPatch
{
    [HarmonyPostfix]
    internal static void Postfix(SpriteData __instance)
    {
        if (__instance is null || __instance.path.IsEmpty()) {
            return;
        }

        // 快速过滤：path 不含 _acs_ 则不是 ACS 横条
        if (__instance.path.IndexOf("_acs_", StringComparison.Ordinal) < 0) {
            return;
        }

        var clip = SkinListPatch.ResolveClipFromPath(__instance);
        if (clip is not { Length: > 0 }) {
            return;
        }

        if (__instance.frame != clip.Length) {
            __instance.frame = clip.Length;
        }

        __instance.time = clip.Interval / 1000f;
        if (__instance.scale != 100) {
            __instance.scale = 100;
        }
    }
}

/// <summary>
/// Beta：Reload(string id, RenderData) 后补齐 ACS 后缀；无底图种族也可从 dictModItems 建后缀。
/// </summary>
[HarmonyPatch]
internal class ReloadSuffixPatch
{
    private static readonly FieldInfo SortedModIdsField =
        AccessTools.Field(typeof(SpriteReplacer), "sortedModIds");

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SpriteReplacer), nameof(SpriteReplacer.Reload))]
    internal static void OnReloadSuffixes(SpriteReplacer __instance, string id, RenderData renderData)
    {
        // 无底图 PCC 种族仍可能在 dictModItems 有 _acs_ 贴图
        if (__instance.suffixes.Count == 0) {
            var dictModItems = SpriteReplacer.dictModItems;
            if (dictModItems is null || id.IsEmpty()) {
                return;
            }

            bool hasAcsTexture = dictModItems.Keys.Any(k =>
                k != null && k.StartsWith(id) && k.Contains("_acs_"));
            if (hasAcsTexture) {
                try {
                    __instance.SortModItemIds();
                    var sorted = SortedModIdsField?.GetValue(null) as List<string>;
                    if (sorted is null) {
                        sorted = dictModItems.Keys.OrderBy(k => k).ToList();
                    }
                    __instance.BuildSuffixData(id, dictModItems, sorted);
                }
                catch (Exception ex) {
                    AcsMod.Warn($"BuildSuffixData fallback failed for '{id}': {ex.Message}");
                }
            }
        }

        if (__instance.suffixes.Count == 0) {
            return;
        }

        SkinListPatch.NormalizeAcsSuffixes(__instance, id);
    }
}
