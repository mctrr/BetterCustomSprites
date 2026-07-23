using System;
using System.Collections.Generic;
using UnityEngine;

namespace ACS.Patches;

/// <summary>
/// PCC 上 ACS/静态 skin 的贴图呈现：scale 约定、底边留白对齐、bottom-pivot 缓存。
/// 不强制 idle/move 同高——作者故意做大/做小的 clip 应原样保留。
/// </summary>
internal static class AcsSpritePresenter
{
    // 100 ppu 下约 10 像素：PCC 站位相对地面的固定浮空补偿（与画幅无关）
    internal const float GroundYOffset = -0.10f;

    private const float PixelsPerUnit = 100f;
    private const byte AlphaThreshold = 10;

    // 源帧 sprite → bottom-pivot 副本
    private static readonly Dictionary<int, Sprite> BottomPivotCache = new Dictionary<int, Sprite>();

    // 源帧 sprite → 底边透明像素数（textureRect 底到首个不透明像素）
    private static readonly Dictionary<int, float> BottomPaddingCache = new Dictionary<int, float>();

    /// <summary>
    /// 将 SpriteData.scale 映射为 PCC 叠加用的世界倍率。
    /// ACS 用 100=1.0；原版静态 skin 用 50=1.0。
    /// 只读作者写的 scale，不做跨 clip 归一。
    /// </summary>
    internal static float ResolveWorldScale(SpriteData data)
    {
        if (data is null || data.scale <= 0) {
            return 1f;
        }

        // ACS 多帧 clip 一律按 /100（ApplyClipMeta 固定写 100）
        if (data.frame > 1 || data.scale >= 100) {
            return data.scale / 100f;
        }

        // 静态 / 单帧：原版约定 scale 50 = 正常体型
        return data.scale / 50f;
    }

    /// <summary>
    /// 地面 Y 偏移 = PCC 浮空补偿 + 本帧底边透明留白（世界单位）。
    /// 128×128 切片高度相同不代表脚底相同；只扫当前帧 rect 的底边 alpha。
    /// </summary>
    /// <param name="source">当前显示帧（切片后的单帧 Sprite，非整条 strip）</param>
    /// <param name="worldScaleY">已应用到 transform 的 Y 缩放（含 SubPass × scale）</param>
    internal static float ResolveGroundYOffset(Sprite? source, float worldScaleY)
    {
        float y = GroundYOffset;
        if (source is null) {
            return y;
        }

        float padPx = MeasureBottomPaddingPx(source);
        if (padPx <= 0f) {
            return y;
        }

        // bottom-pivot 时 pivot 在 rect 底边；底边透明 → 视觉脚底高于 pivot，需再下沉
        float scaleY = worldScaleY == 0f ? 1f : Mathf.Abs(worldScaleY);
        y -= (padPx / PixelsPerUnit) * scaleY;
        return y;
    }

    /// <summary>
    /// 当前帧 textureRect 内，从底边向上数连续透明行数（像素）。
    /// 横条 strip 按帧切开后每帧各自量；同为 128 高时仍能区分脚底留白。
    /// </summary>
    internal static float MeasureBottomPaddingPx(Sprite source)
    {
        if (source is null || source.texture is null) {
            return 0f;
        }

        int key = source.GetInstanceID();
        if (BottomPaddingCache.TryGetValue(key, out float cached)) {
            return cached;
        }

        float pad = 0f;
        try {
            Texture2D tex = source.texture;
            Rect r = source.textureRect;
            int x0 = Mathf.FloorToInt(r.x);
            int y0 = Mathf.FloorToInt(r.y);
            int w = Mathf.Max(1, Mathf.FloorToInt(r.width));
            int h = Mathf.Max(1, Mathf.FloorToInt(r.height));

            Color32[]? pixels = null;
            try {
                pixels = tex.GetPixels32();
            }
            catch (Exception) {
                BottomPaddingCache[key] = 0f;
                return 0f;
            }

            if (pixels is null || pixels.Length == 0) {
                BottomPaddingCache[key] = 0f;
                return 0f;
            }

            int texW = tex.width;
            int stepX = Math.Max(1, w / 32);

            // Unity 纹理 y=0 在底；textureRect.y 是帧底边
            int opaqueMinY = int.MaxValue;
            for (int y = 0; y < h; y++) {
                int row = (y0 + y) * texW;
                bool any = false;
                for (int x = 0; x < w; x += stepX) {
                    int idx = row + x0 + x;
                    if (idx < 0 || idx >= pixels.Length) {
                        continue;
                    }

                    if (pixels[idx].a > AlphaThreshold) {
                        any = true;
                        break;
                    }
                }

                if (any) {
                    opaqueMinY = y;
                    break;
                }
            }

            if (opaqueMinY != int.MaxValue && opaqueMinY > 0) {
                pad = opaqueMinY;
            }
        }
        catch (Exception) {
            pad = 0f;
        }

        BottomPaddingCache[key] = pad;
        return pad;
    }

    /// <summary>源帧 → 底轴 pivot 的 Sprite（缓存，避免每帧 Create）。</summary>
    internal static Sprite? GetBottomPivotSprite(Sprite source)
    {
        if (source is null || source.texture is null) {
            return null;
        }

        int key = source.GetInstanceID();
        if (BottomPivotCache.TryGetValue(key, out var cached)
            && cached != null
            && cached.texture != null) {
            return cached;
        }

        Sprite created = Sprite.Create(
            source.texture,
            source.textureRect,
            new Vector2(0.5f, 0f),
            PixelsPerUnit,
            0u,
            SpriteMeshType.FullRect);

        if (created is null || created.texture is null) {
            return null;
        }

        BottomPivotCache[key] = created;
        return created;
    }

    /// <summary>仅作者 scale → 世界倍率；不跨 clip 压身高。</summary>
    internal static float ComputeLocalScaleMultiplier(Chara? owner, SpriteData data)
    {
        _ = owner;
        return ResolveWorldScale(data);
    }
}
