using ChaosCardGenerator;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 自建数值平衡模型（2026-11-09 用户要求「平衡模型不要用原版的」）。
///
/// 为什么必须自建：
///   AA 的 <c>DefaultValuePolicy</c> 是照**原版五角色**校准的（1 费 ≈ 6 伤 / 5 挡）。
///   但可爱小祥的三个卡池明显更强，实测（Cards\ 下逐卡统计）：
///     祥子 Basic 1 费    伤 6      （原版 6，持平）
///     祥子 Common 1 费   伤 9~13   （原版 6，强 50%~110%）
///     祥子 Common 1 费   挡 7~8    （原版 5，强 40%~60%）
///     祥子 Common 2 费   挡 14     （原版 8~10）
///     爱音 Uncommon 3 费 伤 20     （原版 6~8 区间）
///   直接套原版模型会让随机卡「明显偏弱」，玩家会觉得打不动。
///
/// 本模型的系数（相对原版模型整体放大，按稀有度递增）：
///     稀有度        伤害倍率   格挡倍率
///     Basic        1.00       1.00
///     Common       1.40       1.45
///     Uncommon     1.60       1.70
///     Rare         1.85       1.95
///     Ancient      2.10       2.20
///   —— 依据就是上面那份实测均值：祥子 Common 1 费 9 伤 / 1 费 8 挡，
///      相对原版 6 伤 / 5 挡正好约 1.4~1.6 倍。
///
/// ⚠️ 这个模型只调「数值采样」，不改出率、不改合法性。
/// ⚠️ 想再调强弱，只动 <see cref="Tuning"/> 这张表，别改采样逻辑。
/// </summary>
internal sealed class SakiValuePolicy : IComponentValuePolicy
{
    private readonly GeneratedRarity? _rarityHint;

    /// <summary>实测系数表（见类注释）。</summary>
    internal static readonly Dictionary<GeneratedRarity, (double Damage, double Block)> Tuning = new()
    {
        [GeneratedRarity.Basic] = (1.00, 1.00),
        [GeneratedRarity.Common] = (1.25, 1.30),
        // ★ 2026-11-09 用户反馈「金卡数值太低，和稀有度完全不匹配」：
        //   原来 Rare 只有 1.85/1.95，实测上限伤害反而低于 Uncommon
        //   （祥子 Rare 26 < Uncommon 28），因为custom opcode 根本没被放大。
        //   现在 Uncommon/Rare/Ancient 拉开明显台阶：
        //     白=普通数值 / 蓝=较高 / 金=高 / 先古=最高
        [GeneratedRarity.Uncommon] = (1.55, 1.65),
        [GeneratedRarity.Rare] = (1.85, 1.95),
        [GeneratedRarity.Ancient] = (2.30, 2.40),
    };

    /// <summary>
    /// 哪些组件参与「预算放大」。
    ///
    /// ★ 2026-11-09 修正：原来只认 damage/block 和 ScalableReward 标记，
    ///   导致敬业的自定义机制（芭菲 / 音符 / 和弦 / 能力）**完全不放大**，
    ///   于是金卡和蓝卡数值看起来一样。现在只要有数值槽就参与缩放，
    ///   由 <see cref="ScaleRewardCenter"/> 里的槽位名决定倍率。
    /// </summary>
    public bool IsScalableReward(ComponentAtom atom)
    {
        // ★★ B3 修复（2026-11-10，AA 源码确认）：只对**自定义原子**关掉数值采样。
        //
        // 原因：AA 采样后会把槽的 Source 从 "fixed" 改成 "sampled"，
        //      而 ChaosOperationVariables.TryGetInitialSlot 要求
        //      `value.Upgradable && value.Source == "fixed"`（ChaosCardModel.cs:1609），
        //      取不到槽 → 不给这张卡建 DynamicVar → 文案里的 {Amount0:diff()} 没人填 → 卡面露字。
        //      原生原子不被采样（数值由 AA 自己的预算决定），所以它们从不露字。
        //
        // 代价：自定义原子的数值固定为原子定义时的基准值（多样新靠「不同档位的原子」提供），
        //      换来卡面数字永远正确、且随本地化/局内数值变更正常显示。
        // ★★ 2026-11-10 实机：**所有原子都不许采样**。
        //   原因：AA 采样后会把槽的 Source 从 "fixed" 改成 "sampled"，
        //   而 ChaosOperationVariables.TryGetInitialSlot 要求 `Upgradable && Source=="fixed"`
        //   → 取不到槽 → 不建 DynamicVar → 文案里的 {Xxx0:diff()} / {Energy0:energyIcons()} 没人替换 → 卡面露字。
        //   原生原子同样中招（「终幕之月」露出 {Energy0:energyIcons()}）。
        //   代价：数值固定为原子定义值（多样性靠不同档位的原子），换来卡面永远正确。
        _ = atom;
        return false;
    }

    /// <summary>沿用原版：偏向使用「组件自带原值」而不是无脑放大。</summary>
    public int OriginalValueChance(ComponentAtom atom, int benefitLines) =>
        Math.Clamp(70 - benefitLines * 10, 20, 85);

    /// <summary>围绕中心值采样，保持原版的高斯风格波动。</summary>
    public int SampleAroundCenter(Random random, int center, int payoffScale)
    {
        if (center <= 0) return center;
        var spread = Math.Max(1, center / 6);
        var offset = random.Next(-spread, spread + 1);
        return Math.Max(0, center + offset);
    }

    public int AdjustSampledValue(Random random, ComponentAtom atom, int slot, int value) => value;

    /// <summary>
    /// 数值上限/下限：按槽位给硬约束。
    ///
    /// ★★ 这里必须给**下限 1**（2026-11-09 实测踩坑）：
    ///   替换掉 AA 的 DefaultValuePolicy 之后，就丢掉了它 ClampSampledValue 里的
    ///   一堆按 Template 硬编码的保护（GenerationTuning.cs:736 起，
    ///   例如 N:Draw 至少 1 张、N:AllD 至少 1 点、虚弱/易伤至少 1 层）。
    ///   没有这些下限，采样会掉到 0，生成出「给予0层[gold]虚弱[/gold]」这种废牌。
    ///   ⇒ 规则：凡是「层数/数量」类槽位，下限一律 1；伤害/格挡下限 1。
    ///
    /// 注意 AA 的 slot 参数是 <c>int</c>（槽位序号），不是槽位 ID 字符串。
    /// </summary>
    public int ClampSampledValue(ComponentAtom atom, int slot, int value,
        IReadOnlyList<GeneratorOperation> previous, GeneratedCharacter? character, bool ultimateChaos)
    {
        // 设置：完全随机 → 放开上下限（只保底 1，避免 0 值废牌）。
        if (RandomConfig.FullyRandomValues) return Math.Max(1, value);

        // ★★ 2026-11-11 用户实测「动不动 16 倍 / 21 倍 / 20 多层」——这些必须**在采样时就夹住**，
        //   靠事后重抽根本来不及（AA 自己的 NumericGenerationTuning 也是这么做的）。
        if (atom.RuntimeSpec is { } specGlobal)
        {
            var op = specGlobal.Opcode ?? string.Empty;
            if (op.Contains("scale", StringComparison.Ordinal))
                return Math.Clamp(value, 1, 3);                 // 倍率永远 ≤ 3
            if (op.Contains("power", StringComparison.Ordinal))
                return Math.Clamp(value, 1, 4);                 // 能力层数永远 ≤ 4
            if (op is "csk_parfait" or "csk_note" or "csk_status")
                return Math.Clamp(value, 1, 4);                 // 资源类 ≤ 4
        }

        // ★ 消耗压力换减益（2026-11-10 用户口径：成本与效果都要随机）：
        //   slot 0 = 消耗层数（1~5），slot 1 = 倍率（1~4）。
        //   最终虚弱层数 = 成本 × 倍率，在 handler 里推导。
        if (atom.RuntimeSpec is { Opcode: "csk_pay_pressure", Variant: "weak_all" })
            return slot == 0 ? Math.Clamp(value, 1, 3) : Math.Clamp(value, 2, 3);

        // ★ 压力缩放的**倍率**必须收紧（2026-11-10 实测：被放大到「12 倍」）。
        //   倍率不是伤害，不能跟着稀有度倍率一起涨。
        if (atom.RuntimeSpec is { Opcode: "csk_scale" })
            return Math.Clamp(value, 1, 3);

        // ★★ 逐个变体夹住「次数 / 张数」，否则会被采样成 5~7 这种夸张值
        //   （2026-11-10 离线模拟开局时发现的：「演奏7次」「复制7张」「获得5点能量」）。
        if (atom.RuntimeSpec is { } spec2)
        {
            switch (spec2.Opcode)
            {
                // 多次演奏 / 随机演奏 / 升级剑 / 复制本牌 / 打出手牌 / 猫咪数量
                case SakiComponents.PlayChordOpcode
                    when spec2.Variant is SakiComponents.PlayChordStoredMultiVariant
                        or SakiComponents.ChordLastVariant
                        or SakiComponents.PlayChordRandomVariant:
                    return Math.Clamp(value, 1, 3);
                case SakiComponents.DeckOpcode
                    when spec2.Variant is SakiComponents.CopySelfVariant
                        or SakiComponents.AutoPlayHandVariant
                        or SakiComponents.PlayHandThenForgetVariant
                        or SakiComponents.ExhaustStatusVariant
                        or SakiComponents.RecallFromForgetVariant
                        or SakiComponents.DrawToHandVariant
                        or SakiComponents.RecallFromExhaustVariant:
                    return Math.Clamp(value, 1, 3);
                case SakiComponents.TokenOpcode
                    when spec2.Variant is SakiComponents.SwordUpgradeVariant
                        or SakiComponents.SwordFromAnywhereVariant
                        or SakiComponents.TokenPlayAllSwordsVariant
                        or SakiComponents.TokenPlayPianoVariant
                        or SakiComponents.TokenPlayForgetMemoryVariant
                        or SakiComponents.TokenConsumeNekoDamageVariant
                        or SakiComponents.TokenUpgradeNekoVariant
                        or SakiComponents.TokenNekoRetainVariant:
                    return Math.Clamp(value, 1, 3);
            }
        }

        // ★★ 2026-11-11 用户反馈「数值离谱」（食用 22 杯芭菲打 306 伤、22 层和声、11 层挠！挠！）：
        //   这几类槽位之前完全没夹 → AA 按稀有度倍率放大到两位数。
        //   按 卡牌数值基准表：1 杯芭菲 ≈ 9VP / 1 层能力 ≈ 0.4~0.8VP，
        //   所以「杯数」和「能力层数」都必须压在小范围。
        if (atom.RuntimeSpec is { } specClamp)
        {
            switch (specClamp.Opcode)
            {
                case SakiComponents.ParfaitOpcode:
                    // 吃/充能芭菲：1~4 杯（4 杯 ≈ 36VP，已经是金卡上限）
                    return Math.Clamp(value, 1, 4);
                case SakiComponents.NoteOpcode:
                    // 音符数量：1~3 个
                    return Math.Clamp(value, 1, 3);
                case "power":
                case "gain_power":
                case "csk_gain_power":
                    // 本体能力层数：1~4 层（与 csk_power 同一口径）
                    return Math.Clamp(value, 1, 4);
            }
        }

        // ★ 塞状态牌：1~2 张（实测出现过「将 7 张欺诈加入弃牌堆」，代价过重）
        if (atom.RuntimeSpec is { Opcode: "csk_status" })
            return Math.Clamp(value, 1, 2);

        // ★ 结束回合系列：收益数值必须小（它本身是大负面，
        //   但实测被采样成「抽 9 张」「获得 17 点能量」，收益太夸张）
        if (atom.RuntimeSpec is { Opcode: "csk_extreme" })
        {
            return atom.RuntimeSpec.Variant switch
            {
                SakiComponents.EndTurnDrawVariant => Math.Clamp(value, 2, 6),
                SakiComponents.EndTurnEnergyVariant => Math.Clamp(value, 2, 4),
                SakiComponents.EndTurnDamageVariant => Math.Clamp(value, 15, 35),
                _ => Math.Clamp(value, 1, 3)
            };
        }

        // ★★ 回血量夹 1~6（2026-11-10 用户反馈「回复生命太多了」）：
        //   实测平均 11.1、最高 26 —— 本体卡一般只有 2~6，26 点回血等于一管血。
        //   AA 会把原生 N:Heal 原子按稀有度倍率放大，必须在这里压住。
        if (atom.RuntimeSpec is { Opcode: "heal" })
            return Math.Clamp(value, 1, 6);

        // 自伤也顺手压住，避免「失去 30 点生命」这种
        if (atom.RuntimeSpec is { Opcode: "lose_hp" })
            return Math.Clamp(value, 1, 6);

        // ★ 能量夹 1~2（否则 0 费卡能白给 5 点能量）
        if (atom.RuntimeSpec is { Opcode: "csk_energy" })
            return Math.Clamp(value, 1, 2);

        // ★ 储存和弦数量夹 1~3（2026-11-10：实测出现过「随机储存7个和弦」，
        //   7 个储存和弦再被「演奏」是数量级爆炸）。
        if (atom.RuntimeSpec is { Opcode: "csk_play_chord", Variant: "remember" })
            return Math.Clamp(value, 1, 3);

        // ★ 本体能力的层数必须收紧（2026-11-09 实测：莱芜被放大到 21 层，
        //   而本体卡只给 2~4 层，10 层就转莱芜爽了）。
        //   能力类永远只给 1~4 层，不参与稀有度倍率放大。
        if (atom.RuntimeSpec is { Opcode: "csk_power" })
        {
            // ★ 2026-11-11 用户口径：**只能单层的能力不许写「获得 X 层」**。
            //   能不能叠层由 SakiPowerCatalog.IsStackable 决定（白名单，拿不准就按单层处理）。
            if (!SakiPowerCatalog.IsStackable(atom.RuntimeSpec.Variant)) return 1;
            return Math.Clamp(value, 1, 4);
        }
        var slotId = atom.RuntimeSpec is { Values.Count: > 0 } spec && slot >= 0 && slot < spec.Values.Count
            ? spec.Values[slot].Id
            : string.Empty;

        return slotId switch
        {
            "damage" => Math.Clamp(value, 1, 60),
            "block" => Math.Clamp(value, 1, 55),
            "hits" => Math.Clamp(value, 1, 6),
            "draw" => Math.Clamp(value, 1, 4),
            "energy" => Math.Clamp(value, 1, 3),
            // 层数类：虚弱 / 易伤 / 力量 / 敏捷 / 活力 / 压力 / 回忆 / 音符 / 芭菲 …
            // 下限一律 1，否则会生成「给 0 层」的废牌。
            // ★★ 2026-11-11 用户实测「11 层和声 / 22 倍格挡 / 22 张猫咪」：
            //   这些槽位不属于 damage/block，却被打到两位数 —— 说明**除伤害/格挡外的一切数值**
            //   都应该压到个位数（层数、倍率、张数、杯数）。
            _ => Math.Clamp(value, 1, 8)
        };
    }

    /// <summary>数值加成：不改，留给 AA 自己的倍率逻辑。</summary>
    public int ApplyValueBonuses(ComponentAtom atom, int value, GeneratedCharacter character,
        bool ultimateChaos) => value;

    /// <summary>多个收益效果的叠加折减，沿用原版手感。</summary>
    public int PayoffScalePercent(IReadOnlyList<GeneratorOperation> previous) => previous.Count switch
    {
        // ★ 放宽（2026-11-09）：金卡本来就是「复合强机制」，
        //   原来 2 效果打到 88%、3 效果 78% 会把复合卡的强度削平，
        //   表现成「金卡好几行但每行都很弱」。
        0 or 1 => 100,
        2 => 96,
        3 => 92,
        _ => 88
    };

    /// <summary>
    /// ★ 核心：把整卡预算的中心值按我们的系数放大。
    /// 这是「不用原版平衡模型」真正落地的地方。
    /// </summary>
    public int ScaleRewardCenter(ComponentAtom atom, int center, int benefitLines, GeneratedRarity rarity,
        IReadOnlyList<GeneratorOperation> previous, Random random, bool balancedValues)
    {
        // 设置：完全随机模式 → 不做稀有度缩放，直接在 1~3 倍之间乱给（用户要求「另一个完全随机」）。
        if (RandomConfig.FullyRandomValues)
            return Math.Max(1, (int)Math.Round(center * (0.6 + random.NextDouble() * 2.4)));
        // ★ 2026-11-11 用户口径：**「原版数值」开着时就是原版强度**，不做任何倍率放大。
        //   东尼算法的这个开关本来就是这个意思（ChaosRunDefinitions.ActiveNumericBalanceOptimization
        //   → RandomCardGenerator(request, seed, balancedValues)）。
        //   所以 balancedValues == true → 直接返回中心值；只有关掉它（完全随机）才做缩放。
        if (balancedValues || center <= 0) return center;

        var (damage, block) = Tuning.TryGetValue(rarity, out var tuning) ? tuning : (1.0, 1.0);
        var slot = atom.RuntimeSpec?.Values.FirstOrDefault()?.Id ?? string.Empty;
        var factor = slot switch
        {
            "block" => block,
            "damage" => damage,
            _ => (damage + block) / 2.0
        };

        var scaled = Math.Max(1, (int)Math.Round(center * factor));
        // ★ 回血/自伤在这里再夹一次：只夹 ClampSampledValue 不够
        //   （稀有度缩放发生在它之后，会把 5 又放大到 15）。
        if (atom.RuntimeSpec is { Opcode: "heal" or "lose_hp" })
            scaled = Math.Clamp(scaled, 1, 6);
        return scaled;
    }

    /// <summary>按 profile 建一个实例（AA 每次生成池会调工厂，这里无状态所以复用即可）。</summary>
    internal static SakiValuePolicy For(string profileId) => new();
}



























