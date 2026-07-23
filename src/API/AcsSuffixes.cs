using System.Collections.Generic;

namespace ACS.API;

/// <summary>
/// suffixes 上真实 _acs_* clip 的统一查询（不含 ReservedSuffix 占位）。
/// </summary>
public static class AcsSuffixes
{
    /// <summary>是否存在至少一条真实 _acs_* 数据（Value 非 null）。</summary>
    public static bool HasRealClip(Dictionary<string, SpriteData>? suffixes)
    {
        if (suffixes is null) {
            return false;
        }

        foreach (var kv in suffixes) {
            if (kv.Key is null || kv.Value is null) {
                continue;
            }

            if (kv.Key.StartsWith("_acs_")) {
                return true;
            }
        }

        return false;
    }

    public static bool HasRealClip(SpriteReplacer? replacer)
        => HasRealClip(replacer?.suffixes);

    /// <summary>
    /// 解析角色可用的 ACS suffixes：优先 skin，再种族；无真实 clip 返回 null。
    /// </summary>
    public static Dictionary<string, SpriteData>? Resolve(Chara? owner)
    {
        if (owner is null) {
            return null;
        }

        var suffixes = owner.spriteReplacer?.suffixes ?? owner.sourceCard?.replacer?.suffixes;
        return HasRealClip(suffixes) ? suffixes : null;
    }

    public static int CountClips(Dictionary<string, SpriteData>? suffixes)
    {
        if (suffixes is null) {
            return 0;
        }

        int n = 0;
        foreach (var key in suffixes.Keys) {
            if (key != null && key.StartsWith("_acs_")) {
                n++;
            }
        }

        return n;
    }

    public static int CountClips(SpriteReplacer? replacer)
        => CountClips(replacer?.suffixes);
}
