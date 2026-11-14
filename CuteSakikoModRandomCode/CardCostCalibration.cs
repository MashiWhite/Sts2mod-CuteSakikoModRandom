using ChaosCardGenerator;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 费用校正：按「实际效果强度」微调费用，修掉「0 费超模 / 高费超废」。
///
/// 来由（2026-11-10 离线模拟开局）：卡的费用来自**壳表**，效果强度来自
/// **稀有度预算**，两者会脱节，实测出现「0 费造成 20 点伤害」「2 费只给 13 金币」。
/// 这里只做极端情况的兜底，不做精细平衡（那是数值基准表的职责）。
/// </summary>
internal static class CardCostCalibration
{
    /// <summary>强度分低于这个值就算「这么贵不值」。</summary>
    private const double WeakThreshold = 4.0;

    /// <summary>强度分高于这个值就不该是 0 费。</summary>
    private const double ZeroCostThreshold = 12.0;

    internal static GeneratedCard Apply(GeneratedCard card)
    {
        var score = Score(card);
        // 0 费太强 → 提到 1 费
        if (card.Cost == 0 && score >= ZeroCostThreshold) return card with { Cost = 1 };
        // 2 费太弱 → 降到 1 费
        if (card.Cost == 2 && score <= WeakThreshold) return card with { Cost = 1 };
        // 3 费 / 4 费明显不值 → 降到 1 费（实测出现过「4 费只给 6 点敏捷」）
        if (card.Cost >= 3 && score <= 12) return card with { Cost = 1 };
        if (card.Cost >= 3 && score <= 20) return card with { Cost = 2 };
        return card;
    }

    internal static double Score(GeneratedCard card)
    {
        var score = 0d;
        foreach (var operation in card.Operations)
        {
            var spec = OperationRuntimeSpecCompiler.GetOrCompile(operation);
            var value = spec.Values.Count > 0 ? spec.Values[0].BaseValue : 0;
            // 双槽原子要能读到第二个槽（例如「消耗N层压力 → 永久M点最大生命」）
            var second = spec.Values.Count > 1 ? spec.Values[1].BaseValue : 0;
            score += OpScore(spec.Opcode, spec.Variant, value, second);
        }
        return score;
    }

    /// <summary>
    /// 单个操作的强度分（**带符号**）：代价为负、收益为正，同一原子两者都有就分段相加。
    ///
    /// 2026-11-10 用户口径：「花费金币是负面价值应该扣分，获得金币才是正面价值加分」。
    /// 这条推广到所有「花掉资源换效果」的原子。
    /// </summary>
    private static double OpScore(string opcode, string variant, int value, int second) => opcode switch
    {
        // ── 资源（正负都看 variant / 语义）─────────────────────────────
        // 金币：gain = 加；花费 = 减
        Gold => value * GoldScorePerPoint,

        // 芭菲：吃/充能是收益；**买**是「得 1 杯 − 付掉的价」；
        //       **消耗手牌视为食用**是「得 N 杯 − 消耗的手牌」（手牌每张按 2 分）
        Parfait => variant switch
        {
            SakiComponents.ParfaitBuyVariant => ParfaitCupScore - value * GoldScorePerPoint,
            SakiComponents.ConsumeAsParfaitVariant =>
                value * ParfaitCupScore - value * HandCardCost,
            _ => ParfaitCupScore
        },

        // ★★ 压力支付：**压力是代价 → 必须扣分**，再按 variant 加上换来的收益。
        //   之前一律按 value×2.0 算，会把「消耗 17 层压力只抽 1 张」算成强卡。
        PayPressure => PayPressurePayoff(variant, value) - value * PressureCost,

        // ── 明确的代价（负分）─────────────────────────────────────────
        "lose_hp" => -value * 1.0,
        "discard" => -value * HandCardCost,          // N:Discard
        "exhaust" => -value * HandCardCost * 1.2,    // N:Exhaust

        // 丢弃/消耗手牌类复合原子：代价（手牌）+ 收益（同原子里的 payoff）
        //   value 是 payoff 的槽，手牌代价按「全丢」估一个常量
        "csk_deck" => variant switch
        {
            // 消耗压力换永久最大生命：首槽是**花掉的压力**（代价），次槽才是收益
            SakiComponents.PressureMaxHpVariant => second * 8.0 - value * PressureCost,
            SakiComponents.DiscardAllVariant => value * 0.6 - HandCardCost * 2,
            SakiComponents.ForgetAllHandVariant => value * 2.5 - HandCardCost * 2,
            SakiComponents.DiscardNonParfaitVariant => value * 2.0 - HandCardCost,
            SakiComponents.ExhaustStatusVariant => value * 3.0,   // 消耗状态牌是**净化**，纯收益
            SakiComponents.ConsumeAsParfaitVariant => value * 2.0,
            _ => value * 2.0
        },

        // 猫咪消耗：吃掉猫咪换伤害（猫咪是资源 → 净值）
        "csk_token" => variant is SakiComponents.TokenConsumeNekoDamageVariant
            ? value * 2.0 - NekoCardCost
            : value * 2.0,

        // ── 纯收益（正分）────────────────────────────────────────────
        "deal_damage" => value * 1.0,
        "gain_block" => value * 0.6,
        "draw_cards" => value * 4.0,
        "csk_energy" => value * 8.0,
        // 自增压力是「增幅资源」，比给敌人压力略低（与 SakiValuations 的 0.8× 对齐）；
        // 伤害 + 压力联动的三种变体按伤害计分。
        "csk_pressure" => variant switch
        {
            SakiComponents.PressureSelfVariant => value * 0.65,
            SakiComponents.DoubleTargetPressureVariant
                or SakiComponents.SpreadTargetPressureVariant
                or SakiComponents.ConsumeTargetPressureHitsVariant => value * 1.0,
            _ => value * 0.8
        },
        "csk_scale" => value * 2.5,
        "csk_power" => value * 3.0,
        "csk_play_chord" => value * 3.0,
        "csk_note" => value * 1.5,
        "csk_condition" => value * 1.5,
        // ★★ 结束你的回合 = **大负面**（2026-11-10 用户要求）
        //   代价极大（白送一个回合），按 −18 记，基本一定被降费。
        "csk_extreme" => value * 1.5 - 12.0,
        // 塞状态牌是负面
        "csk_status" => -value * 3.0,
        _ => value * 0.8
    };

    /// <summary>支付压力后换来的收益（按变体估）。</summary>
    private static double PayPressurePayoff(string variant, int value) => variant switch
    {
        SakiComponents.PayPressureDamageVariant => value * 2.0,   // 1 层压力 ≈ 2 点伤害
        SakiComponents.PayPressureBlockVariant => value * 1.0,    // 1 层压力 ≈ 1 点格挡
        SakiComponents.PayPressureDrawVariant => Math.Min(value * 0.4, 6),  // 抽牌不会随压力线性涨
        SakiComponents.PayPressureWeakAllVariant => value * 0.5,
        SakiComponents.PayPressureRecallVariant => value * 2.0,
        _ => value * 1.0
    };

    /// <summary>1 杯抹茶芭菲大约值多少分（用于「买芭菲」这种「付钱换杯数」的净价计算）。</summary>
    private const double ParfaitCupScore = 6.0;

    /// <summary>1 点金币折算多少强度分 —— 花费时减、获得时加。</summary>
    private const double GoldScorePerPoint = 0.15;

    /// <summary>1 层压力作为**代价**折算多少分。</summary>
    private const double PressureCost = 0.6;

    /// <summary>1 张手牌作为**代价**折算多少分。</summary>
    private const double HandCardCost = 2.0;

    /// <summary>1 张猫咪牌作为**代价**折算多少分。</summary>
    private const double NekoCardCost = 2.0;

    private const string Parfait = "csk_parfait";
    private const string Gold = "csk_gold";
    private const string PayPressure = "csk_pay_pressure";
}






