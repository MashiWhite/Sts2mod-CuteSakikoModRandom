using ChaosCardGenerator;
using System.Text.RegularExpressions;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 四个「小祥专属机制」的稳定 ASCII 标识。
///
/// 设计要点：
/// - opcode / variant / flag / 数值槽 ID 必须是小写 ASCII（AA 的硬性校验）。
/// - 这些机制**没有**原版对应物，因此必须自己注册 ComponentRuntimeApi 的处理器 + ComponentValuationApi 的估值。
/// - Template（授权用）也叫 template，"csk:" 前缀防止和其他扩展包撞车。
/// </summary>
public static class SakiComponents
{
    // ── 压力（Pressure）：祥子主资源 ──
    // 施加压力到目标（敌人或自己）。压力按层数放大承受的伤害，到达当前生命时崩溃。
    public const string PressureTemplate = "csk:pressure";
    public const string PressureOpcode = "csk_pressure";
    public const string PressureVariant = "apply";
    public const string PressureAllVariant = "apply_all";

    // ── 记忆（Memory）：祥子的牌堆外资源 ──
    // 把牌「遗忘」进遗忘堆，并减少压力；或从记忆堆「回忆」回手牌。
    // 注意：遗忘分「玩家选择」与「随机」两种语义，必须用不同 variant
    // （本体的写法惯例：「遗忘1张牌」默认=玩家选择；随机要明写「随机遗忘」）。
    public const string ForgetTemplate = "csk:forget";
    public const string ForgetOpcode = "csk_forget";
    public const string ForgetVariant = "selected";
    public const string ForgetRandomVariant = "random";
    public const string RecallTemplate = "csk:recall";
    public const string RecallOpcode = "csk_recall";
    public const string RecallVariant = "random";

    // ── 和弦（Chord）：爱音的主资源 ──
    // 演奏已储存的和弦；储存/学习统一走和弦存储能力。
    public const string PlayChordTemplate = "csk:play_chord";
    public const string PlayChordOpcode = "csk_play_chord";
    public const string PlayChordStoredVariant = "stored";
    /// <summary>演奏储存和弦 2~3 次（单独定价，避免与 1 次共用估值导致总数超预算）。</summary>
    public const string PlayChordStoredMultiVariant = "stored_multi";
    public const string PlayChordRandomVariant = "random_memorized";

    // ── 音符（Note）：爱音的次级资源 ──
    // 音符是 CardType.Attack/Skill/Power 的记号，清空时按类型触发效果。
    // 四种类型各有自己的图标与效果（见 zhs 本地化 CUTE_SAKIKO_MOD_KEYWORD_NOTEIFY），
    // 所以 variant 必须区分类型，不能共用一个 "add"。
    public const string NoteTemplate = "csk:note";
    public const string NoteOpcode = "csk_note";
    public const string NoteAddAttackVariant = "add_attack";
    public const string NoteAddSkillVariant = "add_skill";
    public const string NoteAddPowerVariant = "add_power";
    public const string NoteAddSpecialVariant = "add_special";
    public const string NoteClearVariant = "clear";

    /// <summary>音符类型 → opcode variant。</summary>
    public static string NoteVariantFor(NoteKind kind) => kind switch
    {
        NoteKind.Attack => NoteAddAttackVariant,
        NoteKind.Skill => NoteAddSkillVariant,
        NoteKind.Power => NoteAddPowerVariant,
        _ => NoteAddSpecialVariant
    };

    // ── 芭菲（Parfait）：乐奈的主资源 ──
    // 消耗杯数（吃芭菲），触发所有「吃杯时」监听。
    public const string ParfaitTemplate = "csk:parfait";
    public const string ParfaitOpcode = "csk_parfait";
    public const string ParfaitEatVariant = "eat";
    /// <summary>花 N 金币买 1 杯抹茶芭菲（本体「买芭菲」）。</summary>
    public const string ParfaitBuyVariant = "buy";
    public const string ParfaitChargeVariant = "charge";

    // ★ 令牌牌机制（2026-11-09 用户要求「效果拆完整，乐奈要有猫咪牌、祥子要有骑士之剑」）：
    //   本体里「获得猫咪牌 / 骑士之剑」都是「从令牌卡池里取一张同类卡进手牌」，
    //   令牌池 = CuteSakikoTokenCardPool，用关键词区分（Neko / KnightSword）。
    public const string TokenTemplate = "csk:token";
    public const string TokenOpcode = "csk_token";
    public const string TokenNekoVariant = "neko";
    public const string TokenSwordVariant = "sword";
    public const string TokenCandyVariant = "candy";
    /// <summary>把猫咪加入抽牌堆（本体「猫咪宝藏」「撸猫」）。</summary>
    public const string TokenNekoDrawVariant = "neko_draw";
    public const string TokenNekoBlockVariant = "neko_block";
    /// <summary>把猫咪同时加入抽牌堆 / 手牌 / 弃牌堆（本体「好多猫咪」）。</summary>
    public const string TokenNekoAllPilesVariant = "neko_all_piles";
    /// <summary>消耗手牌中所有猫咪，每张对随机敌人造成 N 点伤害（本体「猫咪大冲击」）。</summary>
    public const string TokenConsumeNekoDamageVariant = "consume_neko_damage";
    /// <summary>升级战斗中所有猫咪，并加入 N 张（本体「撸猫」）。</summary>
    public const string TokenUpgradeNekoVariant = "upgrade_neko";
    /// <summary>获得等同于敌人数量 N 倍的格挡（本体「旁若无人」）。</summary>
    public const string BlockByEnemyCountVariant = "block_by_enemies";
    /// <summary>清除所有音符，每清除 1 个获得 N 点格挡 / 抽 N 张（本体「拨片坏了」「喝水冷静」）。</summary>
    public const string ClearScaleBlockVariant = "clear_scale_block";
    public const string ClearScaleDrawVariant = "clear_scale_draw";
    /// <summary>获得等同于储存和弦数 N 倍的格挡（本体「成功的演出」）。</summary>
    public const string BlockByStoredChordsVariant = "block_by_stored_chords";

    // ★ 消耗压力换额外效果（2026-11-09 用户要求）：
    //   本体写法统一是「若有则消耗N层[gold]压力[/gold]，<额外效果>」，
    //   实例：StrikeFast（伤害→额外1次伤害）、StrikeSlow（伤害→额外格挡）、
    //        Bansogo（能量→额外抽1）、StageConfidence（格挡→返回手牌）。
    //   这里只用「主效果 + 额外效果」两段式，保证卡面读起来是完整的一句话。
    public const string PayPressureTemplate = "csk:pay_pressure";
    public const string PayPressureOpcode = "csk_pay_pressure";
    public const string PayPressureDamageVariant = "damage";
    public const string PayPressureBlockVariant = "block";
    public const string PayPressureDrawVariant = "draw";
    /// <summary>消耗 1 层压力，全体敌人获得 N 层虚弱（本体 Symbol III）。</summary>
    public const string PayPressureWeakAllVariant = "weak_all";

    // ★ 食用芭菲 + 大效果（乐奈）：芭菲是**代价资源**，吃掉必须换来远超稀有度的收益
    //   （基准表 §1.2）。本体实例：
    //     NOBODY_KNOW_PARFAIT 食1杯 → 16 格挡（蓝卡普通档才 11）
    //     SOFT_BIG_BELLY     食1杯 → 30 格挡（金卡）
    //     HUMMING_PARFAIT    食1杯 → 14 全体伤害（蓝卡）
    //     PARFAIT_BIG_BANG   食2杯 → 34 全体伤害（金卡）
    public const string ParfaitComboTemplate = "csk:parfait_combo";
    public const string ParfaitComboOpcode = "csk_parfait_combo";
    public const string ParfaitComboBlockVariant = "eat_block";
    public const string ParfaitComboDamageAllVariant = "eat_damage_all";
    public const string ParfaitComboHealDrawVariant = "eat_heal_draw";
    public const string ParfaitComboDrawVariant = "eat_draw";

    // ★ 复合同子（T-003）：音符/和弦与 payoff 写在同一句，避免 AA 把纯 setup 折价换掉
    //   纯 setup 组件（「获得1个音符」）在 AA 眼里不产生收益，预算不够就被换掉，
    //   导致爱音机制浓度只有 6%（详 §3.34）。复合后 setup 与 payoff 绑在一起，不会被拆散。
    public const string NoteAndDamageVariant = "add_attack_damage";
    public const string NoteAndBlockVariant = "add_skill_block";
    /// <summary>获得音符并抽牌。</summary>
    public const string NoteAndDrawVariant = "add_attack_draw";
    /// <summary>获得音符并获得能量。</summary>
    public const string NoteAndEnergyVariant = "add_attack_energy";
    /// <summary>2026-11-11：获得音符并**立即触发**所有音符效果（本体「并触发音符对应效果」）。</summary>
    public const string NoteAndTriggerVariant = "note_and_trigger";
    public const string ChordAndDamageVariant = "play_stored_damage";
    public const string ChordAndBlockVariant = "play_stored_block";

    // ★ 储存 / 演奏最后一个（T-004）：本体 16 张「储存」+ 6 张「已记忆」，随机池原本一张都没有。
    public const string ChordRememberVariant = "remember";
    public const string ChordLastVariant = "play_last";
    /// <summary>获得随机和弦槽位；若已有则演奏（本体「团队的小太阳」等 6 张卡的机制）。</summary>
    public const string ChordGrantSlotVariant = "grant_slot";
    public const string ChordSlotTemplate = "csk:play_chord_slot";
    /// <summary>获得 N 个随机音符（本体「练习...练习...」）。</summary>
    public const string RandomNoteVariant = "random";
    // ⚠️ 独立模板：同一个 template 配不同中文文本会被 AA 的模板编译器认为结构冲突，
    //    结果是该 atom 静默落选（T-004 实测：remember/play_last 一次都没被抽到）。
    //    每个新机制开自己的 template 最稳。
    public const string ChordRememberTemplate = "csk:chord_remember";

    // ★ 骑士之剑扩展（T-013，2026-11-10）：本体「磨刀」是「升级所有剑 + 压力转化伤害」，
    //   随机池之前只有「获得 1 张剑」，没有这两个用法。
    public const string SwordUpgradeVariant = "sword_upgrade";

    // ══════════════════════════════════════════════════════════════
    // A 档缺口（T-014，2026-11-10）：数值随「压力」缩放
    // 依据《祥子卡池缺口表.md》：90 张祥子卡里 51 张涉及压力，
    // 但随机池此前只有「获得/消耗压力」，**没有「按压力算数值」**，
    // 所以应激反应 / 心之壁这类「压力联动」卡的核心玩法完全缺失。
    // ══════════════════════════════════════════════════════════════
    public const string ScaleTemplate = "csk:scale";
    public const string ScaleOpcode = "csk_scale";
    /// <summary>造成等同于自身压力层数 N 倍的伤害（至少保底）。</summary>
    public const string DamageByPressureVariant = "damage_by_pressure";
    /// <summary>获得等同于自身压力层数 N 倍的格挡（至少保底）。</summary>
    public const string BlockByPressureVariant = "block_by_pressure";
    /// <summary>使自身压力翻倍（并额外 N 层）。</summary>
    public const string DoublePressureVariant = "double";
    /// <summary>消耗全部压力，每 N 层换 1 点能量。</summary>
    public const string SpendAllPressureEnergyVariant = "spend_all_energy";
    /// <summary>消耗全部压力换能量（无槽版，1 层 = 1 能量，文案里不带数字，避免占位符露字）。</summary>
    public const string SpendAllPressureEnergyOneVariant = "spend_all_energy_one";
    /// <summary>造成等同于遗忘堆牌数 N 倍的伤害（本体「不再幼稚的我」）。</summary>
    public const string DamageByForgetPileVariant = "damage_by_forget_pile";
    /// <summary>造成等同于自身格挡值的伤害（本体「把衣服剪开」）。</summary>
    public const string DamageByBlockVariant = "damage_by_block";
    /// <summary>造成等同于目标压力层数 N 倍的伤害（本体「Symbol II」「生气了」）。</summary>
    public const string DamageByTargetPressureVariant = "damage_by_target_pressure";
    /// <summary>造成等同于「琴奏」牌数量 N 倍的伤害。</summary>
    public const string DamageByPianoVariant = "damage_by_piano";
    /// <summary>获得等同于「琴奏」牌数量 N 倍的格挡。</summary>
    public const string BlockByPianoVariant = "block_by_piano";
    /// <summary>造成 N 点伤害，每有 1 张「琴奏」牌额外 1 次（T-031）。</summary>
    public const string DamageHitsByPianoVariant = "damage_hits_by_piano";
    // ★ A10 金币（本体「客服工作」/「艰难抉择」）
    public const string GoldTemplate = "csk:gold";
    public const string GoldOpcode = "csk_gold";
    public const string GainGoldVariant = "gain";

    // ══════════════════════════════════════════════════════════════
    // 条件型原子（2026-11-10 用户要求「效果拆得够不够细」）
    //
    // 本体大量卡的写法是「先做 A；若 <条件> 则再做 B」，
    // 之前的随机池只有「A」，缺了后半截，所以随机卡都很平。
    // 这里给每种常见条件单开变体，条件判断写在 handler 里。
    // ══════════════════════════════════════════════════════════════    // 结束回合系列（大负面 + 收益），2026-11-10 用户要求。
    // 为什么不做成独立原子：AA 的 CardEffectRules 会拒掉「纯结束回合」的卡
    // （HasNoTurnEndTurnLocalPayoffs），必须自带收益才能成牌。
    public const string ExtremeTemplate = "csk:extreme";
    public const string ExtremeOpcode = "csk_extreme";
    /// <summary>结束你的回合，然后抽 N 张牌。</summary>
    public const string EndTurnDrawVariant = "end_turn_draw";
    /// <summary>结束你的回合，然后获得 N 点能量。</summary>
    public const string EndTurnEnergyVariant = "end_turn_energy";
    /// <summary>结束你的回合，然后对所有敌人造成 N 伤害。</summary>
    public const string EndTurnDamageVariant = "end_turn_damage";

    // 生成状态牌（C 项「生成状态牌进牌堆」，2026-11-10）
    public const string StatusTemplate = "csk:status";
    public const string StatusOpcode = "csk_status";
    /// <summary>改敌人意图（T-024）。</summary>
    public const string IntentOpcode = "csk_intent";
    /// <summary>能量（自建，文案用中文数字，避免 AA 生成 {Energy0:energyIcons()} 后无人替换）。</summary>
    public const string EnergyOpcode = "csk_energy";
    public const string EnergyGainTwoVariant = "gain_two";
    public const string EnergyNextTurnTwoVariant = "next_turn_two";
    /// <summary>让所有敌人重新随机下回合意图。</summary>
    public const string RerollIntentVariant = "reroll_all";
    /// <summary>把 N 张「欺诈」加入弃牌堆。</summary>
    public const string FraudStatusVariant = "fraud";
    /// <summary>把 N 张「偷懒」加入弃牌堆。</summary>
    public const string ShirkStatusVariant = "shirk";

    // 复合原子：本场成长（2026-11-10）
    // 为什么自己做：AA 的 I:IncreaseDamageThisCombat 是「挂靠型」修饰符，
    // 单独进池会被整条丢掉（§3.77）。这里自己维护计数器，不依赖它的组装器。
    public const string GrowthTemplate = "csk:growth";
    public const string GrowthOpcode = "csk_growth";
    /// <summary>造成 N 点伤害；这张牌本场战斗中每打出一次，伤害再提高 M 点。</summary>
    public const string DamageGrowthVariant = "damage_growth";
    /// <summary>造成 N 伤害；每打出过一次，额外获得 M 点格挡。</summary>
    public const string DamageGrowthBlockVariant = "damage_growth_block";
    /// <summary>造成 N 伤害；每打出过一次，额外将 M 张骑士之剑加入手牌。</summary>
    public const string DamageGrowthSwordVariant = "damage_growth_sword";
    /// <summary>造成 N 伤害；每打出过一次，额外获得 M 张回忆。</summary>
    public const string DamageGrowthMemoryVariant = "damage_growth_memory";
    /// <summary>造成 N 伤害；每打出过一次，额外抽 M 张牌。</summary>
    public const string DamageGrowthDrawVariant = "damage_growth_draw";
    /// <summary>获得 N 点格挡；每打出过一次，额外获得 M 点格挡。</summary>
    public const string BlockGrowthBlockVariant = "block_growth_block";
    /// <summary>获得 N 格挡；每打出过 1 次额外获得 M 张回忆。</summary>
    public const string BlockGrowthMemoryVariant = "block_growth_memory";
    /// <summary>获得 N 格挡；每打出过 1 次额外获得 M 张骑士之剑。</summary>
    public const string BlockGrowthSwordVariant = "block_growth_sword";
    /// <summary>获得 N 格挡；每打出过 1 次额外获得 M 杯抹茶芭菲（乐奈专属）。</summary>
    public const string BlockGrowthParfaitVariant = "block_growth_parfait";
    /// <summary>获得格挡，每存储一张和弦额外获得（T-020，爱音）。</summary>
    public const string BlockGrowthChordVariant = "block_growth_chord";

    // 改费用 / 变牌（C 项，2026-11-10，复合原子路线 §3.78）
    public const string CostTemplate = "csk:cost";
    public const string CostOpcode = "csk_cost";
    /// <summary>使手牌中 N 张牌本回合费用变为 0。</summary>
    public const string CostFreeHandVariant = "free_hand";
    /// <summary>抽 1 张牌，并使手牌中随机 N 张牌本回合费用变为 0（寄生版，2026-11-10）。</summary>
    public const string CostFreeHandDrawVariant = "free_hand_draw";
    /// <summary>随机将手牌中 N 张牌变形（变牌）。</summary>
    public const string CostTransformHandVariant = "transform_hand";

    public const string ConditionTemplate = "csk:condition";
    public const string ConditionOpcode = "csk_condition";
    /// <summary>完全格挡条件的独立 opcode：绕过 AA 对 csk_condition 未知 variant 的组装过滤。</summary>
    public const string ConditionBlockedOpcode = "csk_condition_blocked";
    /// <summary>压力翻倍；若因此触发崩溃则抽 N 张并获得 1 能量（本体「如梦一场」）。</summary>
    public const string DoubleCollapseBonusVariant = "double_collapse_bonus";
    /// <summary>压力翻倍；若因此触发崩溃则对全体敌人造成 N 伤害。</summary>
    public const string DoubleCollapseDamageVariant = "double_collapse_damage";
    /// <summary>压力翻倍；若因此触发崩溃则抽 N 张牌。</summary>
    public const string DoubleCollapseDrawVariant = "double_collapse_draw";
    /// <summary>压力翻倍；若因此触发崩溃则获得 N 层随机能力。</summary>
    public const string DoubleCollapsePowerVariant = "double_collapse_power";
    /// <summary>压力翻倍；若触发崩溃则获得 N 张骑士之剑（崩溃后压力已清零，所以只能给固定值）。</summary>
    public const string DoubleCollapseSwordVariant = "double_collapse_sword";
    /// <summary>压力翻倍；若触发崩溃则骑士之剑获得 N 点额外伤害。</summary>
    public const string DoubleCollapseSwordBoostVariant = "double_collapse_sword_boost";
    /// <summary>压力翻倍；若触发崩溃则获得 N 张回忆。</summary>
    public const string DoubleCollapseMemoryVariant = "double_collapse_memory";
    /// <summary>造成伤害；若击杀则获得 N 张回忆。</summary>
    public const string DamageMemoryOnKillVariant = "damage_memory_on_kill";
    /// <summary>造成伤害；若击杀则获得 N 张骑士之剑。</summary>
    public const string DamageSwordOnKillVariant = "damage_sword_on_kill";
    /// <summary>造成伤害；若未被完全格挡则施加 N 层易伤。</summary>
    public const string DamageVulnerableIfUnblockedVariant = "damage_vulnerable_if_unblocked";
    /// <summary>造成 N 伤害；若伤害被完全格挡则给予 M 层易伤（对称变体）。</summary>
    public const string DamageVulnerableIfBlockedVariant = "damage_vulnerable_if_blocked";
    /// <summary>造成 N 伤害；若击杀则获得 M 能量（本体「为什么不帮我」）。</summary>
    public const string DamageEnergyOnKillVariant = "damage_energy_on_kill";
    /// <summary>造成 N 伤害；若目标压力高于自身则给予 M 层缩小（本体「真是高高在上」）。</summary>
    public const string DamageShrinkIfLessPressureVariant = "damage_shrink_if_less_pressure";
    /// <summary>造成 N 伤害；若目标压力小于自身则给予 M 层缩小（对称变体）。</summary>
    public const string DamageShrinkIfMorePressureVariant = "damage_shrink_if_more_pressure";
    /// <summary>对所有敌人造成 N 伤害；若自身无压力则改为回复 M 生命（本体「黑白分明」）。</summary>
    public const string DamageAllOrHealVariant = "damage_all_or_heal";
    /// <summary>造成伤害；若击杀则演奏所有已记忆和弦（本体「全都会了！」）。</summary>
    public const string DamagePlayMemorizedOnKillVariant = "damage_play_memorized_on_kill";
    /// <summary>造成伤害；若未被完全格挡则抽 N 张（本体「试音打击」）。</summary>
    public const string DamageDrawIfUnblockedVariant = "damage_draw_if_unblocked";
    /// <summary>造成 N 伤害；若伤害被完全格挡则抽 M 张（对称变体）。</summary>
    public const string DamageDrawIfBlockedVariant = "damage_draw_if_blocked";
    /// <summary>金币 ＝ 存活生物数 × N（本体「T~G~W~集团」）。</summary>
    public const string GainGoldByCreaturesVariant = "gain_by_creatures";
    // ★ 爱A4 丢弃全部手牌（本体「意外事件」）
    public const string DeckTemplate = "csk:deck";
    public const string DeckOpcode = "csk_deck";
    public const string DiscardAllVariant = "discard_all";
    /// <summary>消耗手牌中所有状态牌，每张抽 N（本能「讨厌的东西说拜拜」/「冰释前嫌」）。</summary>
    public const string ExhaustStatusVariant = "exhaust_status";
    /// <summary>把 1 张本牌的复制加入弃牌堆（本体「小小的瞬间」/「猫想要猫得到」）。</summary>
    public const string CopySelfVariant = "copy_self";
    /// <summary>消耗 N 层压力，将该牌返回手牌（本体「泡澡」/「舞台上的自信」）。</summary>
    public const string ReturnToHandVariant = "return_to_hand";
    /// <summary>随机打出 N 张手牌（本体「提线木偶」）。</summary>
    public const string AutoPlayHandVariant = "autoplay_hand";
    /// <summary>遗忘所有手牌，每遗忘 1 张获得 1 张回忆（本体「忘却一切」）。</summary>
    public const string ForgetAllHandVariant = "forget_all_hand";
    /// <summary>从抽牌堆随机打出 N 张猫咪（本体「猫咪打击」「饿猫扑食」）。</summary>
    public const string PlayNekoFromDrawVariant = "play_neko_draw";
    /// <summary>从任何地方打出所有猫咪（本体「小猫出动」）。</summary>
    public const string PlayNekoAnywhereVariant = "play_neko_anywhere";
    /// <summary>减少 N 层压力并抽 M 张（本体「积郁成泣」）。</summary>
    public const string RelievePressureDrawVariant = "relieve_draw";
    /// <summary>随机清除身上一个负面效果（本体「泡澡」）。</summary>
    public const string ClearDebuffVariant = "clear_debuff";
    /// <summary>用回忆填满手牌（本体「被回忆攻击」）。</summary>
    public const string FillHandRecallVariant = "fill_hand_recall";
    /// <summary>从抽牌堆随机打出 N 张牌并遗忘（本体「唯有忘却」）。</summary>
    public const string PlayAndForgetVariant = "play_and_forget";
    /// <summary>丢弃手牌中所有非芭菲相关牌，每张抽 1（本体「挑食」）。</summary>
    public const string DiscardNonParfaitVariant = "discard_non_parfait";
    /// <summary>消耗 N 层压力，永久获得 M 点最大生命（本体「阅读」）。</summary>
    public const string PressureMaxHpVariant = "pressure_max_hp";
    /// <summary>从遗忘堆取 N 张牌加入手牌，本回合免费（本体「重拾回忆」）。</summary>
    public const string RecallFromForgetVariant = "recall_from_forget";
    /// <summary>将抽牌堆 N 张牌直接放入手牌（本体「可以做任何事」）。</summary>
    public const string DrawToHandVariant = "draw_to_hand";
    /// <summary>从弃牌堆遗忘 N 张（本体「我自己的问题」）。</summary>
    public const string ForgetFromDiscardVariant = "forget_from_discard";
    /// <summary>随机打出手牌 N 张并遗忘（本体「暂且遗忘」）。</summary>
    public const string PlayHandThenForgetVariant = "play_hand_then_forget";
    /// <summary>从消耗堆取 N 张牌加入手牌（本体「沉思」）。</summary>
    public const string RecallFromExhaustVariant = "recall_from_exhaust";
    /// <summary>消耗 1 层压力，获得 1 张回忆（本体「再度鼓励」）。</summary>
    public const string PayPressureRecallVariant = "recall";
    /// <summary>猫咪加入手牌并获得保留（本体「和猫发呆」）。</summary>
    public const string TokenNekoRetainVariant = "neko_retain";
    /// <summary>打出手牌中所有骑士之剑（本体「由我君临」）。</summary>
    public const string TokenPlayAllSwordsVariant = "play_all_swords";
    /// <summary>从抽牌堆打出 N 张琴奏牌（本体「琴奏：连奏」）。</summary>
    public const string TokenPlayPianoVariant = "play_piano_draw";
    /// <summary>打出并消耗所有牌堆中的琴奏牌。</summary>
    public const string TokenPlayAllPianoVariant = "play_all_piano";
    /// <summary>升级所有牌堆中的琴奏牌（T-033）。</summary>
    public const string TokenUpgradeAllPianoVariant = "upgrade_all_piano";
    /// <summary>打出并消耗遗忘堆中所有回忆（本体「燃烧回忆」）。</summary>
    public const string TokenPlayForgetMemoryVariant = "play_forget_memory";
    /// <summary>消耗 1 张手牌，视为食用 1 杯抹茶芭菲（本体「猫饿了」）。</summary>
    public const string ConsumeAsParfaitVariant = "consume_as_parfait";
    public const string SwordBoostVariant = "sword_boost";
    /// <summary>无论何处把骑士之剑放入手牌（本体「骑士出鞘」）。</summary>
    public const string SwordFromAnywhereVariant = "sword_from_anywhere";
    /// <summary>打出手牌所有剑，再消耗全部压力追加等量伤害（本体「以压力化利刃」）。</summary>
    public const string SwordPressureDamageVariant = "sword_pressure_damage";
    public const string ChordLastTemplate = "csk:chord_last";

    /// <summary>所有由本模组拥有的 opcode/variant 对，用于启动自检与路由注册。</summary>
    public static readonly (string Opcode, string Variant)[] OwnedRoutes =
    [
        (PressureOpcode, PressureVariant),
        (PressureOpcode, PressureAllVariant),
        (ForgetOpcode, ForgetVariant),
        (ForgetOpcode, ForgetRandomVariant),
        (RecallOpcode, RecallVariant),
        (PlayChordOpcode, PlayChordStoredVariant),
        (PlayChordOpcode, PlayChordRandomVariant),
        (NoteOpcode, NoteAddAttackVariant),
        (NoteOpcode, NoteAddSkillVariant),
        (NoteOpcode, NoteAddPowerVariant),
        (NoteOpcode, NoteAddSpecialVariant),
        (NoteOpcode, NoteClearVariant),
        (ParfaitOpcode, ParfaitEatVariant),
        (ParfaitOpcode, ParfaitChargeVariant),
    ];

    /// <summary>构造一个结构化 atom：自动编译 RuntimeSpec、自动带本地化。</summary>
    /// <summary>
    /// 2026-11-11 用户第 1 条：把染色规则放在**原子文本**这一层，
    /// 这样 AA / ChaosCardModel 动态渲染 operations 时也能拿到颜色，
    /// 而不是只改 Generated.ChineseDescription（动态卡面不走那份文本）。
    /// </summary>
    internal static string NormalizeTextStyle(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var styled = Regex.Replace(text, @"\[blue\]([^\[\]]*\d+[^\[\]]*)\[/blue\]", "$1");
        styled = Regex.Replace(styled,
            @"(?<!\[gold\])(手牌|弃牌堆|抽牌堆|消耗堆|遗忘堆)(?!\[/gold\])", "[gold]$1[/gold]");
        styled = Regex.Replace(styled,
            @"(?<!\[red\])遗忘(?!堆)(?!\[/red\])", "[red]遗忘[/red]");
        // ★ 2026-11-12 用户口径：治疗不再用绿色（会和升级绿撞色），改成「回复」金色。
        //   先把旧的 [green]回复...[/green] 去掉，再只给「回复」两个字上 gold。
        styled = Regex.Replace(styled,
            @"\[green\](回复[^\[\]]*点生命)\[/green\]", "$1");
        styled = Regex.Replace(styled,
            @"(?<!\[gold\])回复(?=\s*(?:\{[^}]+\}|\d+)点生命)", "[gold]回复[/gold]");
        return styled;
    }

    public static ComponentAtom Atom(string template, OperationScope scope, string chinese, string english,
        bool requiresSingleTarget = false, string? semanticId = null,
        CardReferenceRequirement cardReference = CardReferenceRequirement.None,
        ComponentCategory category = ComponentCategory.Automatic)
    {
        chinese = NormalizeTextStyle(chinese);
        var operation = new GeneratorOperation(template, scope, chinese, new Dictionary<string, int>(),
            RequiresSingleTarget: requiresSingleTarget);
        var spec = CompileSpec(operation);
        var (alignedChinese, localized) = AlignLocalized(chinese, english, spec);
        RegisterEnglish(template, alignedChinese, english);
        return new ComponentAtom(template, scope, alignedChinese, requiresSingleTarget, cardReference)
        {
            SemanticId = semanticId,
            RuntimeSpec = spec,
            LocalizedText = localized,
            Category = category
        };
    }

    /// <summary>
    /// 注册「中文原文 → 英文」的映射表。
    ///
    /// 为什么必须做：AA 在数值缩放时会用 <c>TryReplaceFixedValue</c> 重写中文句子，
    /// 然后调用 <c>ExternalOperationTextRegistry.RegisterNumericVariant</c> 反查英文；
    /// 如果该中文句子没有注册过，它会走 <c>EnglishCardDescriptionRenderer.ToEnglish</c>
    /// 的内置正则表，而自定义机制（如「获得5层压力」）不在表里 → 直接抛「缺少英文卡面映射」。
    /// </summary>
    private static void RegisterEnglish(string template, string chinese, string english)
    {
        try
        {
            ExternalOperationTextRegistry.Register(template, chinese, english);
        }
        catch (Exception)
        {
            // 重复注册同一条不算错误，忽略即可。
        }
    }

    /// <summary>
    /// 编译 RuntimeSpec。用 AA 自己的编译器，保证 opcode/variant/数值槽与内部约定一致；
    /// 手写 OperationRuntimeSpec 很容易和校验规则冲突。
    /// </summary>
    public static OperationRuntimeSpec CompileSpec(GeneratorOperation operation)
    {
        var spec = OperationRuntimeSpecCompiler.CompileRequired(operation);
        spec.Validate();
        return spec;
    }

    /// <summary>
    /// 构造一个「自定义 opcode」的 atom：RuntimeSpec 手写（编译器不认识 csk_* opcode），
    /// 但本地化对齐与英文注册仍走统一流程。
    /// </summary>
    public static ComponentAtom AtomWithSpec(string template, OperationScope scope, string chinese,
        string english, OperationRuntimeSpec spec, bool requiresSingleTarget = false,
        CardReferenceRequirement cardReference = CardReferenceRequirement.None,
        ComponentCategory category = ComponentCategory.Automatic)
    {
        chinese = NormalizeTextStyle(chinese);
        // ★★ 2026-11-10 对照 AA wiki：跨组件语义必须用 ComponentSemanticFlags 显式声明，
        //   AA **不会**从 Template 名或文案去猜。我们之前 116 处全是空 flags → AA 组卡时
        //   无法判断正面/负面/伤害/能力构成/可缩放 → 表现成"纯原版卡多"、
        //   生成器兜底失败（Could not produce a unique emergency fallback）等问题。
        //   这里按 ComponentCategory 统一补默认语义，原子自身传了 flags 则以传入为准。
        spec = MergeSemanticFlags(spec, category);
        spec.Validate();
        var (alignedChinese, localized) = AlignLocalized(chinese, english, spec);
        RegisterEnglish(template, alignedChinese, english);
        return new ComponentAtom(template, scope, alignedChinese, requiresSingleTarget, cardReference)
        {
            RuntimeSpec = spec,
            LocalizedText = localized,
            Category = category
        };
    }

    /// <summary>按组件类别补上 AA 要求的跨组件语义 flag（已声明的不动）。</summary>
    internal static OperationRuntimeSpec MergeSemanticFlags(OperationRuntimeSpec spec, ComponentCategory category)
    {
        var flags = new HashSet<string>(spec.Flags, StringComparer.Ordinal);
        void Add(string flag) => flags.Add(flag);

        switch (category)
        {
            case ComponentCategory.Damage:
                Add(ComponentSemanticFlags.EnemyDamage);
                Add(ComponentSemanticFlags.Beneficial);
                break;
            case ComponentCategory.Defense:
            case ComponentCategory.Resource:
                Add(ComponentSemanticFlags.Beneficial);
                break;
            // ★ Negative 语义会让 AA 要求该原子**显式**声明负价值补偿方式
            //   （NegativeLinearValuePerUnit 或 NegativeMultiplier），
            //   我们很多负面原子没有对应估值注册 → 直接抛
            //   "Multiplier-style downside 'csk:pressure' has no explicit multiplier"（实测）。
            //   在补齐全部负面原子的估值之前，先不自动打 Negative。            case ComponentCategory.Utility:
            case ComponentCategory.CardManipulation:
                Add(ComponentSemanticFlags.Beneficial);
                break;
        }

        // ★ 2026-11-11 分析结论（先保留现状，等用户拍板再改）：
        //   ScalableReward 目前按「Resource/Defense 类别 + 有数值槽」一刀切，
        //   会把芭菲杯数、猫咪张数、音符个数、金币、能力层数、倍率全部标成可缩放，
        //   AA 的 RaiseNumericRewardsToTopRarityFloor 就能把它们抬到 11/22/34。
        //   离线实验（.build-check/atomvp/GenProbe.cs vp）显示：只把 flag 改成
        //   「只给 damage/block/draw/energy/heal」并不能消掉大数字（AA 会改抬唯一可缩放的伤害），
        //   而且会让芭菲/猫咪类原子在样本里消失。
        //   ⇒ 真正要改的是「估值按每单位 × 预算带收缩 + 组卡补第二个 payoff」，
        //     单改这一处不够，所以先不动生产逻辑。详见 任务计划\原子VP对照表.md §4。
        if (category is ComponentCategory.Resource or ComponentCategory.Defense
            && spec.Values.Count > 0
            && spec.Opcode != SakiComponents.ParfaitComboOpcode)
            Add(ComponentSemanticFlags.ScalableReward);

        return flags.SetEquals(spec.Flags)
            ? spec
            : spec with { Flags = flags.Order(StringComparer.Ordinal).ToArray() };
    }

    /// <summary>造一个「单数值槽」的自定义 RuntimeSpec。</summary>
    public static OperationRuntimeSpec NumericSpec(string opcode, string variant, string target,
        string slotId, int value, IReadOnlyList<string> flags) => new(
        OperationRuntimeSpec.CurrentSchemaVersion,
        opcode, variant, target,
        SourceZone: "none", DestinationZone: "none", CardFilter: "any",
        Flags: flags.Order(StringComparer.Ordinal).ToArray(),
        Values: [new RuntimeValueSlot(slotId, value)],
        Condition: null, Trigger: null);

    /// <summary>
    /// 阿拉伯数字 → 中文数字（1~10）。自定义原子的文案里**不能出现阿拉伯数字**，
    /// 否则 AA 会把它编译成 {AmountN:diff()} 占位符而卡面没人替换（§3.92，2026-11-10 实机）。
    /// </summary>
    internal static string CnNumber(int value) => value switch
    {
        1 => "一", 2 => "二", 3 => "三", 4 => "四", 5 => "五",
        6 => "六", 7 => "七", 8 => "八", 9 => "九", 10 => "十",
        _ => value.ToString(System.Globalization.CultureInfo.InvariantCulture)
    };

    /// <summary>
    /// 把中英文本编译成具名模板。AA 要求「中文模板渲染结果必须逐字等于 atom.ChineseText」，
    /// 否则包注册直接抛异常——所以这里编译成功后**用渲染结果反写 ChineseText**，
    /// 保证二者永远一致（这是唯一稳妥的做法：手写两遍一定会漂）。
    /// </summary>
    internal static (string Chinese, OperationLocalizedText Localized) AlignLocalized(
        string chinese, string english, OperationRuntimeSpec spec)
    {
        if (OperationLocalizedText.TryCompile(chinese, english, spec, out var localized) && localized is not null)
        {
            try
            {
                localized.Validate(spec);
                // 用模板反渲染出的中文作为权威 ChineseText，二者必然逐字相等。
                return (localized.RenderChinese(spec), localized);
            }
            catch (InvalidOperationException)
            {
                // 落回下面的无槽模板
            }
        }

        return (chinese, new OperationLocalizedText(chinese, english));
    }

    /// <summary>
    /// 无槽 RuntimeSpec：效果没有数值（如「升级所有骑士之剑」）。
    /// ⚠️ 文本里没有 `{count}` 占位符时**不能**给 Values，
    /// 否则组件校验会抛
    /// `Chinese operation template does not cover every printed slot`（实测 2026-11-10）。
    /// </summary>
    public static OperationRuntimeSpec NoValueSpec(string opcode, string variant) => new(
        OperationRuntimeSpec.CurrentSchemaVersion,
        opcode, variant, "self",
        SourceZone: "none", DestinationZone: "none", CardFilter: "any",
        Flags: [], Values: [], Condition: null, Trigger: null);

    /// <summary>
    /// 触发器用的空槽 RuntimeSpec（T-015）。
    /// 触发器没有数值，opcode/variant 与 AA 原生触发操作一致（trigger / event）。
    /// </summary>
    public static OperationRuntimeSpec TriggerSpec() => new(
        OperationRuntimeSpec.CurrentSchemaVersion,
        "trigger", "event", "self",
        SourceZone: "none", DestinationZone: "none", CardFilter: "any",
        Flags: [], Values: [], Condition: null, Trigger: null);

    /// <summary>
    /// 造一个「双数值槽」的自定义 RuntimeSpec（主效果 + 额外效果）。
    /// 用于「消耗压力换额外效果」这类两段式机制。
    /// </summary>
    public static OperationRuntimeSpec TwoSlotSpec(string opcode, string variant, string target,
        string firstSlotId, int firstValue, string secondSlotId, int secondValue,
        IReadOnlyList<string> flags) => new(
        OperationRuntimeSpec.CurrentSchemaVersion,
        opcode, variant, target,
        SourceZone: "none", DestinationZone: "none", CardFilter: "any",
        Flags: flags.Order(StringComparer.Ordinal).ToArray(),
        Values: [new RuntimeValueSlot(firstSlotId, firstValue), new RuntimeValueSlot(secondSlotId, secondValue)],
        Condition: null, Trigger: null);}































































