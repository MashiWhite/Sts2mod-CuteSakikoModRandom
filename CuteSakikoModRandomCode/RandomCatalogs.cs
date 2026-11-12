using ChaosCardGenerator;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>构造 profile 时共用的小工具。</summary>
internal static class RandomCatalogs
{
    /// <summary>
    /// 每个角色允许生成哪些原版关键词。按本体真实卡牌用到的收敛，
    /// 免得生成出「小祥不该有的 Sly / Osty」之类。
    /// </summary>
    /// <param name="customBaseKeywords">
    /// 本角色允许出现的**自定义关键词**（本体 mod 的关键词，由调用方从
    /// CutesakiKeywords 取）。留空则不加自定义关键词。
    ///
    /// ★ 2026-11-09 用户要求「琴奏/弦奏相当于原版的打击，要带上对应的 tag 和关键词」：
    ///   本体里「琴奏」= CardTag.Strike + 关键词 Playpiano，
    ///   「弦奏」= CardTag.Strike + 关键词 Playguitar。
    ///   这里把它作为允许的基础关键词传进去，随机卡就会带上对应关键词。
    ///
    /// 注意：参数化而不是直接引用 CutesakiKeywords，是为了让本文件能被离线自检工程
    /// 编译（那个工程不引用 CuteSakikoMod.dll）。
    /// </param>
    internal static ComponentKeywordPolicy KeywordPolicy(string profileId,
        IReadOnlySet<string>? customBaseKeywords = null) => new(
        AllowedBaseKeywords: new HashSet<CardTag>
        {
            CardTag.Exhaust, CardTag.Innate, CardTag.Retain, CardTag.Ethereal
        },
        AllowedUpgradeAdditions: new HashSet<CardTag> { CardTag.Innate, CardTag.Retain },
        AllowedUpgradeRemovals: new HashSet<CardTag> { CardTag.Exhaust, CardTag.Ethereal },
        AllowedCustomBaseKeywords: customBaseKeywords?.Count > 0 ? customBaseKeywords : null,
        UseArchetypeUpgradeDefaults: false);
}
