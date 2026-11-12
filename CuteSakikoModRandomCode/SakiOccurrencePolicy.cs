using System;
using System.Collections.Generic;
using ChaosCardGenerator;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 出场几率策略（官方 API：IComponentOccurrencePolicy）。
///
/// ★ 2026-11-11 用户口径：**出现几率要靠这个策略调，不能靠把估值压低来骗**。
///   之前为了让芭菲/和声/猫咪/倍率多出现，我们把它们的估值压得很低 →
///   结果 AA 的 RaiseNumericRewardsToTopRarityFloor 把数值抬成 11/22/34。
///   现在估值按文档 VP 还原，出现几率改由这里加权。
///
/// 做法：包一层 AA 的原生策略，只对**我们自己的原子（csk_ 开头）**加权。
/// </summary>
internal sealed class SakiOccurrencePolicy : IComponentOccurrencePolicy
{
    /// <summary>自定义原子的额外权重（1 = 不加权）。</summary>
    private const int CustomBoost = 3;

    private readonly IComponentOccurrencePolicy _inner;

    internal SakiOccurrencePolicy(IComponentOccurrencePolicy inner) => _inner = inner;

    public int SourcePriorWeight(GeneratedRarity rarity, GeneratedCardType type, string family,
        IReadOnlyList<GeneratorOperation> currentCard) =>
        _inner.SourcePriorWeight(rarity, type, family, currentCard);

    public int SelectionWeight(GeneratedRarity rarity, GeneratedCardType type, string family,
        IReadOnlyList<GeneratorOperation> currentCard) =>
        _inner.SelectionWeight(rarity, type, family, currentCard);

    public void Observe(GeneratedCard card) => _inner.Observe(card);

    public int VariantWeight(ComponentAtom atom, GeneratedRarity rarity, GeneratedCardType type, TargetMode target,
        IReadOnlyList<GeneratorOperation> currentCard)
    {
        var weight = _inner.VariantWeight(atom, rarity, type, target, currentCard);
        try
        {
            var opcode = atom.RuntimeSpec?.Opcode ?? string.Empty;
            if (opcode.StartsWith("csk_", StringComparison.Ordinal) && weight > 0)
                weight = Math.Max(1, weight * CustomBoost);
        }
        catch { }

        return weight;
    }
}

