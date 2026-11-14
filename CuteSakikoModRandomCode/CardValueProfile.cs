using ChaosCardGenerator;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>目标价值带（VP）：Low/High = 常规带，Center = 本体卡中位。</summary>
internal readonly record struct ValueBand(double Low, double High, double Center)
{
    public bool Contains(double value) => value >= Low && value <= High;
}

/// <summary>
/// P0 校准后的「本体卡同稀有度/同费用」目标带（2026-11-11）。
/// 数据源：E:\SLJT2\任务计划\东尼算法\资料\P0目标价值表.md（254 张本体卡，208 有值，53 张人工校准）。
///
/// 常规带 = 中位 ±25%；表里只存中位，带宽在 For() 里算。
/// 费用 >4：按 4 费中位 + 每点 +5 VP 外推；X 费返回 null（不做目标带判断）。
/// 0 费：P0 样本 <10 张，P4 按「基准表 0~8 + 稀有度梯度」人工校准为
/// Basic 6 / Common 4.5 / Uncommon 6 / Rare 7.5 / Ancient 14。
/// </summary>
internal static class CardValueProfile
{
    private static readonly Dictionary<(GeneratedRarity Rarity, int Cost), double> Centers = new()
    {
        [(GeneratedRarity.Basic, 0)] = 6.0,
        [(GeneratedRarity.Basic, 1)] = 5.0,

        [(GeneratedRarity.Common, 0)] = 4.5,
        [(GeneratedRarity.Common, 1)] = 8.0,
        [(GeneratedRarity.Common, 2)] = 12.0,
        [(GeneratedRarity.Common, 3)] = 18.8,
        [(GeneratedRarity.Common, 4)] = 12.5,

        [(GeneratedRarity.Uncommon, 0)] = 6.0,
        [(GeneratedRarity.Uncommon, 1)] = 8.4,
        [(GeneratedRarity.Uncommon, 2)] = 12.6,
        [(GeneratedRarity.Uncommon, 3)] = 19.7,
        [(GeneratedRarity.Uncommon, 4)] = 25.0,

        [(GeneratedRarity.Rare, 0)] = 7.5,
        [(GeneratedRarity.Rare, 1)] = 9.2,
        [(GeneratedRarity.Rare, 2)] = 13.9,
        [(GeneratedRarity.Rare, 3)] = 21.7,
        [(GeneratedRarity.Rare, 4)] = 27.5,

        [(GeneratedRarity.Ancient, 0)] = 14.0,
        [(GeneratedRarity.Ancient, 1)] = 12.5,
        [(GeneratedRarity.Ancient, 2)] = 18.7,
        [(GeneratedRarity.Ancient, 3)] = 29.2,
        [(GeneratedRarity.Ancient, 4)] = 37.1
    };

    /// <summary>常规带（中位 ±25%）；X 费返回 null。</summary>
    internal static ValueBand? For(GeneratedRarity rarity, int cost)
    {
        if (cost < 0) return null;
        var center = CenterFor(rarity, cost);
        return new ValueBand(center * 0.75, center * 1.25, center);
    }

    private static double CenterFor(GeneratedRarity rarity, int cost)
    {
        if (Centers.TryGetValue((rarity, cost), out var center)) return center;

        // 4 费以上：每多 1 费 +5 VP（现有本体样本只有 4c 一张，属于外推）。
        if (cost > 4)
        {
            var baseCenter = Centers.TryGetValue((rarity, 4), out var four)
                ? four
                : Centers[(rarity, 1)];
            return baseCenter + (cost - 4) * 5.0;
        }

        // 0~4 费缺格：退回同稀有度 1 费中位（Basic/Common 用 1c 是安全兜底）。
        return Centers.TryGetValue((rarity, 1), out var one) ? one : 8.0;
    }
}
