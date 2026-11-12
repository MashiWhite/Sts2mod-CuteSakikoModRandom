using ChaosCardGenerator;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 组件词表：把三个卡池的真实卡牌归纳成**可复用的效果组件**。
///
/// ★ 三个硬规则（2026-11-09 按用户反馈固化，改之前先读）：
///
/// ① 染色规则（同本体 localization，1646 处 [gold] / 514 处 [red]）：
///      正面机制词/资源      → [gold]  压力 / 回忆 / 和弦 / 抹茶芭菲 / 猫咪 / 手牌 / 格挡
///      「遗忘」这个负面动作  → [red]
///      数值                 → 随机卡不写死数值（AA 会缩放），所以不用 [blue]
///
/// ② 描述语义（本体的写法惯例）：
///      「遗忘1张牌」默认 = **玩家选择**手牌中的 1 张（与「丢弃1张牌」同义）
///      真要随机必须明写「随机遗忘手牌中的1张牌」
///      ⇒ 选择型：遗忘N张[gold]手牌[/gold]
///       随机型：随机遗忘[gold]手牌[/gold]中的N张牌
///
/// ③ 机制不串池（用户要求）：
///      祥子 → 压力 / 遗忘 / 回忆
///      爱音 → 四种音符 / 和弦
///      乐奈 → 芭菲
///
/// ⚠️ 所有 atom 必须经 <see cref="SakiComponents.Atom"/> 或
///   <see cref="SakiComponents.AtomWithSpec"/> 构造（编译 RuntimeSpec / 对齐本地化 / 注册英文）。
/// </summary>
internal static class SakiAtomLibrary
{
    private const string IconRoot = "res://CuteSakikoMod/images/ui/text/";

    public static string Icon(string name) => $"[img]{IconRoot}{name}.png[/img]";

    /// <summary>音符类型 → 图标短名（与本体 text_attack / text_skill / text_power / text_special 对应）。</summary>
    private static string NoteIconName(NoteKind kind) => kind switch
    {
        NoteKind.Attack => "text_attack",
        NoteKind.Skill => "text_skill",
        NoteKind.Power => "text_power",
        _ => "text_special"
    };

    // ─────────────────────────────────────────────────────────────
    // 通用效果（AA 自带 opcode，三角色共用）
    // ─────────────────────────────────────────────────────────────

    public static ComponentAtom Damage(int amount) =>
        Atom("T:D", OperationScope.SingleEnemyOnly, $"造成{amount}点伤害。", $"Deal {amount} damage.",
            requiresSingleTarget: true, category: ComponentCategory.Damage);

    public static ComponentAtom DamageAll(int amount) =>
        Atom("N:AllD", OperationScope.NonTargeted, $"对所有敌人造成{amount}点伤害。",
            $"Deal {amount} damage to ALL enemies.", category: ComponentCategory.Damage);

    public static ComponentAtom DamageRandom(int amount) =>
        Atom("N:RandomD", OperationScope.NonTargeted, $"随机对敌人造成{amount}点伤害。",
            $"Deal {amount} damage to a random enemy.", category: ComponentCategory.Damage);

    public static ComponentAtom Block(int amount) =>
        Atom("N:B", OperationScope.NonTargeted, $"获得{amount}点[gold]格挡[/gold]。",
            $"Gain {amount} [gold]Block[/gold].", category: ComponentCategory.Defense);

    public static ComponentAtom Draw(int amount) =>
        Atom("N:Draw", OperationScope.NonTargeted, $"抽{amount}张牌。",
            $"Draw {amount} card{(amount > 1 ? "s" : "")}.", category: ComponentCategory.CardManipulation);

    public static ComponentAtom Energy(int amount) =>
        Atom("N:E", OperationScope.NonTargeted, $"获得{amount}点能量。", $"Gain {amount} Energy.",
            category: ComponentCategory.Resource);

    public static ComponentAtom LoseHp(int amount) =>
        Atom("N:HP-", OperationScope.NonTargeted, $"失去{amount}点生命。", $"Lose {amount} HP.",
            category: ComponentCategory.Negative);

    public static ComponentAtom Heal(int amount) =>
        Atom("N:Heal", OperationScope.NonTargeted, $"[gold]回复[/gold]{amount}点生命。", $"Heal {amount} HP.",
            category: ComponentCategory.Resource);

    public static ComponentAtom Strength(int amount) =>
        Atom("N:Self", OperationScope.NonTargeted, $"获得{amount}点[gold]力量[/gold]。",
            $"Gain {amount} [gold]Strength[/gold].", category: ComponentCategory.Growth);

    public static ComponentAtom Dexterity(int amount) =>
        Atom("N:Dex", OperationScope.NonTargeted, $"获得{amount}点[gold]敏捷[/gold]。",
            $"Gain {amount} [gold]Dexterity[/gold].", category: ComponentCategory.Growth);

    public static ComponentAtom TempStrength(int amount) =>
        Atom("N:TempStrength", OperationScope.NonTargeted, $"本回合获得{amount}点[gold]力量[/gold]。",
            $"Gain {amount} [gold]Strength[/gold] this turn.", category: ComponentCategory.Growth);

    public static ComponentAtom Vigor(int amount) =>
        Atom("N:Vigor", OperationScope.NonTargeted, $"获得{amount}层[gold]活力[/gold]。",
            $"Gain {amount} [gold]Vigor[/gold].", category: ComponentCategory.Growth);

    public static ComponentAtom ApplyWeak(int amount) =>
        Atom("T:Apply", OperationScope.SingleEnemyOnly, $"给予{amount}层[gold]虚弱[/gold]。",
            $"Apply {amount} [gold]Weak[/gold].", requiresSingleTarget: true,
            category: ComponentCategory.Status);

    public static ComponentAtom ApplyVulnerable(int amount) =>
        Atom("T:Apply", OperationScope.SingleEnemyOnly, $"给予{amount}层[gold]易伤[/gold]。",
            $"Apply {amount} [gold]Vulnerable[/gold].", requiresSingleTarget: true,
            category: ComponentCategory.Status);

    public static ComponentAtom ApplyWeakAll(int amount) =>
        Atom("N:AllWeak", OperationScope.NonTargeted, $"给予所有敌人{amount}层[gold]虚弱[/gold]。",
            $"Apply {amount} [gold]Weak[/gold] to ALL enemies.", category: ComponentCategory.Status);

    public static ComponentAtom ApplyVulnerableAll(int amount) =>
        Atom("N:AllVulnerable", OperationScope.NonTargeted, $"给予所有敌人{amount}层[gold]易伤[/gold]。",
            $"Apply {amount} [gold]Vulnerable[/gold] to ALL enemies.", category: ComponentCategory.Status);

    public static ComponentAtom EnemiesLoseStrength(int amount) =>
        Atom("N:AllTempStrengthLoss", OperationScope.NonTargeted,
            $"所有敌人在本回合失去{amount}点[gold]力量[/gold]。",
            $"ALL enemies lose {amount} [gold]Strength[/gold] this turn.", category: ComponentCategory.Status);

    /// <summary>结束你的回合，然后抽 N 张牌（大负面 + 收益）。</summary>
    public static ComponentAtom EndTurnDraw(int draw) =>
        SakiComponents.AtomWithSpec(SakiComponents.ExtremeTemplate + "_draw", OperationScope.NonTargeted,
            $"结束你的回合，然后抽{draw}张牌。",
            $"End your turn, then draw {draw} card(s).",
            SakiComponents.NumericSpec(SakiComponents.ExtremeOpcode, SakiComponents.EndTurnDrawVariant,
                "self", "count", draw, []),
            category: ComponentCategory.Negative);

    /// <summary>结束你的回合，然后获得 N 点能量。</summary>
    public static ComponentAtom EndTurnEnergy(int energy) =>
        SakiComponents.AtomWithSpec(SakiComponents.ExtremeTemplate + "_energy", OperationScope.NonTargeted,
            $"结束你的回合，然后获得{{energyPrefix:energyIcons({energy})}}。",
            $"End your turn, then gain {energy} Energy.",
            SakiComponents.NumericSpec(SakiComponents.ExtremeOpcode, SakiComponents.EndTurnEnergyVariant,
                "self", "count", energy, []),
            category: ComponentCategory.Negative);

    /// <summary>结束你的回合，然后对所有敌人造成 N 伤害。</summary>
    public static ComponentAtom EndTurnDamage(int damage) =>
        SakiComponents.AtomWithSpec(SakiComponents.ExtremeTemplate + "_damage", OperationScope.NonTargeted,
            $"结束你的回合，然后对所有敌人造成{damage}点伤害。",
            $"End your turn, then deal {damage} damage to ALL enemies.",
            SakiComponents.NumericSpec(SakiComponents.ExtremeOpcode, SakiComponents.EndTurnDamageVariant,
                "self", "damage", damage, []),
            category: ComponentCategory.Negative);

    /// <summary>
    /// 复合原子（成长解耦版）：主效果是造成 N 伤害，**每打出过一次再额外给 M 点 XY**。
    /// 成长方向与主效果无关 —— 这就是用户要的「造成 X 伤害，每打出一次获得格挡 +M」。
    /// </summary>
    private static ComponentAtom GrowthOf(string variant, string chineseGrowth, string englishGrowth,
        int damage, int growth, string growthSlotId, ComponentCategory category) =>
        SakiComponents.AtomWithSpec(SakiComponents.GrowthTemplate + "_" + variant, OperationScope.NonTargeted,
            $"造成{damage}点伤害。这张牌本场每打出过1次，额外{chineseGrowth}{growth}。",
            $"Deal {damage} damage. For each time you played this card this combat, {englishGrowth} {growth}.",
            SakiComponents.TwoSlotSpec(SakiComponents.GrowthOpcode, variant,
                "self", "damage", damage, growthSlotId, growth, []),
            requiresSingleTarget: false,
            category: category);

    public static ComponentAtom DamageGrowthBlock(int damage, int block) =>
        GrowthOf(SakiComponents.DamageGrowthBlockVariant, "获得[gold]格挡[/gold]", "gain [gold]Block[/gold]",
            damage, block, "growth", ComponentCategory.Growth);

    public static ComponentAtom DamageGrowthSword(int damage, int swords) =>
        GrowthOf(SakiComponents.DamageGrowthSwordVariant, "将[gold]骑士之剑[/gold]加入手牌", "add [gold]Knight Sword[/gold]",
            damage, swords, "growth", ComponentCategory.Growth);

    public static ComponentAtom DamageGrowthMemory(int damage, int memory) =>
        GrowthOf(SakiComponents.DamageGrowthMemoryVariant, "获得[gold]回忆[/gold]", "gain [gold]Memory[/gold]",
            damage, memory, "growth", ComponentCategory.Growth);

    public static ComponentAtom DamageGrowthDraw(int damage, int draw) =>
        GrowthOf(SakiComponents.DamageGrowthDrawVariant, "抽牌", "draw",
            damage, draw, "growth", ComponentCategory.CardManipulation);

    /// <summary>
    /// 抽 1 张牌，并使手牌中随机 N 张牌本回合费用变为 0（C 项·改费用）。
    ///
    /// ★ 为什么文案要带「抽1张牌」：纯费用句 AA 认不出价值（实测 255 张 0 次，
    ///   估值提到 1100 仍 0），必须**寄生**在一个它能识别的效果上（§3.62 的套路）。
    /// </summary>
    public static ComponentAtom FreeHandCost(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.CostTemplate + "_free", OperationScope.NonTargeted,
            $"抽一张牌。使手牌中随机{count}张牌本回合免费。",
            $"Draw 1 card. {count} random card(s) in your hand cost nothing this turn.",
            SakiComponents.NumericSpec(SakiComponents.CostOpcode, SakiComponents.CostFreeHandVariant,
                "self", "count", count, []),
            category: ComponentCategory.CardManipulation);
    /// <summary>
    /// 抽 1 张牌，并随机将手牌中 N 张牌变形（C 项·变牌）。
    ///
    /// ★ 同样走「寄生」路线（§3.82）—— 纯变牌句 AA 认不出价值。
    /// ★ API：`CardCmd.TransformToRandom(CardModel, Rng, CardPreviewStyle)`，
    ///   Rng 取 `owner.PlayerRng.Transformations`（不是 RunState.Rng，后者没这个成员）。
    /// </summary>
    public static ComponentAtom TransformHand(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.CostTemplate + "_transform", OperationScope.NonTargeted,
            $"抽一张牌。随机将手牌中的{count}张牌[gold]变化[/gold]为其他牌。",
            $"Draw 1 card. {count} random card(s) in your hand transform into others of the same rarity.",
            SakiComponents.NumericSpec(SakiComponents.CostOpcode, SakiComponents.CostTransformHandVariant,
                "self", "count", count, []),
            category: ComponentCategory.CardManipulation);
    /// <summary>获得 N 点格挡；每打出过一次额外获得 M 张回忆。</summary>
    public static ComponentAtom BlockGrowthMemory(int block, int memory) =>
        SakiComponents.AtomWithSpec(SakiComponents.GrowthTemplate + "_block_memory", OperationScope.NonTargeted,
            $"获得{block}点[gold]格挡[/gold]。这张牌本场每打出过1次，额外获得[gold]回忆[/gold]{memory}。",
            $"Gain {block} [gold]Block[/gold]. For each time you played this card this combat, gain {memory} [gold]Memory[/gold].",
            SakiComponents.TwoSlotSpec(SakiComponents.GrowthOpcode, SakiComponents.BlockGrowthMemoryVariant,
                "self", "block", block, "growth", memory, []),
            category: ComponentCategory.Defense);

    /// <summary>获得 N 点格挡；每打出过一次额外获得 M 杯抹茶芭菲（乐奈专属）。</summary>
    public static ComponentAtom BlockGrowthParfait(int block, int cups) =>
        SakiComponents.AtomWithSpec(SakiComponents.GrowthTemplate + "_block_parfait", OperationScope.NonTargeted,
            $"获得{block}点[gold]格挡[/gold]。这张牌本场每打出过1次，额外获得[gold]抹茶芭菲[/gold]{cups}杯。",
            $"Gain {block} [gold]Block[/gold]. For each time you played this card this combat, gain {cups} cup(s) of [gold]Matcha Parfait[/gold].",
            SakiComponents.TwoSlotSpec(SakiComponents.GrowthOpcode, SakiComponents.BlockGrowthParfaitVariant,
                "self", "block", block, "growth", cups, []),
            category: ComponentCategory.Defense);

    /// <summary>获得 N 点格挡；你每存储 1 张和弦，额外获得 M 点格挡（T-020）。</summary>
    public static ComponentAtom BlockGrowthChord(int block, int perChord) =>
        SakiComponents.AtomWithSpec(SakiComponents.GrowthTemplate + "_block_chord", OperationScope.NonTargeted,
            $"获得{block}点[gold]格挡[/gold]。你每存储1张[gold]和弦[/gold]，额外获得{perChord}点格挡。",
            $"Gain {block} [gold]Block[/gold]. For each stored [gold]Chord[/gold], gain an extra {perChord} Block.",
            SakiComponents.TwoSlotSpec(SakiComponents.GrowthOpcode, SakiComponents.BlockGrowthChordVariant,
                "self", "block", block, "growth", perChord, []),
            category: ComponentCategory.Defense);

    /// <summary>获得 N 点格挡；每打出过一次额外获得 M 张骑士之剑。</summary>
    public static ComponentAtom BlockGrowthSword(int block, int swords) =>
        SakiComponents.AtomWithSpec(SakiComponents.GrowthTemplate + "_block_sword", OperationScope.NonTargeted,
            $"获得{block}点[gold]格挡[/gold]。这张牌本场每打出过1次，额外将[gold]骑士之剑[/gold]加入手牌{swords}张。",
            $"Gain {block} [gold]Block[/gold]. For each time you played this card this combat, add {swords} [gold]Knight Sword[/gold] to your hand.",
            SakiComponents.TwoSlotSpec(SakiComponents.GrowthOpcode, SakiComponents.BlockGrowthSwordVariant,
                "self", "block", block, "growth", swords, []),
            category: ComponentCategory.Defense);

    /// <summary>获得 N 点格挡；每打出过一次再额外获得 M 点格挡。</summary>
    public static ComponentAtom BlockGrowthBlock(int block, int growth) =>
        SakiComponents.AtomWithSpec(SakiComponents.GrowthTemplate + "_block", OperationScope.NonTargeted,
            $"获得{block}点[gold]格挡[/gold]。这张牌本场每打出过1次，额外获得[gold]格挡[/gold]{growth}。",
            $"Gain {block} [gold]Block[/gold]. For each time you played this card this combat, gain {growth} more [gold]Block[/gold].",
            SakiComponents.TwoSlotSpec(SakiComponents.GrowthOpcode, SakiComponents.BlockGrowthBlockVariant,
                "self", "block", block, "growth", growth, []),
            category: ComponentCategory.Defense);

    /// <summary>
    /// 复合原子：造成 N 点伤害，且这张牌**本场每打出一次，伤害再 +M**。
    ///
    /// ★ 为什么不用 AA 的 `I:IncreaseDamageThisCombat`：它是挂靠型修饰符，
    ///   池子里单独出现会被整条丢掉（§3.77，实测 255 张出现 0 次）。
    ///   这里由我们自己的 handler 维护计数器，完全不依赖 AA 的组装器。
    /// </summary>
    public static ComponentAtom DamageGrowth(int damage, int growth) =>
        SakiComponents.AtomWithSpec(SakiComponents.GrowthTemplate + "_damage", OperationScope.SingleEnemyOnly,
            $"造成{damage}点伤害。这张牌在本场战斗中每打出过1次，伤害提高{growth}点。",
            $"Deal {damage} damage. This card deals {growth} more damage for each time it was played this combat.",
            SakiComponents.TwoSlotSpec(SakiComponents.GrowthOpcode, SakiComponents.DamageGrowthVariant,
                "selected_enemy", "damage", damage, "growth", growth, []),
            requiresSingleTarget: true,
            category: ComponentCategory.Growth);

    /// <summary>
    /// 本场成长：在本场战斗中，这张牌的基础伤害永久增加 N 点。
    ///
    /// 用 AA 原生模板 `I:IncreaseDamageThisCombat` —— 中文文案是**照抄**它自己
    /// 平衡模型里的规范写法（EffectBalanceModel.cs:3183「在本场战斗中，此卡的基础伤害增加4点。」），
    /// 不要自己改写，改了 AA 会判零值（§3.61/§3.62）。
    /// </summary>
    public static ComponentAtom CombatDamageGrowth(int amount) =>
        Atom("I:IncreaseDamageThisCombat", OperationScope.Independent,
            $"在本场战斗中，此卡的基础伤害增加{amount}点。",
            $"In this combat, this card's base damage increases by {amount}.",
            category: ComponentCategory.Growth);

    // ─────────────────────────────────────────────────────────────
    // 延迟结算（C 档「下回合获得 X」，2026-11-10）
    // 用 AA 原生模板 N:NextTurnEnergy / N:NextTurnDraw，
    // 中文写法照 CardTextStyle.cs 里的规范：「在下个回合，获得…／抽…」
    // ─────────────────────────────────────────────────────────────

    /// <summary>把 N 张「欺诈」加入弃牌堆（负面）。</summary>
    public static ComponentAtom AddFraud(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.StatusTemplate + "_fraud", OperationScope.NonTargeted,
            $"将{count}张[gold]欺诈[/gold]加入你的弃牌堆。",
            $"Add {count} [gold]Fraud[/gold] to your discard pile.",
            SakiComponents.NumericSpec(SakiComponents.StatusOpcode, SakiComponents.FraudStatusVariant,
                "self", "count", count, []),
            category: ComponentCategory.Negative);

    /// <summary>把 N 张「偷懒」加入弃牌堆（负面）。</summary>
    public static ComponentAtom AddShirk(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.StatusTemplate + "_shirk", OperationScope.NonTargeted,
            $"将{count}张[gold]逃避[/gold]加入你的弃牌堆。",
            $"Add {count} [gold]Slack Off[/gold] to your discard pile.",
            SakiComponents.NumericSpec(SakiComponents.StatusOpcode, SakiComponents.ShirkStatusVariant,
                "self", "count", count, []),
            category: ComponentCategory.Negative);

    /// <summary>在下个回合获得 N 点能量。</summary>
    public static ComponentAtom NextTurnEnergy(int energy) =>
        Atom("N:NextTurnEnergy", OperationScope.NonTargeted, $"在下个回合，获得{energy}点能量。",
            $"Next turn, gain {energy} Energy.", category: ComponentCategory.Resource);

    /// <summary>在下个回合抽 N 张牌。</summary>
    public static ComponentAtom NextTurnDraw(int draw) =>
        Atom("N:NextTurnDraw", OperationScope.NonTargeted, $"在下个回合，抽{draw}张牌。",
            $"Next turn, draw {draw} card(s).", category: ComponentCategory.CardManipulation);

    /// <summary>丢弃：默认是玩家选择，所以描述里不写「随机」。</summary>
    public static ComponentAtom Discard(int count) =>
        Atom("N:Discard", OperationScope.NonTargeted, $"丢弃{count}张[gold]手牌[/gold]。",
            $"Discard {count} card{(count > 1 ? "s" : "")} from your [gold]Hand[/gold].",
            category: ComponentCategory.CardManipulation);

    public static ComponentAtom ExhaustHand(int count) =>
        Atom("N:Exhaust", OperationScope.NonTargeted, $"消耗{count}张[gold]手牌[/gold]。",
            $"Exhaust {count} card{(count > 1 ? "s" : "")} from your [gold]Hand[/gold].",
            category: ComponentCategory.CardManipulation);

    // ─────────────────────────────────────────────────────────────
    // 触发器 / 能力牌基础
    // ─────────────────────────────────────────────────────────────

    public static ComponentAtom TriggerTurnStart() =>
        Atom("A:turnStart", OperationScope.AbilityTrigger, "在你的回合开始时。", "At the start of your turn.",
            category: ComponentCategory.Trigger);

    public static ComponentAtom TriggerTurnEnd() =>
        Atom("A:turnEnd", OperationScope.AbilityTrigger, "在你的回合结束时。", "At the end of your turn.",
            category: ComponentCategory.Trigger);

    public static ComponentAtom PowerFoundation(int strength) =>
        Atom("N:Self", OperationScope.NonTargeted, $"获得{strength}点[gold]力量[/gold]。",
            $"Gain {strength} [gold]Strength[/gold].", category: ComponentCategory.Growth);

    // ─────────────────────────────────────────────────────────────
    // 祥子专属：压力 / 遗忘 / 回忆
    // ─────────────────────────────────────────────────────────────

    public static ComponentAtom Pressure(int amount) =>
        SakiComponents.AtomWithSpec(SakiComponents.PressureTemplate, OperationScope.SingleEnemyOnly,
            $"给予{amount}层[gold]压力[/gold]。", $"Apply {amount} [gold]Pressure[/gold].",
            SakiComponents.NumericSpec(SakiComponents.PressureOpcode, SakiComponents.PressureVariant,
                "selected_enemy", "amount", amount,
                [ComponentSemanticFlags.Beneficial, "requires_single_target"]),
            requiresSingleTarget: true, category: ComponentCategory.Status);

    public static ComponentAtom PressureSelf(int amount) =>
        SakiComponents.AtomWithSpec(SakiComponents.PressureTemplate, OperationScope.NonTargeted,
            $"获得{amount}层[gold]压力[/gold]。", $"Gain {amount} [gold]Pressure[/gold].",
            SakiComponents.NumericSpec(SakiComponents.PressureOpcode, SakiComponents.PressureVariant,
                "self", "amount", amount, [ComponentSemanticFlags.Beneficial]),
            category: ComponentCategory.Status);

    public static ComponentAtom PressureAll(int amount) =>
        SakiComponents.AtomWithSpec(SakiComponents.PressureTemplate, OperationScope.NonTargeted,
            $"给予所有敌人{amount}层[gold]压力[/gold]。",
            $"Apply {amount} [gold]Pressure[/gold] to ALL enemies.",
            SakiComponents.NumericSpec(SakiComponents.PressureOpcode, SakiComponents.PressureAllVariant,
                "all_enemies", "amount", amount, [ComponentSemanticFlags.Beneficial]),
            category: ComponentCategory.Status);

    /// <summary>
    /// 遗忘（选择型）：**玩家选择**手牌中的 N 张遗忘。
    /// 「遗忘」是负面动作 → 用 [red]，与本体 11 处 [red]遗忘[/red] 一致。
    /// 描述不写「随机」，因为玩家确实是自己选。
    /// </summary>
    public static ComponentAtom Forget(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.ForgetTemplate, OperationScope.NonTargeted,
            $"[red]遗忘[/red]{count}张[gold]手牌[/gold]。",
            $"[red]Forget[/red] {count} card{(count > 1 ? "s" : "")} from your [gold]Hand[/gold].",
            SakiComponents.NumericSpec(SakiComponents.ForgetOpcode, SakiComponents.ForgetVariant,
                "selected_card", "count", count, ["requires_player_choice"]),
            category: ComponentCategory.CardManipulation);

    /// <summary>
    /// 遗忘（随机型）：明确写「随机遗忘手牌中的 N 张牌」。
    /// 与 <see cref="Forget"/> 用不同 variant，执行器走随机分支。
    /// </summary>
    public static ComponentAtom ForgetRandom(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.ForgetTemplate, OperationScope.NonTargeted,
            $"随机[red]遗忘[/red][gold]手牌[/gold]中的{count}张牌。",
            $"Randomly [red]Forget[/red] {count} card{(count > 1 ? "s" : "")} from your [gold]Hand[/gold].",
            SakiComponents.NumericSpec(SakiComponents.ForgetOpcode, SakiComponents.ForgetRandomVariant,
                "selected_card", "count", count, []),
            category: ComponentCategory.CardManipulation);

    public static ComponentAtom Recall(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.RecallTemplate, OperationScope.NonTargeted,
            $"获得{count}张[gold]回忆[/gold]。", $"Add {count} [gold]Memory[/gold] to your Hand.",
            SakiComponents.NumericSpec(SakiComponents.RecallOpcode, SakiComponents.RecallVariant,
                "self", "count", count, ["replenishes_hand"]),
            category: ComponentCategory.CardManipulation);

    // ─────────────────────────────────────────────────────────────
    // 爱音专属：音符（四种类型）/ 和弦
    //
    // 音符类型与图标（zhs 本地化 CUTE_SAKIKO_MOD_KEYWORD_NOTEIFY）：
    //   Attack  text_attack.png   对所有敌人造成4点伤害
    //   Skill   text_skill.png    所有友方获得3点格挡
    //   Power   text_power.png    所有友方抽1张牌
    //   Special text_special.png  所有友方获得1点能量
    // ─────────────────────────────────────────────────────────────

    public static ComponentAtom AddNote(int count, NoteKind kind)
    {
        var icon = kind switch
        {
            NoteKind.Attack => "text_attack",
            NoteKind.Skill => "text_skill",
            NoteKind.Power => "text_power",
            _ => "text_special"
        };
        // ★ 2026-11-11 用户第 5 条：本体写法是「数字 + 音符图标」（如「好演奏」的
        //   「获得5[icon]」），不要写「5个能力音符」。类型由图标颜色区分。
        //   只有随机音符保留「随机」前缀。
        return SakiComponents.AtomWithSpec(SakiComponents.NoteTemplate, OperationScope.NonTargeted,
            $"获得{count}{Icon(icon)}。",
            $"Gain {count} Note{((count > 1) ? "s" : "")}.",
            SakiComponents.NumericSpec(SakiComponents.NoteOpcode,
                SakiComponents.NoteVariantFor(kind), "self", "count", count, []),
            category: ComponentCategory.Resource);
    }

    public static ComponentAtom ClearNotes(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.NoteTemplate, OperationScope.NonTargeted,
            $"清除{count}次所有{Icon("text_note")}并触发对应效果。",
            $"Clear all Notes {count} time and trigger their effects.",
            SakiComponents.NumericSpec(SakiComponents.NoteOpcode, SakiComponents.NoteClearVariant,
                "self", "count", count, []),
            category: ComponentCategory.Resource);

    /// <summary>演奏 N 次所有储存的和弦（多次版，单独定价，避免与 1 次共用估值）。</summary>
    public static ComponentAtom PlayStoredChordsMulti(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.PlayChordTemplate + "_multi", OperationScope.NonTargeted,
            $"演奏{count}次所有储存的[gold]和弦[/gold]。",
            $"Play all stored [gold]Chords[/gold] {count} times.",
            SakiComponents.NumericSpec(SakiComponents.PlayChordOpcode, SakiComponents.PlayChordStoredMultiVariant,
                "self", "count", count, []),
            category: ComponentCategory.Damage);

    public static ComponentAtom PlayStoredChords(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.PlayChordTemplate, OperationScope.NonTargeted,
            $"演奏{count}次所有储存的[gold]和弦[/gold]。",
            $"Play all stored [gold]Chords[/gold] {count} time.",
            SakiComponents.NumericSpec(SakiComponents.PlayChordOpcode,
                SakiComponents.PlayChordStoredVariant, "self", "count", count, []),
            category: ComponentCategory.Utility);

    public static ComponentAtom PlayRandomChord(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.PlayChordTemplate, OperationScope.NonTargeted,
            $"随机演奏{count}个[gold]已记忆和弦[/gold]。",
            $"Randomly play {count} memorized [gold]Chord{((count > 1) ? "s" : "")}[/gold].",
            SakiComponents.NumericSpec(SakiComponents.PlayChordOpcode,
                SakiComponents.PlayChordRandomVariant, "self", "count", count, []),
            category: ComponentCategory.Utility);

    // ─────────────────────────────────────────────────────────────
    // 乐奈专属：芭菲
    // ─────────────────────────────────────────────────────────────

    public static ComponentAtom EatParfait(int amount) =>
        SakiComponents.AtomWithSpec(SakiComponents.ParfaitTemplate, OperationScope.NonTargeted,
            $"食用{amount}杯[gold]抹茶芭菲[/gold]。", $"Consume {amount} [gold]Matcha Parfait[/gold].",
            SakiComponents.NumericSpec(SakiComponents.ParfaitOpcode, SakiComponents.ParfaitEatVariant,
                "self", "amount", amount, []),
            category: ComponentCategory.Resource);

    public static ComponentAtom GainParfait(int amount) =>
        SakiComponents.AtomWithSpec(SakiComponents.ParfaitTemplate, OperationScope.NonTargeted,
            $"获得{amount}杯[gold]抹茶芭菲[/gold]。", $"Gain {amount} [gold]Matcha Parfait[/gold].",
            SakiComponents.NumericSpec(SakiComponents.ParfaitOpcode, SakiComponents.ParfaitChargeVariant,
                "self", "amount", amount, []),
            category: ComponentCategory.Resource);    // ─────────────────────────────────────────────────────────────
    // 令牌牌：猫咪（乐奈）/ 骑士之剑（祥子）
    //
    // 本体里这两类都不是「直接给个 buff」，而是**把一张令牌卡放进手牌**
    // （令牌池 CuteSakikoTokenCardPool，按关键词区分）。
    // 之前随机池完全没有这两类，所以玩起来「乐奈没有猫咪牌、祥子没有剑」。
    // ─────────────────────────────────────────────────────────────

    /// <summary>获得 N 张猫咪牌。</summary>
    public static ComponentAtom GainNeko(int amount) =>
        SakiComponents.AtomWithSpec(SakiComponents.TokenTemplate + "_neko", OperationScope.NonTargeted,
            $"获得{amount}张[gold]猫咪[/gold]牌。", $"Add {amount} [gold]Cat[/gold] card(s) to your hand.",
            SakiComponents.NumericSpec(SakiComponents.TokenOpcode, SakiComponents.TokenNekoVariant,
                "self", "amount", amount, []),
            category: ComponentCategory.CardManipulation);

    /// <summary>获得 N 张骑士之剑。</summary>
    public static ComponentAtom GainSword(int amount) =>
        SakiComponents.AtomWithSpec(SakiComponents.TokenTemplate + "_sword", OperationScope.NonTargeted,
            $"获得{amount}张[gold]骑士之剑[/gold]。", $"Add {amount} [gold]Knight Sword[/gold].",
            SakiComponents.NumericSpec(SakiComponents.TokenOpcode, SakiComponents.TokenSwordVariant,
                "self", "amount", amount, []),
            category: ComponentCategory.Damage);

    /// <summary>获得 N 张糖果（乐奈的甜食令牌牌）。</summary>
    public static ComponentAtom GainCandy(int amount) =>
        SakiComponents.AtomWithSpec(SakiComponents.TokenTemplate + "_candy", OperationScope.NonTargeted,
            $"获得{amount}张[gold]糖果[/gold]牌。", $"Add {amount} [gold]Candy[/gold] card(s) to your hand.",
            SakiComponents.NumericSpec(SakiComponents.TokenOpcode, SakiComponents.TokenCandyVariant,
                "self", "amount", amount, []),
            category: ComponentCategory.Resource);    // ─────────────────────────────────────────────────────────────
    // 消耗压力换额外效果（祥子）
    //
    // 本体统一写法「若有则消耗N层压力，额外 X」。做成**两段式单原子**，
    // 保证卡面永远读得通（不会出现只有「额外造成4点伤害」却没有主效果）。
    // ─────────────────────────────────────────────────────────────

    /// <summary>造成 N 点伤害。若有则消耗 1 层压力，额外造成 M 点伤害。</summary>
    public static ComponentAtom PayPressureDamage(int damage, int extraHits = 1) =>
        SakiComponents.AtomWithSpec(SakiComponents.PayPressureTemplate, OperationScope.SingleEnemyOnly,
            $"造成{damage}点伤害。若有则消耗1层[gold]压力[/gold]，额外造成{extraHits}次伤害。",
            $"Deal {damage} damage. If able, spend 1 [gold]Pressure[/gold] to deal {extraHits} more hit(s).",
            SakiComponents.TwoSlotSpec(SakiComponents.PayPressureOpcode,
                SakiComponents.PayPressureDamageVariant, "selected_enemy",
                "damage", damage, "hits", extraHits, []),
            category: ComponentCategory.Damage);

    /// <summary>获得 N 点格挡。若有则消耗 1 层压力，额外获得 M 点格挡。</summary>
    public static ComponentAtom PayPressureBlock(int block, int extra) =>
        SakiComponents.AtomWithSpec(SakiComponents.PayPressureTemplate, OperationScope.NonTargeted,
            $"获得{block}点[gold]格挡[/gold]。若有则消耗1层[gold]压力[/gold]，额外获得{extra}点[gold]格挡[/gold]。",
            $"Gain {block} [gold]Block[/gold]. If able, spend 1 [gold]Pressure[/gold] to gain {extra} more.",
            SakiComponents.TwoSlotSpec(SakiComponents.PayPressureOpcode,
                SakiComponents.PayPressureBlockVariant, "self",
                "block", block, "extra", extra, []),
            category: ComponentCategory.Defense);

    /// <summary>造成 N 点伤害。若有则消耗 1 层压力，额外抽 M 张牌。</summary>
    public static ComponentAtom PayPressureDraw(int damage, int extra) =>
        SakiComponents.AtomWithSpec(SakiComponents.PayPressureTemplate, OperationScope.SingleEnemyOnly,
            $"造成{damage}点伤害。若有则消耗1层[gold]压力[/gold]，额外抽{extra}张牌。",
            $"Deal {damage} damage. If able, spend 1 [gold]Pressure[/gold] to draw {extra}.",
            SakiComponents.TwoSlotSpec(SakiComponents.PayPressureOpcode,
                SakiComponents.PayPressureDrawVariant, "selected_enemy",
                "damage", damage, "draw", extra, []),
            category: ComponentCategory.Damage);    // ─────────────────────────────────────────────────────────────
    // 食用芭菲 + 大效果（乐奈）
    //
    // 依据基准表 §1.2：食用 N 杯是**代价**（吃掉 9VP/杯 的储存资源），
    // 所以同一张卡上要给出远超稀有度档位的数值。
    // ─────────────────────────────────────────────────────────────

    /// <summary>食用 N 杯芭菲，获得 M 点格挡。</summary>
    public static ComponentAtom EatParfaitBlock(int cups, int block) =>
        SakiComponents.AtomWithSpec(SakiComponents.ParfaitComboTemplate, OperationScope.NonTargeted,
            $"食用{cups}杯[gold]抹茶芭菲[/gold]，获得{block}点[gold]格挡[/gold]。",
            $"Consume {cups} [gold]Matcha Parfait[/gold] and gain {block} [gold]Block[/gold].",
            SakiComponents.TwoSlotSpec(SakiComponents.ParfaitComboOpcode,
                SakiComponents.ParfaitComboBlockVariant, "self",
                "cups", cups, "block", block, []),
            category: ComponentCategory.Defense);

    /// <summary>食用 N 杯芭菲，对所有敌人造成 M 点伤害。</summary>
    public static ComponentAtom EatParfaitDamageAll(int cups, int damage) =>
        SakiComponents.AtomWithSpec(SakiComponents.ParfaitComboTemplate, OperationScope.NonTargeted,
            $"食用{cups}杯[gold]抹茶芭菲[/gold]，对所有敌人造成{damage}点伤害。",
            $"Consume {cups} [gold]Matcha Parfait[/gold] and deal {damage} damage to ALL enemies.",
            SakiComponents.TwoSlotSpec(SakiComponents.ParfaitComboOpcode,
                SakiComponents.ParfaitComboDamageAllVariant, "all_enemies",
                "cups", cups, "damage", damage, []),
            category: ComponentCategory.Damage);

    /// <summary>食用 N 杯芭菲，回复 M 点生命并抽 1 张牌。</summary>
    public static ComponentAtom EatParfaitHealDraw(int cups, int heal) =>
        SakiComponents.AtomWithSpec(SakiComponents.ParfaitComboTemplate, OperationScope.NonTargeted,
            $"食用{cups}杯[gold]抹茶芭菲[/gold]，回复{heal}点生命并抽1张牌。",
            $"Consume {cups} [gold]Matcha Parfait[/gold], heal {heal} and draw 1.",
            SakiComponents.TwoSlotSpec(SakiComponents.ParfaitComboOpcode,
                SakiComponents.ParfaitComboHealDrawVariant, "self",
                "cups", cups, "heal", heal, []),
            category: ComponentCategory.Resource);    // ─────────────────────────────────────────────────────────────
    // 爱音的复合同子（T-003）
    //
    // 「获得音符」「演奏和弦」本身不产生收益（纯 setup），
    // AA 组装时会让它们给 payoff 让位 → 机制浓度只有 6%。
    // 把 setup 和 payoff 写进同一句、同一个原子，就不会被拆开。
    // ─────────────────────────────────────────────────────────────

    /// <summary>获得 N 个音符，造成 M 点伤害。</summary>
    public static ComponentAtom AddNoteAndDamage(int count, NoteKind kind, int damage) =>
        SakiComponents.AtomWithSpec(SakiComponents.NoteTemplate, OperationScope.SingleEnemyOnly,
            $"获得{count}{Icon(NoteIconName(kind))}，造成{damage}点伤害。",
            $"Gain {count} {kind} Note and deal {damage} damage.",
            SakiComponents.TwoSlotSpec(SakiComponents.NoteOpcode, SakiComponents.NoteAndDamageVariant,
                "selected_enemy", "count", count, "damage", damage, []),
            category: ComponentCategory.Damage);

    /// <summary>获得 N 个音符，获得 M 点格挡。</summary>
    public static ComponentAtom AddNoteAndBlock(int count, NoteKind kind, int block) =>
        SakiComponents.AtomWithSpec(SakiComponents.NoteTemplate, OperationScope.NonTargeted,
            $"获得{count}{Icon(NoteIconName(kind))}，获得{block}点[gold]格挡[/gold]。",
            $"Gain {count} {kind} Note and gain {block} [gold]Block[/gold].",
            SakiComponents.TwoSlotSpec(SakiComponents.NoteOpcode, SakiComponents.NoteAndBlockVariant,
                "self", "count", count, "block", block, []),
            category: ComponentCategory.Defense);

    /// <summary>获得 N 个音符，并抽 M 张牌。</summary>
    public static ComponentAtom AddNoteAndDraw(int count, NoteKind kind, int draw) =>
        SakiComponents.AtomWithSpec(SakiComponents.NoteTemplate + "_draw", OperationScope.NonTargeted,
            $"获得{count}{Icon(NoteIconName(kind))}，抽{draw}张牌。",
            $"Gain {count} {kind} Note and draw {draw}.",
            SakiComponents.TwoSlotSpec(SakiComponents.NoteOpcode, SakiComponents.NoteAndDrawVariant,
                "self", "count", count, "draw", draw, []),
            category: ComponentCategory.Resource);

    /// <summary>获得 N 个音符，并获得 M 点能量。</summary>
    public static ComponentAtom AddNoteAndEnergy(int count, NoteKind kind, int energy) =>
        SakiComponents.AtomWithSpec(SakiComponents.NoteTemplate + "_energy", OperationScope.NonTargeted,
            $"获得{count}{Icon(NoteIconName(kind))}，获得{{energyPrefix:energyIcons({energy})}}。",
            $"Gain {count} {kind} Note and gain {energy} energy.",
            SakiComponents.TwoSlotSpec(SakiComponents.NoteOpcode, SakiComponents.NoteAndEnergyVariant,
                "self", "count", count, "energy", energy, []),
            category: ComponentCategory.Resource);

    /// <summary>食用 N 杯芭菲，抽 M 张牌。</summary>
    public static ComponentAtom EatParfaitDraw(int cups, int draw) =>
        SakiComponents.AtomWithSpec(SakiComponents.ParfaitComboTemplate + "_draw", OperationScope.NonTargeted,
            $"食用{cups}杯[gold]抹茶芭菲[/gold]，抽{draw}张牌。",
            $"Consume {cups} [gold]Matcha Parfait[/gold] and draw {draw}.",
            SakiComponents.TwoSlotSpec(SakiComponents.ParfaitComboOpcode,
                SakiComponents.ParfaitComboDrawVariant, "self",
                "cups", cups, "draw", draw, []),
            category: ComponentCategory.Resource);

    /// <summary>获得 N 个随机音符，并**触发所有音符效果**（本体「并触发音符对应效果」）。</summary>
    public static ComponentAtom AddNoteAndTrigger(int attackNotes, int skillNotes) =>
        SakiComponents.AtomWithSpec(SakiComponents.NoteTemplate, OperationScope.NonTargeted,
            $"获得{attackNotes}{Icon("text_attack")}与{skillNotes}{Icon("text_skill")}，并触发所有{Icon("text_note")}对应效果。",
            $"Gain {attackNotes} Attack Notes and {skillNotes} Skill Notes, then trigger all Note effects.",
            SakiComponents.TwoSlotSpec(SakiComponents.NoteOpcode, SakiComponents.NoteAndTriggerVariant,
                "self", "attack", attackNotes, "skill", skillNotes, []),
            category: ComponentCategory.Damage);

    /// <summary>演奏 N 次所有储存的和弦，造成 M 点伤害。</summary>
    public static ComponentAtom PlayChordAndDamage(int count, int damage) =>
        SakiComponents.AtomWithSpec(SakiComponents.PlayChordTemplate, OperationScope.SingleEnemyOnly,
            $"演奏{count}次所有储存的[gold]和弦[/gold]，造成{damage}点伤害。",
            $"Play all stored [gold]Chords[/gold] {count} time and deal {damage} damage.",
            SakiComponents.TwoSlotSpec(SakiComponents.PlayChordOpcode, SakiComponents.ChordAndDamageVariant,
                "selected_enemy", "count", count, "damage", damage, []),
            category: ComponentCategory.Damage);

    /// <summary>演奏 N 次所有储存的和弦，获得 M 点格挡。</summary>
    public static ComponentAtom PlayChordAndBlock(int count, int block) =>
        SakiComponents.AtomWithSpec(SakiComponents.PlayChordTemplate, OperationScope.NonTargeted,
            $"演奏{count}次所有储存的[gold]和弦[/gold]，获得{block}点[gold]格挡[/gold]。",
            $"Play all stored [gold]Chords[/gold] {count} time and gain {block} [gold]Block[/gold].",
            SakiComponents.TwoSlotSpec(SakiComponents.PlayChordOpcode, SakiComponents.ChordAndBlockVariant,
                "self", "count", count, "block", block, []),
            category: ComponentCategory.Defense);    // ─────────────────────────────────────────────────────────────
    // 储存和弦 / 演奏最后一个（T-004）
    //
    // 本体爱音的完整主轴是「储存 → 演奏」。之前随机池只有后半截（演奏储存的和弦），
    // 没有「把和弦存起来」的动作，所以玩家永远攒不出和弦。
    // ─────────────────────────────────────────────────────────────

    /// <summary>随机储存 N 个和弦。</summary>
    public static ComponentAtom RememberChord(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.ChordRememberTemplate, OperationScope.NonTargeted,
            $"随机储存{count}个[gold]和弦[/gold]，并让该和弦额外获得一个槽位。",
            $"Store {count} random [gold]Chord[/gold].",
            SakiComponents.NumericSpec(SakiComponents.PlayChordOpcode, SakiComponents.ChordRememberVariant,
                "self", "count", count, []),
            category: ComponentCategory.Resource);

    /// <summary>演奏 N 次最后储存的和弦。</summary>
    public static ComponentAtom PlayLastChord(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.ChordLastTemplate, OperationScope.NonTargeted,
            $"演奏{count}次最后储存的[gold]和弦[/gold]。",
            $"Play the last stored [gold]Chord[/gold] {count} time(s).",
            SakiComponents.NumericSpec(SakiComponents.PlayChordOpcode, SakiComponents.ChordLastVariant,
                "self", "count", count, []),
            category: ComponentCategory.Damage);











    /// <summary>使所有敌人重新随机下回合的意图（T-024）。无槽版，文案不带数字。</summary>
    public static ComponentAtom RerollEnemyIntent() =>
        SakiComponents.AtomWithSpec("csk:reroll_intent", OperationScope.NonTargeted,
            "使所有敌人重新随机下回合的意图。",
            "Make all enemies reroll their next intent.",
            SakiComponents.NoValueSpec(SakiComponents.IntentOpcode, SakiComponents.RerollIntentVariant),
            category: ComponentCategory.Utility);

    /// <summary>
    /// 获得 2 点能量。★ 槽 id 必须叫 `energy`：AA 的命名本地化模板用
    /// `[[energy:energy]]` 这个 token，校验时会去找 **id 为 energy 的数值槽**，
    /// 找不到就报 "Chinese operation template references missing slot energy"（2026-11-10 实测）。
    /// </summary>
    public static ComponentAtom EnergyGainTwo() =>
        SakiComponents.AtomWithSpec("csk:energy_gain_two", OperationScope.NonTargeted,
            "获得2点能量。", "Gain 2 Energy.",
            SakiComponents.NumericSpec(SakiComponents.EnergyOpcode, SakiComponents.EnergyGainTwoVariant,
                "self", "energy", 2, []),
            category: ComponentCategory.Resource);

    /// <summary>下回合获得两点能量（自建）。</summary>
    public static ComponentAtom EnergyNextTurnTwo() =>
        SakiComponents.AtomWithSpec("csk:energy_next_two", OperationScope.NonTargeted,
            "在下个回合，获得两点能量。", "Next turn, gain 2 Energy.",
            SakiComponents.NoValueSpec(SakiComponents.EnergyOpcode, SakiComponents.EnergyNextTurnTwoVariant),
            category: ComponentCategory.Resource);

    /// <summary>造成等同于「琴奏」牌数量 N 倍的伤害（本体「琴奏：回忆」）。</summary>
    public static ComponentAtom DamageByPiano(int multiplier) =>
        SakiComponents.AtomWithSpec(SakiComponents.ScaleTemplate + "_piano_damage", OperationScope.NonTargeted,
            $"造成等同于你拥有的[gold]琴奏[/gold]牌数量{multiplier}倍的伤害（至少3点）。",
            $"Deal damage equal to {multiplier}x the number of [gold]Piano[/gold] cards you have (at least 3).",
            SakiComponents.NumericSpec(SakiComponents.ScaleOpcode, SakiComponents.DamageByPianoVariant,
                "self", "count", multiplier, []),
            category: ComponentCategory.Damage);

    /// <summary>获得等同于「琴奏」牌数量 N 倍的格挡。</summary>
    public static ComponentAtom BlockByPiano(int multiplier) =>
        SakiComponents.AtomWithSpec(SakiComponents.ScaleTemplate + "_piano_block", OperationScope.NonTargeted,
            $"获得等同于你拥有的[gold]琴奏[/gold]牌数量{multiplier}倍的[gold]格挡[/gold]（至少3点）。",
            $"Gain Block equal to {multiplier}x the number of [gold]Piano[/gold] cards you have (at least 3).",
            SakiComponents.NumericSpec(SakiComponents.ScaleOpcode, SakiComponents.BlockByPianoVariant,
                "self", "count", multiplier, []),
            category: ComponentCategory.Defense);

    /// <summary>造成 N 点伤害，每有 1 张「琴奏」牌额外 1 次（T-031，本体「琴奏：华丽」）。</summary>
    public static ComponentAtom DamageHitsByPiano(int damage) =>
        SakiComponents.AtomWithSpec(SakiComponents.ScaleTemplate + "_piano_hits", OperationScope.NonTargeted,
            $"造成{damage}点伤害，每有1张[gold]琴奏[/gold]牌就额外1次。",
            $"Deal {damage} damage once, plus once for each [gold]Piano[/gold] card you have.",
            SakiComponents.NumericSpec(SakiComponents.ScaleOpcode, SakiComponents.DamageHitsByPianoVariant,
                "self", "damage", damage, []),
            category: ComponentCategory.Damage);

    /// <summary>升级你所有牌堆中的琴奏牌（T-033，本体「磨刀」的琴奏版）。无槽版，文案不带数字。</summary>
    public static ComponentAtom UpgradeAllPiano() =>
        SakiComponents.AtomWithSpec(SakiComponents.TokenTemplate + "_upgrade_all_piano", OperationScope.NonTargeted,
            "升级你所有牌堆中的[gold]琴奏[/gold]牌。",
            "Upgrade every [gold]Piano[/gold] card in your piles.",
            SakiComponents.NoValueSpec(SakiComponents.TokenOpcode, SakiComponents.TokenUpgradeAllPianoVariant),
            category: ComponentCategory.Utility);

    /// <summary>打出并消耗所有牌堆中的琴奏牌（本体「终幕演出」）。
    /// ★ 无槽版：文案不带数字，避免 AA 把数字编译成 {AmountN:diff()}（§3.92）。</summary>
    public static ComponentAtom PlayAllPiano() =>
        SakiComponents.AtomWithSpec(SakiComponents.TokenTemplate + "_play_all_piano", OperationScope.NonTargeted,
            "打出并消耗你所有牌堆中的[gold]琴奏[/gold]牌。",
            "Play and exhaust every [gold]Piano[/gold] card in your piles.",
            SakiComponents.NoValueSpec(SakiComponents.TokenOpcode, SakiComponents.TokenPlayAllPianoVariant),
            category: ComponentCategory.Damage);

    // ─────────────────────────────────────────────────────────────
    // 条件型原子（2026-11-10）
    // ─────────────────────────────────────────────────────────────

    /// <summary>压力翻倍；若因此触发崩溃则抽 N 张并获得 1 点能量（本体「如梦一场」）。</summary>
    public static ComponentAtom DoubleCollapseBonus(int draw) =>
        SakiComponents.AtomWithSpec(SakiComponents.ConditionTemplate + "_double_collapse",
            OperationScope.NonTargeted,
            $"使自身的[gold]压力[/gold]翻倍；若因此触发[gold]崩溃[/gold]，抽{draw}张牌并获得一点能量。",
            $"Double your [gold]Pressure[/gold]; if that triggers [gold]Breakdown[/gold], draw {draw} and gain 1 energy.",
            SakiComponents.NumericSpec(SakiComponents.ConditionOpcode, SakiComponents.DoubleCollapseBonusVariant,
                "self", "draw", draw, []),
            category: ComponentCategory.Resource);

    /// <summary>压力翻倍；若因此触发崩溃则对全体敌人造成 N 伤害。</summary>
    public static ComponentAtom DoubleCollapseDamage(int damage) =>
        SakiComponents.AtomWithSpec(SakiComponents.ConditionTemplate + "_double_damage",
            OperationScope.NonTargeted,
            $"使自身的[gold]压力[/gold]翻倍；如果因此触发[gold]崩溃[/gold]，对所有敌人造成{damage}点伤害。",
            $"Double your [gold]Pressure[/gold]; if that triggers [gold]Breakdown[/gold], deal {damage} damage to ALL enemies.",
            SakiComponents.NumericSpec(SakiComponents.ConditionOpcode,
                SakiComponents.DoubleCollapseDamageVariant, "self", "damage", damage, []),
            category: ComponentCategory.Damage);

    /// <summary>压力翻倍；若因此触发崩溃则抽 N 张牌。</summary>
    public static ComponentAtom DoubleCollapseDraw(int draw) =>
        SakiComponents.AtomWithSpec(SakiComponents.ConditionTemplate + "_double_draw",
            OperationScope.NonTargeted,
            $"使自身的[gold]压力[/gold]翻倍；如果因此触发[gold]崩溃[/gold]，抽{draw}张牌。",
            $"Double your [gold]Pressure[/gold]; if that triggers [gold]Breakdown[/gold], draw {draw} cards.",
            SakiComponents.NumericSpec(SakiComponents.ConditionOpcode,
                SakiComponents.DoubleCollapseDrawVariant, "self", "count", draw, []),
            category: ComponentCategory.CardManipulation);

    /// <summary>压力翻倍；若因此触发崩溃则获得 N 层随机能力。</summary>
    public static ComponentAtom DoubleCollapsePower(int stacks) =>
        SakiComponents.AtomWithSpec(SakiComponents.ConditionTemplate + "_double_power",
            OperationScope.NonTargeted,
            $"使自身的[gold]压力[/gold]翻倍；如果因此触发[gold]崩溃[/gold]，获得{stacks}层随机[gold]增益[/gold]。",
            $"Double your [gold]Pressure[/gold]; if that triggers [gold]Breakdown[/gold], gain {stacks} stack(s) of a random [gold]buff[/gold].",
            SakiComponents.NumericSpec(SakiComponents.ConditionOpcode,
                SakiComponents.DoubleCollapsePowerVariant, "self", "count", stacks, []),
            category: ComponentCategory.Growth);

    /// <summary>压力翻倍；若触发崩溃则获得 N 张骑士之剑。</summary>
    public static ComponentAtom DoubleCollapseSword(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.ConditionTemplate + "_double_sword",
            OperationScope.NonTargeted,
            $"使自身的[gold]压力[/gold]翻倍；如果因此触发[gold]崩溃[/gold]，将{count}张[gold]骑士之剑[/gold]加入手牌。",
            $"Double your [gold]Pressure[/gold]; if that triggers [gold]Breakdown[/gold], add {count} [gold]Knight Sword[/gold] to your hand.",
            SakiComponents.NumericSpec(SakiComponents.ConditionOpcode,
                SakiComponents.DoubleCollapseSwordVariant, "self", "count", count, []),
            category: ComponentCategory.Resource);

    /// <summary>压力翻倍；若触发崩溃则所有骑士之剑获得 N 点额外伤害。</summary>
    public static ComponentAtom DoubleCollapseSwordBoost(int bonus) =>
        SakiComponents.AtomWithSpec(SakiComponents.ConditionTemplate + "_double_sword_boost",
            OperationScope.NonTargeted,
            $"使自身的[gold]压力[/gold]翻倍；如果因此触发[gold]崩溃[/gold]，你所有的[gold]骑士之剑[/gold]获得{bonus}点额外伤害。",
            $"Double your [gold]Pressure[/gold]; if that triggers [gold]Breakdown[/gold], your [gold]Knight Swords[/gold] gain {bonus} extra damage.",
            SakiComponents.NumericSpec(SakiComponents.ConditionOpcode,
                SakiComponents.DoubleCollapseSwordBoostVariant, "self", "count", bonus, []),
            category: ComponentCategory.Growth);

    /// <summary>压力翻倍；若触发崩溃则获得 N 张回忆。</summary>
    public static ComponentAtom DoubleCollapseMemory(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.ConditionTemplate + "_double_memory",
            OperationScope.NonTargeted,
            $"使自身的[gold]压力[/gold]翻倍；如果因此触发[gold]崩溃[/gold]，获得{count}张[gold]回忆[/gold]。",
            $"Double your [gold]Pressure[/gold]; if that triggers [gold]Breakdown[/gold], gain {count} [gold]Memory[/gold].",
            SakiComponents.NumericSpec(SakiComponents.ConditionOpcode,
                SakiComponents.DoubleCollapseMemoryVariant, "self", "count", count, []),
            category: ComponentCategory.Resource);

    /// <summary>造成 N 伤害；若击杀则获得 M 张回忆。</summary>
    public static ComponentAtom DamageMemoryOnKill(int damage, int memory) =>
        SakiComponents.AtomWithSpec(SakiComponents.ConditionTemplate + "_memory_on_kill",
            OperationScope.SingleEnemyOnly,
            $"造成{damage}点伤害；若击杀敌人，则获得{memory}张[gold]回忆[/gold]。",
            $"Deal {damage} damage; if it kills, gain {memory} [gold]Memory[/gold].",
            SakiComponents.TwoSlotSpec(SakiComponents.ConditionOpcode, SakiComponents.DamageMemoryOnKillVariant,
                "selected_enemy", "damage", damage, "count", memory, []),
            requiresSingleTarget: true,
            category: ComponentCategory.Damage);

    /// <summary>造成 N 伤害；若击杀则获得 M 张骑士之剑。</summary>
    public static ComponentAtom DamageSwordOnKill(int damage, int swords) =>
        SakiComponents.AtomWithSpec(SakiComponents.ConditionTemplate + "_sword_on_kill",
            OperationScope.SingleEnemyOnly,
            $"造成{damage}点伤害；若击杀敌人，则将{swords}张[gold]骑士之剑[/gold]加入手牌。",
            $"Deal {damage} damage; if it kills, add {swords} [gold]Knight Sword[/gold] to your hand.",
            SakiComponents.TwoSlotSpec(SakiComponents.ConditionOpcode, SakiComponents.DamageSwordOnKillVariant,
                "selected_enemy", "damage", damage, "count", swords, []),
            requiresSingleTarget: true,
            category: ComponentCategory.Damage);

    /// <summary>造成 N 伤害；若未被完全格挡则施加 M 层易伤。</summary>
    public static ComponentAtom DamageVulnerableIfUnblocked(int damage, int vulnerable) =>
        SakiComponents.AtomWithSpec(SakiComponents.ConditionTemplate + "_vuln_if_unblocked",
            OperationScope.SingleEnemyOnly,
            $"造成{damage}点伤害；若伤害未被完全格挡，给予{@vulnerable}层[gold]易伤[/gold]。",
            $"Deal {damage} damage; if it isn't fully blocked, apply {@vulnerable} [gold]Vulnerable[/gold].",
            SakiComponents.TwoSlotSpec(SakiComponents.ConditionOpcode,
                SakiComponents.DamageVulnerableIfUnblockedVariant,
                "selected_enemy", "damage", damage, "count", vulnerable, []),
            requiresSingleTarget: true,
            category: ComponentCategory.Status);

    /// <summary>造成 N 伤害；若击杀则获得 M 点能量（本体「为什么不帮我」）。</summary>
    public static ComponentAtom DamageEnergyOnKill(int damage, int energy) =>
        SakiComponents.AtomWithSpec(SakiComponents.ConditionTemplate + "_energy_on_kill",
            OperationScope.SingleEnemyOnly,
            $"造成{damage}点伤害；若击杀敌人，则获得{{energyPrefix:energyIcons({energy})}}。",
            $"Deal {damage} damage; if it kills, gain {energy} energy.",
            SakiComponents.TwoSlotSpec(SakiComponents.ConditionOpcode, SakiComponents.DamageEnergyOnKillVariant,
                "selected_enemy", "damage", damage, "energy", energy, []),
            requiresSingleTarget: true,
            category: ComponentCategory.Damage);

    /// <summary>造成 N 伤害；若目标压力高于自身则给予 M 层缩小（本体「真是高高在上」）。</summary>
    public static ComponentAtom DamageShrinkIfLessPressure(int damage, int shrink) =>
        SakiComponents.AtomWithSpec(SakiComponents.ConditionTemplate + "_shrink",
            OperationScope.SingleEnemyOnly,
            $"造成{damage}点伤害；若目标的[gold]压力[/gold]大于自身，则给予其{shrink}层[gold]缩小[/gold]。",
            $"Deal {damage} damage; if the target has more [gold]Pressure[/gold] than you, apply {shrink} [gold]Shrink[/gold].",
            SakiComponents.TwoSlotSpec(SakiComponents.ConditionOpcode,
                SakiComponents.DamageShrinkIfLessPressureVariant,
                "selected_enemy", "damage", damage, "shrink", shrink, []),
            requiresSingleTarget: true,
            category: ComponentCategory.Damage);

    /// <summary>
    /// 压力比较通用原子：造成伤害；若目标压力比自身大/小，则触发 payoff。
    /// payoff 可以是缩小 / 易伤 / 压力 / 翻倍 / 额外伤害 / 格挡 / 抽牌 / 能量。
    /// 同一套条件只写一次，收益由 payoff 决定，避免为每种收益复制一套条件逻辑。
    /// </summary>
    public static ComponentAtom PressureCompare(int damage, int value, string payoff, bool more)
    {
        var direction = more ? "大于" : "小于";
        var englishDirection = more ? "more" : "less";
        var suffix = more ? "more" : "less";
        var payoffChinese = payoff switch
        {
            "shrink" => $"给予其{value}层[gold]缩小[/gold]",
            "vulnerable" => $"给予其{value}层[gold]易伤[/gold]",
            "pressure" => $"给予其{value}层[gold]压力[/gold]",
            "doublepressure" => "使其[gold]压力[/gold]翻倍",
            "bonusdamage" => $"额外造成{value}点伤害",
            "block" => $"获得{value}点[gold]格挡[/gold]",
            "draw" => $"抽{value}张牌",
            "energy" => $"获得{{energyPrefix:energyIcons({value})}}",
            _ => string.Empty
        };
        var payoffEnglish = payoff switch
        {
            "shrink" => $"apply {value} [gold]Shrink[/gold]",
            "vulnerable" => $"apply {value} [gold]Vulnerable[/gold]",
            "pressure" => $"apply {value} [gold]Pressure[/gold]",
            "doublepressure" => "double its [gold]Pressure[/gold]",
            "bonusdamage" => $"deal {value} additional damage",
            "block" => $"gain {value} [gold]Block[/gold]",
            "draw" => $"draw {value}",
            "energy" => $"gain {value} energy",
            _ => string.Empty
        };
        var slotId = payoff switch
        {
            "block" => "block",
            "draw" => "draw",
            "energy" => "energy",
            "bonusdamage" => "bonus",
            _ => "count"
        };
        var category = payoff switch
        {
            "block" or "draw" or "energy" => ComponentCategory.Resource,
            "vulnerable" or "pressure" or "doublepressure" => ComponentCategory.Status,
            _ => ComponentCategory.Damage
        };

        return SakiComponents.AtomWithSpec(
            SakiComponents.ConditionTemplate + $"_pressure_{suffix}_{payoff}",
            OperationScope.SingleEnemyOnly,
            $"造成{damage}点伤害；若目标的[gold]压力[/gold]{direction}自身，则{payoffChinese}。",
            $"Deal {damage} damage; if the target has {englishDirection} [gold]Pressure[/gold] than you, {payoffEnglish}.",
            payoff == "doublepressure"
                ? SakiComponents.NumericSpec(SakiComponents.ConditionOpcode,
                    $"pressure_{suffix}_{payoff}", "selected_enemy", "damage", damage, [])
                : SakiComponents.TwoSlotSpec(SakiComponents.ConditionOpcode,
                    $"pressure_{suffix}_{payoff}", "selected_enemy",
                    "damage", damage, slotId, value, []),
            requiresSingleTarget: true,
            category: category);
    }

    /// <summary>
    /// 通用「伤害 + 条件收益」原子：
    /// condition = unblocked / blocked / kill / notkill；
    /// payoff = block / draw / energy / bonusdamage。
    /// 条件和收益拆开组合，避免为每种组合复制一套判断代码。
    /// </summary>
    public static ComponentAtom DamageConditionPayoff(int damage, int value, string condition, string payoff)
    {
        var conditionChinese = condition switch
        {
            "unblocked" => "若伤害未被完全格挡",
            "blocked" => "若伤害被完全格挡",
            "kill" => "若击杀敌人",
            "notkill" => "若未击杀敌人",
            _ => string.Empty
        };
        var conditionEnglish = condition switch
        {
            "unblocked" => "if it isn't fully blocked",
            "blocked" => "if it is fully blocked",
            "kill" => "if it kills the target",
            "notkill" => "if it doesn't kill the target",
            _ => string.Empty
        };
        var payoffChinese = payoff switch
        {
            "block" => $"获得{value}点[gold]格挡[/gold]",
            "draw" => $"抽{value}张牌",
            "energy" => $"获得{{energyPrefix:energyIcons({value})}}",
            "bonusdamage" => $"额外造成{value}点伤害",
            _ => string.Empty
        };
        var payoffEnglish = payoff switch
        {
            "block" => $"gain {value} [gold]Block[/gold]",
            "draw" => $"draw {value}",
            "energy" => $"gain {value} energy",
            "bonusdamage" => $"deal {value} additional damage",
            _ => string.Empty
        };
        var slotId = payoff switch
        {
            "block" => "block",
            "draw" => "draw",
            "energy" => "energy",
            "bonusdamage" => "bonus",
            _ => "count"
        };
        var category = payoff switch
        {
            "block" or "draw" or "energy" => ComponentCategory.Resource,
            _ => ComponentCategory.Damage
        };

        var opcode = condition == "blocked"
            ? SakiComponents.ConditionBlockedOpcode
            : SakiComponents.ConditionOpcode;
        return SakiComponents.AtomWithSpec(
            SakiComponents.ConditionTemplate + $"_{condition}_{payoff}",
            OperationScope.SingleEnemyOnly,
            $"造成{damage}点伤害；{conditionChinese}，{payoffChinese}。",
            $"Deal {damage} damage; {conditionEnglish}, {payoffEnglish}.",
            SakiComponents.TwoSlotSpec(opcode,
                $"{condition}_{payoff}", "selected_enemy",
                "damage", damage, slotId, value, []),
            requiresSingleTarget: true,
            category: category);
    }

    /// <summary>造成 N 伤害；若目标压力小于自身则给予 M 层缩小（对称拓展）。</summary>
    public static ComponentAtom DamageShrinkIfMorePressure(int damage, int shrink) =>
        SakiComponents.AtomWithSpec(SakiComponents.ConditionTemplate + "_shrink_more",
            OperationScope.SingleEnemyOnly,
            $"造成{damage}点伤害；若目标的[gold]压力[/gold]小于自身，则给予其{shrink}层[gold]缩小[/gold]。",
            $"Deal {damage} damage; if the target has less [gold]Pressure[/gold] than you, apply {shrink} [gold]Shrink[/gold].",
            SakiComponents.TwoSlotSpec(SakiComponents.ConditionOpcode,
                SakiComponents.DamageShrinkIfMorePressureVariant,
                "selected_enemy", "damage", damage, "shrink", shrink, []),
            requiresSingleTarget: true,
            category: ComponentCategory.Damage);

    /// <summary>造成 N 伤害；若伤害被完全格挡则施加 M 层易伤（对称拓展）。</summary>
    public static ComponentAtom DamageVulnerableIfBlocked(int damage, int vulnerable) =>
        SakiComponents.AtomWithSpec(SakiComponents.ConditionTemplate + "_blocked_vuln",
            OperationScope.SingleEnemyOnly,
            $"造成{damage}点伤害；若伤害被完全格挡，给予{@vulnerable}层[gold]易伤[/gold]。",
            $"Deal {damage} damage; if it is fully blocked, apply {@vulnerable} [gold]Vulnerable[/gold].",
            SakiComponents.TwoSlotSpec(SakiComponents.ConditionOpcode,
                SakiComponents.DamageVulnerableIfBlockedVariant,
                "selected_enemy", "damage", damage, "count", vulnerable, []),
            requiresSingleTarget: true,
            category: ComponentCategory.Status);

    /// <summary>造成 N 伤害；若伤害被完全格挡则抽 M 张（对称拓展）。</summary>
    public static ComponentAtom DamageDrawIfBlocked(int damage, int draw) =>
        SakiComponents.AtomWithSpec(SakiComponents.ConditionTemplate + "_blocked_draw",
            OperationScope.SingleEnemyOnly,
            $"造成{damage}点伤害；若伤害被完全格挡，抽{draw}张牌。",
            $"Deal {damage} damage; if it is fully blocked, draw {draw}.",
            SakiComponents.TwoSlotSpec(SakiComponents.ConditionOpcode,
                SakiComponents.DamageDrawIfBlockedVariant,
                "selected_enemy", "damage", damage, "draw", draw, []),
            requiresSingleTarget: true,
            category: ComponentCategory.Damage);

    /// <summary>造成 N 伤害；若击杀则演奏所有已记忆和弦（本体「全都会了！」）。</summary>
    public static ComponentAtom DamagePlayMemorizedOnKill(int damage) =>
        SakiComponents.AtomWithSpec(SakiComponents.ConditionTemplate + "_play_on_kill",
            OperationScope.SingleEnemyOnly,
            $"造成{damage}点伤害；若击杀敌人，则演奏所有[gold]已记忆和弦[/gold]。",
            $"Deal {damage} damage; if it kills, play all your memorized [gold]Chords[/gold].",
            SakiComponents.NumericSpec(SakiComponents.ConditionOpcode,
                SakiComponents.DamagePlayMemorizedOnKillVariant, "selected_enemy", "damage", damage, []),
            requiresSingleTarget: true,
            category: ComponentCategory.Damage);

    /// <summary>造成 N 伤害；若未被完全格挡则抽 M 张（本体「试音打击」）。</summary>
    public static ComponentAtom DamageDrawIfUnblocked(int damage, int draw) =>
        SakiComponents.AtomWithSpec(SakiComponents.ConditionTemplate + "_draw_if_unblocked",
            OperationScope.SingleEnemyOnly,
            $"造成{damage}点伤害；若伤害未被完全格挡，抽{draw}张牌。",
            $"Deal {damage} damage; if it isn't fully blocked, draw {draw}.",
            SakiComponents.TwoSlotSpec(SakiComponents.ConditionOpcode,
                SakiComponents.DamageDrawIfUnblockedVariant,
                "selected_enemy", "damage", damage, "draw", draw, []),
            requiresSingleTarget: true,
            category: ComponentCategory.Damage);

    /// <summary>对所有敌人造成 N 伤害；若自身无压力则改为回复 M 生命（本体「黑白分明」）。</summary>
    public static ComponentAtom DamageAllOrHeal(int damage, int heal) =>
        SakiComponents.AtomWithSpec(SakiComponents.ConditionTemplate + "_all_or_heal",
            OperationScope.NonTargeted,
            $"对所有敌人造成{damage}点伤害；若自身没有[gold]压力[/gold]，则改为回复{heal}点生命。",
            $"Deal {damage} damage to ALL enemies; if you have no [gold]Pressure[/gold], heal {heal} instead.",
            SakiComponents.TwoSlotSpec(SakiComponents.ConditionOpcode, SakiComponents.DamageAllOrHealVariant,
                "self", "damage", damage, "heal", heal, []),
            category: ComponentCategory.Damage);

    // ─────────────────────────────────────────────────────────────
    // A1 数值随压力缩放（T-014）
    // ⚠️ 保底值写进文本里（「至少4点」），否则压力为 0 时这张牌完全没效果。
    // ⚠️ 数值槽必须有（§3.52：无槽原子会被判零价值剪掉）。
    // ─────────────────────────────────────────────────────────────

    /// <summary>造成等同于遗忘堆牌数 N 倍的伤害（至少 4 点）。本体「不再幼稚的我」。</summary>
    public static ComponentAtom DamageByForgetPile(int multiplier) =>
        SakiComponents.AtomWithSpec(SakiComponents.ScaleTemplate + "_forget", OperationScope.SingleEnemyOnly,
            $"造成等同于[gold]遗忘堆[/gold]牌数{multiplier}倍的伤害（至少4点）。",
            $"Deal damage equal to {multiplier}x the number of cards in your [gold]Forget pile[/gold] (at least 4).",
            SakiComponents.NumericSpec(SakiComponents.ScaleOpcode, SakiComponents.DamageByForgetPileVariant,
                "selected_enemy", "count", multiplier, []),
            requiresSingleTarget: true,
            category: ComponentCategory.Damage);

    /// <summary>获得「存活生物数 × N」点金币（本体「T~G~W~集团」）。</summary>
    public static ComponentAtom GainGoldByCreatures(int per) =>
        SakiComponents.AtomWithSpec(SakiComponents.GoldTemplate + "_creatures", OperationScope.NonTargeted,
            $"获得存活生物数量{per}倍的点[gold]金币[/gold]。",
            $"Gain Gold equal to {per}x the number of living creatures.",
            SakiComponents.NumericSpec(SakiComponents.GoldOpcode, SakiComponents.GainGoldByCreaturesVariant,
                "self", "count", per, []),
            category: ComponentCategory.Resource);

    /// <summary>获得 N 点金币（本体「客服工作」）。</summary>
    public static ComponentAtom GainGold(int amount) =>
        SakiComponents.AtomWithSpec(SakiComponents.GoldTemplate, OperationScope.NonTargeted,
            $"获得{amount}点[gold]金币[/gold]。",
            $"Gain {amount} [gold]Gold[/gold].",
            SakiComponents.NumericSpec(SakiComponents.GoldOpcode, SakiComponents.GainGoldVariant,
                "self", "count", amount, []),
            category: ComponentCategory.Resource);

    /// <summary>消耗 1 层压力，全体敌人获得 N 层虚弱（本体 Symbol III）。</summary>
    /// <remarks>
    /// ⚠️ 必须用**两槽**（cost / amount）：文本里有两个数字，
    /// 只给一个槽时 AA 会把槽绑到**第一个**数字上，变成
    /// 「消耗N层…获得1层」（2026-11-10 实测踩到）。
    /// </remarks>
    /// <summary>
    /// 消耗 N 层压力，使全体敌人获得 2N 层虚弱（本体 Symbol III 的加强版）。
    /// </summary>
    /// <remarks>
    /// 用户口径（2026-11-10）：「消耗层数成本可以变大，相应的效果也要变得更好。」
    /// ⇒ 成本与效果**按比例联动**：消耗 N 层 → 虚弱 2N 层。
    /// 文本里只出现**一个数字**（成本），效果用「两倍」这种词描述，
    /// 避免 AA 把槽绑到错误的数字上（见上一版踩坑记录）。
    /// </remarks>
    public static ComponentAtom PayPressureWeakAll(int cost, int multiplier) =>
        SakiComponents.AtomWithSpec(SakiComponents.PayPressureTemplate + "_weak_all", OperationScope.NonTargeted,
            // 用户口径（2026-11-10）：**两个数字都随机**才有随机的感觉 ——
            //   「消耗 A 层，获得 A×B 层」。A 和 B 各自独立抽样，
            //   所以同样是「消耗压力换虚弱」，可以是 1×2、3×4、5×3 ……
            $"消耗{cost}层[gold]压力[/gold]，使全体敌人获得其{multiplier}倍的[gold]虚弱[/gold]。",
            $"Spend {cost} [gold]Pressure[/gold] to apply {multiplier}x that much [gold]Weak[/gold] to ALL enemies.",
            SakiComponents.TwoSlotSpec(SakiComponents.PayPressureOpcode,
                SakiComponents.PayPressureWeakAllVariant, "self",
                "cost", cost, "multiplier", multiplier, []),
            category: ComponentCategory.Status);

    /// <summary>使自身压力翻倍，并额外获得 N 层（本体「如梦一场」）。</summary>
    public static ComponentAtom DoublePressure(int extra) =>
        SakiComponents.AtomWithSpec(SakiComponents.ScaleTemplate, OperationScope.NonTargeted,
            $"使自身的[gold]压力[/gold]翻倍，并额外获得{extra}层。",
            $"Double your [gold]Pressure[/gold], then gain {extra} more.",
            SakiComponents.NumericSpec(SakiComponents.ScaleOpcode, SakiComponents.DoublePressureVariant,
                "self", "count", extra, []),
            category: ComponentCategory.Resource);

    /// <summary>消耗全部压力，每 N 层换 1 点能量（本体「于夜中重燃斗志」）。</summary>
    public static ComponentAtom SpendAllPressureEnergy(int perEnergy) =>
        SakiComponents.AtomWithSpec(SakiComponents.ScaleTemplate, OperationScope.NonTargeted,
            $"消耗全部[gold]压力[/gold]，每{perEnergy}层获得一点能量。",
            $"Lose all [gold]Pressure[/gold]; gain 1 point of energy per {perEnergy}.",
            SakiComponents.NumericSpec(SakiComponents.ScaleOpcode, SakiComponents.SpendAllPressureEnergyVariant,
                "self", "count", perEnergy, []),
            category: ComponentCategory.Resource);

    /// <summary>消耗全部压力换能量：1 层 = 1 点能量（**无槽版**）。
    /// 为什么单独做无槽版：带数值槽时 AA 会把数字变成 {Amount0:diff()}，
    /// 而这型原子的变量名对不上，卡面会露字（2026-11-10 实机「空利防御」）。
    /// 文案里不写数字就不会产生占位符。</summary>
    public static ComponentAtom SpendAllPressureEnergySimple() =>
        SakiComponents.AtomWithSpec(SakiComponents.ScaleTemplate + "_one", OperationScope.NonTargeted,
            "消耗全部[gold]压力[/gold]，获得等同于其层数的一点能量。",
            "Lose all [gold]Pressure[/gold]; gain that much energy.",
            SakiComponents.NoValueSpec(SakiComponents.ScaleOpcode, SakiComponents.SpendAllPressureEnergyOneVariant),
            category: ComponentCategory.Resource);

    /// <summary>造成等同于自身压力层数 N 倍的伤害（至少 4 点）。</summary>
    public static ComponentAtom DamageByPressure(int multiplier) =>
        SakiComponents.AtomWithSpec(SakiComponents.ScaleTemplate, OperationScope.SingleEnemyOnly,
            $"造成等同于自身[gold]压力[/gold]层数{multiplier}倍的伤害（至少4点）。",
            $"Deal damage equal to {multiplier}x your [gold]Pressure[/gold] (at least 4).",
            SakiComponents.NumericSpec(SakiComponents.ScaleOpcode, SakiComponents.DamageByPressureVariant,
                "selected_enemy", "count", multiplier, []),
            // ⚠️ 单目标原子必须显式带 requiresSingleTarget（否则会跑到无目标壳上）。
            // ⚠️ 分类**不能**写 Damage：AA 的伤害预算只认它自己的 deal_damage，
            //    自定义 opcode 标 Damage 会被伤害需求模型判为「零伤害」而永不选中
            //    （2026-11-10 实测：标 Damage 时 96 张里 0 次）。
            requiresSingleTarget: true,
            category: ComponentCategory.Automatic);

    /// <summary>获得等同于自身压力层数 N 倍的格挡（至少 5 点）。</summary>
    public static ComponentAtom BlockByPressure(int multiplier) =>
        SakiComponents.AtomWithSpec(SakiComponents.ScaleTemplate, OperationScope.NonTargeted,
            $"获得等同于自身[gold]压力[/gold]层数{multiplier}倍的[gold]格挡[/gold]（至少5点）。",
            $"Gain Block equal to {multiplier}x your [gold]Pressure[/gold] (at least 5).",
            SakiComponents.NumericSpec(SakiComponents.ScaleOpcode, SakiComponents.BlockByPressureVariant,
                "self", "count", multiplier, []),
            category: ComponentCategory.Defense);

    // ─────────────────────────────────────────────────────────────
    // 骑士之剑的两个额外用法（T-013）
    // 本体「磨刀 SharpenBlade」就是「升级所有剑」+「按压力给所有剑加伤」。
    // ─────────────────────────────────────────────────────────────

    /// <summary>获得 N 层压力，并「无论何处」把骑士之剑放入手牌（本体「骑士出鞘」）。</summary>
    public static ComponentAtom SwordFromAnywhere(int pressure) =>
        SakiComponents.AtomWithSpec(SakiComponents.TokenTemplate + "_sword_from_anywhere",
            OperationScope.NonTargeted,
            $"获得{pressure}层[gold]压力[/gold]，无论何处，将[gold]骑士之剑[/gold]放入手牌。",
            $"Gain {pressure} [gold]Pressure[/gold]; put a [gold]Knight Sword[/gold] into your hand from anywhere.",
            SakiComponents.NumericSpec(SakiComponents.TokenOpcode, SakiComponents.SwordFromAnywhereVariant,
                "self", "count", pressure, []),
            category: ComponentCategory.Resource);

    /// <summary>从抽牌堆打出 N 张琴奏牌（本体「琴奏：连奏」）。</summary>
    public static ComponentAtom PlayPianoFromDraw(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.TokenTemplate + "_play_piano", OperationScope.NonTargeted,
            $"从抽牌堆打出{count}张[gold]琴奏[/gold]牌。",
            $"Play {count} [gold]Piano[/gold] card(s) from your draw pile.",
            SakiComponents.NumericSpec(SakiComponents.TokenOpcode, SakiComponents.TokenPlayPianoVariant,
                "self", "count", count, []),
            category: ComponentCategory.Damage);

    /// <summary>打出并消耗遗忘堆中所有回忆（本体「燃烧回忆」）。</summary>
    public static ComponentAtom PlayForgetMemory(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.TokenTemplate + "_play_forget_mem", OperationScope.NonTargeted,
            $"打出并消耗[gold]遗忘堆[/gold]中的{count}张[gold]回忆[/gold]。",
            $"Play and exhaust {count} [gold]Memory[/gold] card(s) from your [gold]Forget pile[/gold].",
            SakiComponents.NumericSpec(SakiComponents.TokenOpcode, SakiComponents.TokenPlayForgetMemoryVariant,
                "self", "count", count, []),
            category: ComponentCategory.Damage);

    /// <summary>打出手牌中所有骑士之剑（本体「由我君临」）。</summary>
    public static ComponentAtom PlayAllSwords(int bonus) =>
        SakiComponents.AtomWithSpec(SakiComponents.TokenTemplate + "_play_swords", OperationScope.NonTargeted,
            $"打出手牌中所有[gold]骑士之剑[/gold]，每张额外打出{bonus}次。",
            $"Play every [gold]Knight Sword[/gold] in your hand; each plays {bonus} extra time(s).",
            SakiComponents.NumericSpec(SakiComponents.TokenOpcode, SakiComponents.TokenPlayAllSwordsVariant,
                "self", "count", bonus, []),
            category: ComponentCategory.Damage);

    /// <summary>打出手牌中所有骑士之剑，再消耗全部压力追加等量伤害（本体「以压力化利刃」）。</summary>
    public static ComponentAtom SwordPressureDamage(int minimum) =>
        SakiComponents.AtomWithSpec(SakiComponents.TokenTemplate + "_sword_pressure_damage",
            OperationScope.SingleEnemyOnly,
            $"打出手牌中所有[gold]骑士之剑[/gold]，并消耗所有[gold]压力[/gold]额外造成等量伤害（至少{minimum}点）。",
            $"Play all [gold]Knight Swords[/gold] in your hand, then deal damage equal to your [gold]Pressure[/gold] (at least {minimum}).",
            SakiComponents.NumericSpec(SakiComponents.TokenOpcode, SakiComponents.SwordPressureDamageVariant,
                "selected_enemy", "count", minimum, []),
            requiresSingleTarget: true,
            category: ComponentCategory.Damage);

    /// <summary>升级至多 N 张骑士之剑（手牌 / 抽牌堆 / 弃牌堆 / 消耗堆 / 遗忘堆）。</summary>
    /// <remarks>
    /// ⚠️ 必须带数值槽（「N 张」）。无槽版本实测 384 张里 0 次——
    /// AA 把零数值原子判为无价值直接剪掉（§3.52）。
    /// </remarks>
    public static ComponentAtom UpgradeSwords(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.TokenTemplate + "_sword_upgrade", OperationScope.NonTargeted,
            $"升级至多{count}张[gold]骑士之剑[/gold]。",
            $"Upgrade up to {count} [gold]Knight Sword[/gold](s).",
            SakiComponents.NumericSpec(SakiComponents.TokenOpcode, SakiComponents.SwordUpgradeVariant,
                "self", "count", count, []),
            category: ComponentCategory.Utility);

    /// <summary>所有骑士之剑获得额外伤害（基础值 + 每层压力再追加同量）。</summary>
    public static ComponentAtom BoostSwordsByPressure(int bonus) =>
        SakiComponents.AtomWithSpec(SakiComponents.TokenTemplate + "_sword_boost", OperationScope.NonTargeted,
            $"所有[gold]骑士之剑[/gold]获得{bonus}点额外伤害，每层[gold]压力[/gold]再追加相同数值。",
            $"All [gold]Knight Swords[/gold] gain {bonus} bonus damage, plus the same per [gold]Pressure[/gold].",
            SakiComponents.NumericSpec(SakiComponents.TokenOpcode, SakiComponents.SwordBoostVariant,
                "self", "count", bonus, []),
            category: ComponentCategory.Damage);

    /// <summary>
    /// 获得 N 个随机和弦槽位；若某个槽位已经有了，就演奏它。
    /// 本体写法（`Cards\Anon\Rare\TeamSunshine.cs`）：
    ///     var temp = chords.GetTemporaryChords();
    ///     if (temp.Contains(id)) await ChordNoteSystem.PlayChordAsync(...); else chords.AddTemporaryChord(id);
    /// </summary>
    public static ComponentAtom GrantChordSlot(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.ChordSlotTemplate, OperationScope.NonTargeted,
            $"随机储存{count}个[gold]和弦[/gold]到额外槽位。",
            $"Store {count} random [gold]Chord[/gold] into extra slots.",
            SakiComponents.NumericSpec(SakiComponents.PlayChordOpcode, SakiComponents.ChordGrantSlotVariant,
                "self", "count", count, []),
            category: ComponentCategory.Resource);

    /// <summary>升级战斗中所有猫咪，并把 N 张猫咪加入手牌（本体「撸猫」）。</summary>
    public static ComponentAtom UpgradeNeko(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.TokenTemplate + "_upgrade_neko", OperationScope.NonTargeted,
            $"将{count}张[gold]猫咪[/gold]加入手牌，并升级战斗中所有猫咪。",
            $"Add {count} [gold]Cat[/gold] to your hand and upgrade all Cats in combat.",
            SakiComponents.NumericSpec(SakiComponents.TokenOpcode, SakiComponents.TokenUpgradeNekoVariant,
                "self", "count", count, []),
            category: ComponentCategory.Resource);

    /// <summary>获得等同于「敌人数量 N 倍」的格挡（本体「旁若无人」）。</summary>
    public static ComponentAtom BlockByEnemyCount(int multiplier) =>
        SakiComponents.AtomWithSpec(SakiComponents.ScaleTemplate + "_enemies", OperationScope.NonTargeted,
            $"获得等同于敌人数量{multiplier}倍的[gold]格挡[/gold]（至少5点）。",
            $"Gain Block equal to {multiplier}x the number of enemies (at least 5).",
            SakiComponents.NumericSpec(SakiComponents.ScaleOpcode, SakiComponents.BlockByEnemyCountVariant,
                "self", "count", multiplier, []),
            category: ComponentCategory.Defense);

    /// <summary>清除所有音符，每清除 1 个获得 N 点格挡（本体「拨片坏了！？」）。</summary>
    public static ComponentAtom ClearScaleBlock(int blockPer) =>
        SakiComponents.AtomWithSpec(SakiComponents.NoteTemplate + "_clear_block", OperationScope.NonTargeted,
            $"清除所有{Icon("text_note")}，每清除1个获得{blockPer}点[gold]格挡[/gold]。",
            $"Clear all Notes; gain {blockPer} [gold]Block[/gold] per Note cleared.",
            SakiComponents.NumericSpec(SakiComponents.NoteOpcode, SakiComponents.ClearScaleBlockVariant,
                "self", "count", blockPer, []),
            category: ComponentCategory.Defense);

    /// <summary>清除所有音符，每清除 1 个抽 N 张（本体「喝水冷静」）。</summary>
    public static ComponentAtom ClearScaleDraw(int drawPer) =>
        SakiComponents.AtomWithSpec(SakiComponents.NoteTemplate + "_clear_draw", OperationScope.NonTargeted,
            $"清除所有{Icon("text_note")}，每清除1个抽{drawPer}张牌。",
            $"Clear all Notes; draw {drawPer} card per Note cleared.",
            SakiComponents.NumericSpec(SakiComponents.NoteOpcode, SakiComponents.ClearScaleDrawVariant,
                "self", "count", drawPer, []),
            category: ComponentCategory.CardManipulation);

    /// <summary>获得等同于「储存和弦数 N 倍」的格挡（本体「成功的演出」）。</summary>
    public static ComponentAtom BlockByStoredChords(int multiplier) =>
        SakiComponents.AtomWithSpec(SakiComponents.ScaleTemplate + "_chords", OperationScope.NonTargeted,
            $"获得等同于储存[gold]和弦[/gold]数量{multiplier}倍的[gold]格挡[/gold]（至少5点）。",
            $"Gain Block equal to {multiplier}x the number of stored [gold]Chords[/gold] (at least 5).",
            SakiComponents.NumericSpec(SakiComponents.ScaleOpcode, SakiComponents.BlockByStoredChordsVariant,
                "self", "count", multiplier, []),
            category: ComponentCategory.Defense);

    /// <summary>造成等同于自身格挡值的伤害（至少 4 点）。本体「把衣服剪开」。</summary>
    public static ComponentAtom DamageByBlock(int minimum) =>
        SakiComponents.AtomWithSpec(SakiComponents.ScaleTemplate + "_block", OperationScope.SingleEnemyOnly,
            $"造成等同于自身[gold]格挡[/gold]的伤害（至少{minimum}点）。",
            $"Deal damage equal to your [gold]Block[/gold] (at least {minimum}).",
            SakiComponents.NumericSpec(SakiComponents.ScaleOpcode, SakiComponents.DamageByBlockVariant,
                "selected_enemy", "count", minimum, []),
            requiresSingleTarget: true,
            category: ComponentCategory.Automatic);

    /// <summary>消耗手牌中所有猫咪，每张对随机敌人造成 N 点伤害（本体「猫咪大冲击」）。</summary>
    public static ComponentAtom ConsumeNekoDamage(int damage) =>
        SakiComponents.AtomWithSpec(SakiComponents.TokenTemplate + "_consume_neko", OperationScope.NonTargeted,
            $"消耗手牌中所有[gold]猫咪[/gold]，每张对随机敌人造成{damage}点伤害。",
            $"Exhaust all [gold]Cat[/gold] cards in your hand; deal {damage} damage to a random enemy per card.",
            SakiComponents.NumericSpec(SakiComponents.TokenOpcode, SakiComponents.TokenConsumeNekoDamageVariant,
                "self", "count", damage, []),
            category: ComponentCategory.Automatic);

    /// <summary>随机打出 N 张手牌（本体「提线木偶」）。</summary>
    public static ComponentAtom AutoPlayHand(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.DeckTemplate + "_autoplay", OperationScope.NonTargeted,
            $"随机打出{count}张手牌。",
            $"Randomly play {count} card(s) from your hand.",
            SakiComponents.NumericSpec(SakiComponents.DeckOpcode, SakiComponents.AutoPlayHandVariant,
                "self", "count", count, []),
            category: ComponentCategory.CardManipulation);

    /// <summary>遗忘所有手牌，每遗忘 1 张获得 1 张回忆（本体「忘却一切」）。</summary>
    public static ComponentAtom ForgetAllHand(int recallPer) =>
        SakiComponents.AtomWithSpec(SakiComponents.DeckTemplate + "_forget_all", OperationScope.NonTargeted,
            $"遗忘所有手牌，每遗忘{recallPer}张获得1张[gold]回忆[/gold]。",
            $"Forget your entire hand; gain 1 [gold]Memory[/gold] per {recallPer} card(s) forgotten.",
            SakiComponents.NumericSpec(SakiComponents.DeckOpcode, SakiComponents.ForgetAllHandVariant,
                "self", "count", recallPer, []),
            category: ComponentCategory.CardManipulation);

    /// <summary>从任何地方打出所有猫咪（抽牌堆 + 手牌 + 弃牌堆），本体「小猫出动」。</summary>
    public static ComponentAtom PlayNekoAnywhere(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.TokenTemplate + "_play_neko_any", OperationScope.NonTargeted,
            $"无论何处，打出{count}张[gold]猫咪[/gold]。",
            $"Play {count} [gold]Cat[/gold] card(s) from anywhere.",
            SakiComponents.NumericSpec(SakiComponents.TokenOpcode, SakiComponents.PlayNekoAnywhereVariant,
                "self", "count", count, []),
            category: ComponentCategory.Damage);

    /// <summary>随机打出手牌 N 张并遗忘（本体「暂且遗忘」）。</summary>
    public static ComponentAtom PlayHandThenForget(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.DeckTemplate + "_play_forget_hand", OperationScope.NonTargeted,
            $"随机打出手牌中的{count}张牌，并将其遗忘。",
            $"Randomly play {count} card(s) from your hand, then forget them.",
            SakiComponents.NumericSpec(SakiComponents.DeckOpcode, SakiComponents.PlayHandThenForgetVariant,
                "self", "count", count, []),
            category: ComponentCategory.CardManipulation);

    /// <summary>从消耗堆取 N 张牌加入手牌（本体「沉思」）。</summary>
    public static ComponentAtom RecallFromExhaust(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.DeckTemplate + "_recall_exhaust", OperationScope.NonTargeted,
            $"从消耗堆中选择{count}张牌加入手牌。",
            $"Put {count} card(s) from your exhaust pile into your hand.",
            SakiComponents.NumericSpec(SakiComponents.DeckOpcode, SakiComponents.RecallFromExhaustVariant,
                "self", "count", count, []),
            category: ComponentCategory.CardManipulation);

    /// <summary>消耗 1 层压力，获得 1 张回忆（本体「再度鼓励」）。</summary>
    public static ComponentAtom PayPressureRecall(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.PayPressureTemplate + "_recall", OperationScope.NonTargeted,
            $"若有则消耗1层[gold]压力[/gold]，获得{count}张[gold]回忆[/gold]。",
            $"If you have [gold]Pressure[/gold], spend 1 to gain {count} [gold]Memory[/gold].",
            SakiComponents.NumericSpec(SakiComponents.PayPressureOpcode, SakiComponents.PayPressureRecallVariant,
                "self", "count", count, []),
            category: ComponentCategory.Resource);

    /// <summary>造成等同于目标压力层数 N 倍的伤害（本体「Symbol II」）。</summary>
    public static ComponentAtom DamageByTargetPressure(int multiplier) =>
        SakiComponents.AtomWithSpec(SakiComponents.ScaleTemplate + "_target_pressure", OperationScope.SingleEnemyOnly,
            $"造成等同于目标[gold]压力[/gold]层数{multiplier}倍的伤害（至少4点）。",
            $"Deal damage equal to {multiplier}x the target's [gold]Pressure[/gold] (at least 4).",
            SakiComponents.NumericSpec(SakiComponents.ScaleOpcode, SakiComponents.DamageByTargetPressureVariant,
                "selected_enemy", "count", multiplier, []),
            requiresSingleTarget: true,
            category: ComponentCategory.Automatic);

    /// <summary>从遗忘堆取 N 张牌加入手牌，本回合免费（本体「重拾回忆」）。</summary>
    public static ComponentAtom RecallFromForget(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.DeckTemplate + "_recall_forget", OperationScope.NonTargeted,
            $"从[gold]遗忘堆[/gold]中选择{count}张牌加入手牌。",
            $"Put {count} card(s) from your [gold]Forget pile[/gold] into your hand; they cost 0 this turn.",
            SakiComponents.NumericSpec(SakiComponents.DeckOpcode, SakiComponents.RecallFromForgetVariant,
                "self", "count", count, []),
            category: ComponentCategory.CardManipulation);

    /// <summary>将抽牌堆 N 张牌直接放入手牌（本体「可以做任何事」）。</summary>
    public static ComponentAtom DrawToHand(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.DeckTemplate + "_draw_hand", OperationScope.NonTargeted,
            $"将抽牌堆中的{count}张牌放入手牌。",
            $"Put {count} card(s) from your draw pile into your hand.",
            SakiComponents.NumericSpec(SakiComponents.DeckOpcode, SakiComponents.DrawToHandVariant,
                "self", "count", count, []),
            category: ComponentCategory.CardManipulation);

    /// <summary>从弃牌堆遗忘 N 张（本体「我自己的问题」）。</summary>
    public static ComponentAtom ForgetFromDiscard(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.DeckTemplate + "_forget_discard", OperationScope.NonTargeted,
            $"从弃牌堆中遗忘{count}张牌。",
            $"Forget {count} card(s) from your discard pile.",
            SakiComponents.NumericSpec(SakiComponents.DeckOpcode, SakiComponents.ForgetFromDiscardVariant,
                "self", "count", count, []),
            category: ComponentCategory.CardManipulation);

    /// <summary>消耗 N 层压力，永久获得 M 点最大生命（本体「阅读」）。</summary>
    public static ComponentAtom PressureMaxHp(int pressure, int maxHp) =>
        SakiComponents.AtomWithSpec(SakiComponents.DeckTemplate + "_max_hp", OperationScope.NonTargeted,
            $"消耗{pressure}层[gold]压力[/gold]，永久获得{maxHp}点最大生命。",
            $"Spend {pressure} [gold]Pressure[/gold] to permanently gain {maxHp} Max HP.",
            SakiComponents.TwoSlotSpec(SakiComponents.DeckOpcode, SakiComponents.PressureMaxHpVariant,
                "self", "pressure", pressure, "maxhp", maxHp, []),
            category: ComponentCategory.Utility);

    /// <summary>将 N 张猫咪加入手牌，并使它们获得保留（本体「和猫发呆」）。</summary>
    public static ComponentAtom NekoRetain(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.TokenTemplate + "_neko_retain", OperationScope.NonTargeted,
            $"将{count}张[gold]猫咪[/gold]加入手牌，并使你的[gold]猫咪[/gold]获得保留。",
            $"Add {count} [gold]Cat[/gold] to your hand; your [gold]Cats[/gold] gain Retain.",
            SakiComponents.NumericSpec(SakiComponents.TokenOpcode, SakiComponents.TokenNekoRetainVariant,
                "self", "count", count, []),
            category: ComponentCategory.Resource);

    /// <summary>获得 N 张猫咪，并获得 M 点格挡。</summary>
    public static ComponentAtom AddNekoAndBlock(int count, int block) =>
        SakiComponents.AtomWithSpec(SakiComponents.TokenTemplate + "_neko_block", OperationScope.NonTargeted,
            $"获得{count}张[gold]猫咪[/gold]，获得{block}点[gold]格挡[/gold]。",
            $"Gain {count} [gold]Cat[/gold] and {block} [gold]Block[/gold].",
            SakiComponents.TwoSlotSpec(SakiComponents.TokenOpcode, SakiComponents.TokenNekoBlockVariant,
                "self", "count", count, "block", block, []),
            category: ComponentCategory.Defense);

    /// <summary>用回忆填满手牌（本体「被回忆攻击」）。</summary>
    public static ComponentAtom FillHandRecall(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.DeckTemplate + "_fill_recall", OperationScope.NonTargeted,
            $"用[gold]回忆[/gold]填满手牌（至少{count}张）。",
            $"Fill your hand with [gold]Memory[/gold] cards (at least {count}).",
            SakiComponents.NumericSpec(SakiComponents.DeckOpcode, SakiComponents.FillHandRecallVariant,
                "self", "count", count, []),
            category: ComponentCategory.CardManipulation);

    /// <summary>从抽牌堆随机打出 N 张牌并遗忘（本体「唯有忘却」）。</summary>
    public static ComponentAtom PlayAndForget(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.DeckTemplate + "_play_forget", OperationScope.NonTargeted,
            $"从抽牌堆随机打出{count}张牌，并将其遗忘。",
            $"Randomly play {count} card(s) from your draw pile, then forget them.",
            SakiComponents.NumericSpec(SakiComponents.DeckOpcode, SakiComponents.PlayAndForgetVariant,
                "self", "count", count, []),
            category: ComponentCategory.CardManipulation);

    /// <summary>减少 N 层压力，抽 M 张牌（本体「积郁成泣」）。</summary>
    public static ComponentAtom RelievePressureDraw(int pressure, int draw) =>
        SakiComponents.AtomWithSpec(SakiComponents.DeckTemplate + "_relieve", OperationScope.NonTargeted,
            $"减少{pressure}层[gold]压力[/gold]，抽{draw}张牌。",
            $"Lose {pressure} [gold]Pressure[/gold]; draw {draw} card(s).",
            SakiComponents.TwoSlotSpec(SakiComponents.DeckOpcode, SakiComponents.RelievePressureDrawVariant,
                "self", "pressure", pressure, "draw", draw, []),
            category: ComponentCategory.CardManipulation);

    /// <summary>随机清除身上一个负面效果（本体「泡澡」）。</summary>
    public static ComponentAtom ClearDebuff(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.DeckTemplate + "_clear_debuff", OperationScope.NonTargeted,
            $"随机清除身上{count}个负面效果。",
            $"Remove {count} random negative effect(s) from yourself.",
            SakiComponents.NumericSpec(SakiComponents.DeckOpcode, SakiComponents.ClearDebuffVariant,
                "self", "count", count, []),
            category: ComponentCategory.Utility);

    /// <summary>丢弃手牌中所有「与抹茶芭菲无关」的牌，每张抽 1（本体「挑食」）。</summary>
    public static ComponentAtom DiscardNonParfait(int drawPer) =>
        SakiComponents.AtomWithSpec(SakiComponents.DeckTemplate + "_non_parfait", OperationScope.NonTargeted,
            $"丢弃手牌中所有与[gold]抹茶芭菲[/gold]无关的牌，每丢弃1张抽{drawPer}张牌。",
            $"Discard every card in your hand unrelated to [gold]Matcha Parfait[/gold]; draw {drawPer} per card.",
            SakiComponents.NumericSpec(SakiComponents.DeckOpcode, SakiComponents.DiscardNonParfaitVariant,
                "self", "count", drawPer, []),
            category: ComponentCategory.CardManipulation);

    /// <summary>消耗 1 张手牌，视为食用 1 杯抹茶芭菲（本体「猫饿了」）。</summary>
    public static ComponentAtom ConsumeAsParfait(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.ParfaitTemplate + "_consume", OperationScope.NonTargeted,
            $"消耗手牌中{count}张牌，视为食用等量的[gold]抹茶芭菲[/gold]。",
            $"Exhaust {count} card(s) from your hand and treat them as eaten [gold]Matcha Parfait[/gold].",
            SakiComponents.NumericSpec(SakiComponents.ParfaitOpcode, SakiComponents.ConsumeAsParfaitVariant,
                "self", "count", count, []),
            category: ComponentCategory.Resource);

    /// <summary>从抽牌堆随机打出 N 张猫咪（本体「猫咪打击」）。</summary>
    public static ComponentAtom PlayNekoFromDraw(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.TokenTemplate + "_play_neko", OperationScope.NonTargeted,
            $"从抽牌堆随机打出{count}张[gold]猫咪[/gold]。",
            $"Randomly play {count} [gold]Cat[/gold] card(s) from your draw pile.",
            SakiComponents.NumericSpec(SakiComponents.TokenOpcode, SakiComponents.PlayNekoFromDrawVariant,
                "self", "count", count, []),
            category: ComponentCategory.Damage);

    /// <summary>获得 N 个随机音符（本体「练习...练习...」）。</summary>
    public static ComponentAtom RandomNotes(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.NoteTemplate + "_random", OperationScope.NonTargeted,
            $"获得{count}个随机{Icon("text_note")}。",
            $"Gain {count} random Note(s).",
            SakiComponents.NumericSpec(SakiComponents.NoteOpcode, SakiComponents.RandomNoteVariant,
                "self", "count", count, []),
            category: ComponentCategory.Resource);

    /// <summary>消耗手牌中所有状态牌，每张抽 N 张（本体「讨厌的东西说拜拜」）。</summary>
    public static ComponentAtom ExhaustStatusDraw(int drawPer) =>
        SakiComponents.AtomWithSpec(SakiComponents.DeckTemplate + "_status", OperationScope.NonTargeted,
            $"消耗手牌中所有状态牌，每消耗1张抽{drawPer}张牌。",
            $"Exhaust all Status cards in your hand; draw {drawPer} per card exhausted.",
            SakiComponents.NumericSpec(SakiComponents.DeckOpcode, SakiComponents.ExhaustStatusVariant,
                "self", "count", drawPer, []),
            category: ComponentCategory.CardManipulation);

    /// <summary>把 N 张本牌的复制加入弃牌堆（本体「小小的瞬间」）。</summary>
    public static ComponentAtom CopySelf(int copies) =>
        SakiComponents.AtomWithSpec(SakiComponents.DeckTemplate + "_copy", OperationScope.NonTargeted,
            $"将{copies}张本牌的复制加入弃牌堆。",
            $"Add {copies} cop{(copies == 1 ? "y" : "ies")} of this card to your discard pile.",
            SakiComponents.NumericSpec(SakiComponents.DeckOpcode, SakiComponents.CopySelfVariant,
                "self", "count", copies, []),
            category: ComponentCategory.CardManipulation);

    /// <summary>消耗 N 层压力，将该牌返回手牌（本体「泡澡」/「舞台上的自信」）。</summary>
    public static ComponentAtom ReturnToHand(int pressure) =>
        SakiComponents.AtomWithSpec(SakiComponents.DeckTemplate + "_return", OperationScope.NonTargeted,
            $"消耗{pressure}层[gold]压力[/gold]，将此牌返回手牌。",
            $"Spend {pressure} [gold]Pressure[/gold] to return this card to your hand.",
            SakiComponents.NumericSpec(SakiComponents.DeckOpcode, SakiComponents.ReturnToHandVariant,
                "self", "count", pressure, []),
            category: ComponentCategory.CardManipulation);

    /// <summary>丢弃所有手牌，获得 N 点格挡（本体「意外事件」）。</summary>
    public static ComponentAtom DiscardAllHand(int block) =>
        SakiComponents.AtomWithSpec(SakiComponents.DeckTemplate, OperationScope.NonTargeted,
            $"丢弃所有手牌，获得{block}点[gold]格挡[/gold]。",
            $"Discard your entire hand, then gain {block} [gold]Block[/gold].",
            SakiComponents.NumericSpec(SakiComponents.DeckOpcode, SakiComponents.DiscardAllVariant,
                "self", "count", block, []),
            category: ComponentCategory.Defense);

    /// <summary>花 N 金币买 1 杯抹茶芭菲（本体「买芭菲」）。</summary>
    public static ComponentAtom BuyParfait(int gold) =>
        SakiComponents.AtomWithSpec(SakiComponents.ParfaitTemplate + "_buy", OperationScope.NonTargeted,
            $"花费{gold}点[gold]金币[/gold]，购买1杯[gold]抹茶芭菲[/gold]。",
            $"Spend {gold} [gold]Gold[/gold] to buy 1 [gold]Matcha Parfait[/gold].",
            SakiComponents.NumericSpec(SakiComponents.ParfaitOpcode, SakiComponents.ParfaitBuyVariant,
                "self", "count", gold, []),
            category: ComponentCategory.Resource);

    /// <summary>把 N 张猫咪加入抽牌堆（本体「猫咪宝藏」「撸猫」）。</summary>
    public static ComponentAtom NekoToDraw(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.TokenTemplate + "_neko_draw", OperationScope.NonTargeted,
            $"将{count}张[gold]猫咪[/gold]加入抽牌堆。",
            $"Add {count} [gold]Cat[/gold] card(s) to your draw pile.",
            SakiComponents.NumericSpec(SakiComponents.TokenOpcode, SakiComponents.TokenNekoDrawVariant,
                "self", "count", count, []),
            category: ComponentCategory.Resource);

    /// <summary>把 N 张猫咪分别加入抽牌堆 / 手牌 / 弃牌堆（本体「好多猫咪」）。</summary>
    public static ComponentAtom NekoToAllPiles(int count) =>
        SakiComponents.AtomWithSpec(SakiComponents.TokenTemplate + "_neko_all", OperationScope.NonTargeted,
            $"在抽牌堆、手牌和弃牌堆各添加{count}张[gold]猫咪[/gold]。",
            $"Add {count} [gold]Cat[/gold] card to your draw pile, hand and discard pile.",
            SakiComponents.NumericSpec(SakiComponents.TokenOpcode, SakiComponents.TokenNekoAllPilesVariant,
                "self", "count", count, []),
            category: ComponentCategory.Resource);

    // ─────────────────────────────────────────────────────────────
    // 触发原子（T-015 第一批）
    //
    // ⚠️ 触发器的语义**靠 template 名判断**（AA 源码里真有
    //    `trigger.Template == "A:turnStart"` 这种比较，ComponentAssemblyGenerator.cs:4781），
    //    所以必须用原生 template 名 "A:turnStart" / "A:turnEnd"，
    //    不能像自家机制那样起 csk:xxx。
    //    原生写法的中文文本是 "在你的回合开始时。"（后缀带句号）。
    // ─────────────────────────────────────────────────────────────

    /// <summary>触发器：在你的回合开始时。</summary>
    public static ComponentAtom TurnStartTrigger() =>
        SakiComponents.AtomWithSpec("A:turnStart", OperationScope.AbilityTrigger,
            "在你的回合开始时。", "At the start of your turn.",
            SakiComponents.TriggerSpec(),
            category: ComponentCategory.Utility);

    /// <summary>触发器：在你的回合结束时。</summary>
    public static ComponentAtom TurnEndTrigger() =>
        SakiComponents.AtomWithSpec("A:turnEnd", OperationScope.AbilityTrigger,
            "在你的回合结束时。", "At the end of your turn.",
            SakiComponents.TriggerSpec(),
            category: ComponentCategory.Utility);

    // ─────────────────────────────────────────────────────────────
    // 底层构造
    // ─────────────────────────────────────────────────────────────

    private static ComponentAtom Atom(string template, OperationScope scope, string chinese, string english,
        bool requiresSingleTarget = false, ComponentCategory category = ComponentCategory.Automatic) =>
        SakiComponents.Atom(template, scope, chinese, english, requiresSingleTarget, category: category);
}

/// <summary>爱音的音符类型（对应 zhs 本地化的四张图）。</summary>
public enum NoteKind
{
    Attack,
    Skill,
    Power,
    Special
}
























































































































