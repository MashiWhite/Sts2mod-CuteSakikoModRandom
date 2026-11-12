using ChaosCardGenerator;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// P1 只读估值器：把一张 <see cref="GeneratedCard"/> 的 operations 按 <see cref="AtomVpTable"/>
/// 折算成完整 VP，用于和 <see cref="CardValueProfile"/> 的目标带比较。
///
/// ★ 只用于日志，**不改任何 operation**，也不注册给 AA。
/// ★ 触发型（模板以 `A:` 开头）按 0.75 折：它们不是打出时立即生效，
///   真实收益取决于回合数；这里只做保守估计。
/// ★ 关键词给小额固定值；先不做逐关键词的精确建模（P4 再调）。
/// </summary>
internal static class CardValuator
{
    /// <summary>
    /// 永久性 power 落在非 Power 卡上的 VP 倍率（2026-11-12 用户规则）。
    /// 例：攻击牌上「获得1点力量」、技能牌上「获得1点敏捷」→ 该原子 VP ×1.5。
    /// 「本回合获得 X 点力量」这类临时 power 不变。
    /// </summary>
    internal const double OffTypePermanentPowerMultiplier = 1.5;

    internal static double Evaluate(GeneratedCard card)
    {
        var total = 0d;
        foreach (var operation in card.Operations)
        {
            var spec = OperationRuntimeSpecCompiler.GetOrCompile(operation);
            var value = AtomVpTable.Vp(spec);

            // ★ 永久性 power 不在 Power 卡上 → VP ×1.5（本回合型 power 不享受）。
            if (card.Type != GeneratedCardType.Power && AtomVpTable.IsPermanentPower(spec))
                value *= OffTypePermanentPowerMultiplier;

            if (operation.Template.StartsWith("A:", StringComparison.Ordinal)) value *= 0.75;
            total += value;
        }

        total += KeywordValue(card);
        return Math.Max(0d, total);
    }

    private static double KeywordValue(GeneratedCard card)
    {
        var value = 0d;
        foreach (var tag in card.Tags)
        {
            value += tag switch
            {
                CardTag.Strike => 1.0,
                CardTag.Defend => 0.5,
                CardTag.Innate => 1.0,
                CardTag.Retain => 1.0,
                CardTag.Exhaust => -0.5,
                CardTag.Ethereal => -0.5,
                _ => 0d
            };
        }

        var piano = CuteSakikoMod.CuteSakikoModCode.Others.CutesakiKeywords.Playpiano;
        var guitar = CuteSakikoMod.CuteSakikoModCode.Others.CutesakiKeywords.Playguitar;
        var parfait = CuteSakikoMod.CuteSakikoModCode.Others.CutesakiKeywords.Parfait;
        foreach (var keyword in card.CustomKeywords ?? [])
        {
            if (keyword == piano) value += 0.5;
            else if (keyword == guitar) value += 0.5;
            else if (keyword == parfait) value += 0.5;
        }

        return value;
    }
}
