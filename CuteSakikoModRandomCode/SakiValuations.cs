using ChaosCardGenerator;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 自定义 opcode 的估值（AA 单位：普通单体伤害一点的百分之一，100 = 1 点伤害）。
///
/// ★★ 2026-11-11 回滚说明（A 方案实验失败，已恢复到此版本）：
///   曾尝试把 158 个变体改成「按本体 VP 反算的每单位估值」，
///   离线/实机都证明**只做估值不够**：真实 VP 让 AA 的 RaiseNumericRewardsToTopRarityFloor
///   把计数器抬到 10 次演奏 / 10 个音符 / 8 层能力 / 55 格挡（见 AI工作日志 第 174 轮）。
///   ⇒ 在 `ScalableReward` 收紧 + 组卡补第二个 payoff + 预算带收缩三件一起做之前，
///     保留这张「按变体手写」的估值表（它同时承担了「让计数不爆炸」的职责）。
///   完整的 VP 对照与后续方案见 `任务计划\原子VP对照表.md`。
/// </summary>
internal static class SakiValuations
{
    /// <summary>1 杯抹茶芭菲 = 9 VP = 1800 单位（基准表 §1）。</summary>
    private const double ParfaitUnit = 1800d;

    /// <summary>1 层压力 = 0.75 VP = 150 单位（基准表 §1.3）。</summary>
    private const double PressureUnit = 150d;

    /// <summary>所有自定义 opcode 的估值路由（按 opcode+variant；变体精确优先于通配）。</summary>
    internal static IReadOnlyList<ComponentValuationRegistration> All() => _cached ??= Raw();

    private static IReadOnlyList<ComponentValuationRegistration>? _cached;

    /// <summary>
    /// 按我们注册给 AA 的估值表，给一个 RuntimeSpec 估值（AA 单位：100 = 1 点普通单体伤害）。
    /// 用于把「永久 power 在非 Power 卡上 ×1.5」同步进 AA 的整卡预算模型。
    /// </summary>
    internal static int EstimateFor(OperationRuntimeSpec spec)
    {
        var registrations = All();
        var registration = registrations.FirstOrDefault(route =>
                               route.Opcode == spec.Opcode && route.Variant == spec.Variant)
                           ?? registrations.FirstOrDefault(route =>
                               route.Opcode == spec.Opcode && route.Variant.Length == 0);
        if (registration is null) return 0;
        try
        {
            return registration.Valuation.Estimate(
                new ComponentValuationContext(string.Empty, OperationScope.NonTargeted, spec));
        }
        catch
        {
            return 0;
        }
    }

    private static IReadOnlyList<ComponentValuationRegistration> Raw() =>
    [
        new("csk_condition", "damage_all_or_heal", new LinearValuation(760)),
        new("csk_condition", "damage_draw_if_unblocked", new LinearValuation(620)),
        new("csk_condition", "damage_memory_on_kill", new LinearValuation(620)),
        new("csk_condition", "damage_play_memorized_on_kill", new LinearValuation(980)),
        new("csk_condition", "damage_shrink_if_less_pressure", new LinearValuation(820)),
        new("csk_condition", "damage_vulnerable_if_unblocked", new LinearValuation(600)),
        // ★ 2026-11-12：压力比较 × 多收益
        new("csk_condition", "pressure_more_shrink", new LinearValuation(820)),
        new("csk_condition", "pressure_less_shrink", new LinearValuation(820)),
        new("csk_condition", "pressure_more_vulnerable", new LinearValuation(600)),
        new("csk_condition", "pressure_less_vulnerable", new LinearValuation(600)),
        new("csk_condition", "pressure_more_pressure", new LinearValuation(620)),
        new("csk_condition", "pressure_less_pressure", new LinearValuation(620)),
        new("csk_condition", "pressure_more_doublepressure", new LinearValuation(760)),
        new("csk_condition", "pressure_less_doublepressure", new LinearValuation(760)),
        new("csk_condition", "pressure_more_bonusdamage", new LinearValuation(900)),
        new("csk_condition", "pressure_less_bonusdamage", new LinearValuation(900)),
        new("csk_condition", "pressure_more_block", new LinearValuation(600)),
        new("csk_condition", "pressure_less_block", new LinearValuation(600)),
        new("csk_condition", "pressure_more_draw", new LinearValuation(620)),
        new("csk_condition", "pressure_less_draw", new LinearValuation(620)),
        new("csk_condition", "pressure_more_energy", new LinearValuation(700)),
        new("csk_condition", "pressure_less_energy", new LinearValuation(700)),
        // ★ 2026-11-12：其他条件 × 收益
        new("csk_condition", "unblocked_block", new LinearValuation(600)),
        new("csk_condition", "unblocked_draw", new LinearValuation(620)),
        new("csk_condition", "unblocked_energy", new LinearValuation(700)),
        new("csk_condition", "unblocked_bonusdamage", new LinearValuation(900)),
        new("csk_condition", "kill_block", new LinearValuation(600)),
        new("csk_condition", "kill_draw", new LinearValuation(620)),
        new("csk_condition", "kill_energy", new LinearValuation(700)),
        new("csk_condition", "kill_bonusdamage", new LinearValuation(900)),
        new("csk_condition", "notkill_block", new LinearValuation(600)),
        new("csk_condition", "notkill_draw", new LinearValuation(620)),
        new("csk_condition", "notkill_energy", new LinearValuation(700)),
        new("csk_condition", "notkill_bonusdamage", new LinearValuation(900)),
        new("csk_condition_blocked", "blocked_block", new LinearValuation(600)),
        new("csk_condition_blocked", "blocked_draw", new LinearValuation(620)),
        new("csk_condition_blocked", "blocked_energy", new LinearValuation(700)),
        new("csk_condition_blocked", "blocked_bonusdamage", new LinearValuation(900)),
        new("csk_condition", "double_collapse_bonus", new LinearValuation(1050)),
        new("csk_condition", "double_collapse_damage", new LinearValuation(700)),
        new("csk_condition", "double_collapse_draw", new LinearValuation(720)),
        new("csk_condition", "double_collapse_memory", new LinearValuation(1400)),
        new("csk_condition", "double_collapse_power", new LinearValuation(760)),
        new("csk_cost", "free_hand", new LinearValuation(1100)),
        new("csk_cost", "transform_hand", new LinearValuation(900)),
        new("csk_deck", "autoplay_hand", new LinearValuation(650)),
        new("csk_deck", "clear_debuff", new LinearValuation(880)),
        new("csk_deck", "copy_self", new LinearValuation(1500)),
        new("csk_deck", "discard_all", new LinearValuation(100)),
        new("csk_deck", "discard_non_parfait", new LinearValuation(780)),
        new("csk_deck", "draw_to_hand", new LinearValuation(400)),
        new("csk_deck", "exhaust_status", new LinearValuation(700)),
        new("csk_deck", "fill_hand_recall", new LinearValuation(900)),
        new("csk_deck", "forget_all_hand", new LinearValuation(700)),
        new("csk_deck", "forget_from_discard", new LinearValuation(360)),
        new("csk_deck", "play_and_forget", new LinearValuation(820)),
        new("csk_deck", "play_hand_then_forget", new LinearValuation(380)),
        new("csk_deck", "pressure_max_hp", new LinearValuation(320)),
        new("csk_deck", "recall_from_exhaust", new LinearValuation(420)),
        new("csk_deck", "recall_from_forget", new LinearValuation(420)),
        new("csk_deck", "relieve_draw", new LinearValuation(950)),
        new("csk_deck", "return_to_hand", new LinearValuation(600)),
        new("csk_energy", "gain_two", new LinearValuation(600)),
        new(SakiComponents.ExtremeOpcode, "end_turn_damage", new LinearValuation(100), NegativeMultiplier: 2),
        new(SakiComponents.ExtremeOpcode, "end_turn_draw", new LinearValuation(120), NegativeMultiplier: 2),
        new("csk_forget", "random", new LinearValuation(300)),
        new("csk_forget", "selected", new LinearValuation(360)),
        new("csk_gold", "gain", new LinearValuation(300)),
        new("csk_gold", "gain_by_creatures", new LinearValuation(300)),
        new("csk_growth", "block_growth_block", new LinearValuation(560)),
        new("csk_growth", "block_growth_memory", new LinearValuation(700)),
        new("csk_growth", "block_growth_parfait", new LinearValuation(700)),
        new("csk_growth", "damage_growth", new LinearValuation(700)),
        new("csk_growth", "damage_growth_block", new LinearValuation(1050)),
        new("csk_growth", "damage_growth_draw", new LinearValuation(1050)),
        new("csk_growth", "damage_growth_memory", new LinearValuation(1050)),
        new("csk_intent", "reroll_all", new LinearValuation(430)),
        new("csk_note", "add_attack", new LinearValuation(130)),
        new("csk_note", "add_attack_damage", new LinearValuation(400)),
        new("csk_note", "add_attack_draw", new LinearValuation(500)),
        new("csk_note", "add_attack_energy", new LinearValuation(600)),
        new("csk_note", "add_power", new LinearValuation(150)),
        new("csk_note", "add_skill", new LinearValuation(130)),
        new("csk_note", "add_skill_block", new LinearValuation(500)),
        new("csk_note", "add_special", new LinearValuation(380)),
        new("csk_note", "clear", new LinearValuation(400)),
        new("csk_note", "clear_scale_block", new LinearValuation(100)),
        new("csk_note", "clear_scale_draw", new LinearValuation(260)),
        new("csk_note", "random", new LinearValuation(450)),
        new("csk_parfait_combo", "eat_block", new LinearValuation(1500)),
        new("csk_parfait_combo", "eat_damage_all", new LinearValuation(1200)),
        new("csk_parfait_combo", "eat_heal_draw", new LinearValuation(900)),
        new("csk_parfait_combo", "eat_draw", new LinearValuation(500)),
        new("csk_parfait", "buy", new LinearValuation(520)),
        new("csk_parfait", "charge", new LinearValuation(1800)),
        new("csk_parfait", "consume_as_parfait", new LinearValuation(900)),
        new("csk_pay_pressure", "block", new LinearValuation(760)),
        new("csk_pay_pressure", "damage", new LinearValuation(700)),
        new("csk_pay_pressure", "draw", new LinearValuation(150)),
        new("csk_pay_pressure", "recall", new LinearValuation(420)),
        new("csk_pay_pressure", "weak_all", new LinearValuation(360)),
        new("csk_play_chord", "play_last", new LinearValuation(1100)),
        new("csk_play_chord", "play_stored_block", new LinearValuation(1500)),
        new("csk_play_chord", "play_stored_damage", new LinearValuation(1400)),
        new("csk_play_chord", "random_memorized", new LinearValuation(1100)),
        new("csk_play_chord", "remember", new LinearValuation(1300)),
        new("csk_play_chord", "stored", new LinearValuation(1200)),
        new("csk_play_chord", "stored_multi", new LinearValuation(760)),
        new("csk_power", "artifact", new LinearValuation(900)),
        new("csk_power", "be_god", new LinearValuation(900)),
        new("csk_power", "black_rebirth", new LinearValuation(900)),
        new("csk_power", "bow_to_me", new LinearValuation(900)),
        new("csk_power", "calmness_lady", new LinearValuation(900)),
        new("csk_power", "cats_more_cats", new LinearValuation(900)),
        new("csk_power", "chord_bonus", new LinearValuation(900)),
        new("csk_power", "daggers_drawn", new LinearValuation(900)),
        new("csk_power", "dont_associate", new LinearValuation(900)),
        new("csk_power", "encore_next", new LinearValuation(900)),
        new("csk_power", "escapism", new LinearValuation(900)),
        new("csk_power", "ether", new LinearValuation(900)),
        new("csk_power", "eve_live", new LinearValuation(900)),
        new("csk_power", "finally_here", new LinearValuation(900)),
        new("csk_power", "full_assault", new LinearValuation(900)),
        new("csk_power", "guitar_vocal", new LinearValuation(900)),
        new("csk_power", "half_body", new LinearValuation(900)),
        new("csk_power", "intangible", new LinearValuation(9000)),
        new("csk_power", "lingering_taste", new LinearValuation(900)),
        new("csk_power", "live_sweet", new LinearValuation(900)),
        new("csk_power", "marks_of_practice", new LinearValuation(900)),
        new("csk_power", "masquerade", new LinearValuation(900)),
        new("csk_power", "meet_performance", new LinearValuation(900)),
        new("csk_power", "memory_coming", new LinearValuation(900)),
        new("csk_power", "messy_play", new LinearValuation(900)),
        new("csk_power", "musica", new LinearValuation(900)),
        new("csk_power", "nai_vitality", new LinearValuation(900)),
        new("csk_power", "near_sight", new LinearValuation(900)),
        new("csk_power", "neko_leader", new LinearValuation(900)),
        new("csk_power", "neko_scratch", new LinearValuation(900)),
        new("csk_power", "neko_trance", new LinearValuation(900)),
        new("csk_power", "ob_card", new LinearValuation(900)),
        new("csk_power", "oblivionis", new LinearValuation(900)),
        new("csk_power", "parfait_delivery", new LinearValuation(900)),
        new("csk_power", "parfait_treat", new LinearValuation(900)),
        new("csk_power", "patience", new LinearValuation(900)),
        new("csk_power", "play_immediately", new LinearValuation(900)),
        new("csk_power", "pressure_dmg", new LinearValuation(900)),
        new("csk_power", "rain_omen", new LinearValuation(900)),
        new("csk_power", "rana_live", new LinearValuation(900)),
        new("csk_power", "saki_parry", new LinearValuation(900)),
        new("csk_power", "saki_sweep", new LinearValuation(900)),
        new("csk_power", "shrink", new LinearValuation(900)),
        new("csk_power", "stage_nerves", new LinearValuation(900)),
        new("csk_power", "steady_practice", new LinearValuation(900)),
        new("csk_power", "unforgettable", new LinearValuation(900)),
        new("csk_power", "vocal_harmony", new LinearValuation(900)),
        new("csk_power", "want_both", new LinearValuation(900)),
        new("csk_power", "want_more_parfait", new LinearValuation(900)),
        new("csk_power", "weak_me_dead", new LinearValuation(900)),
        new("csk_power", "when_perform", new LinearValuation(900)),
        new("csk_pressure", "apply", new LinearValuation(260)),
        new("csk_pressure", "apply_all", new LinearValuation(120)),
        new("csk_recall", "random", new LinearValuation(550)),
        new("csk_scale", "block_by_enemies", new LinearValuation(620)),
        new("csk_scale", "block_by_piano", new LinearValuation(820)),
        new("csk_scale", "block_by_pressure", new LinearValuation(820)),
        new("csk_scale", "block_by_stored_chords", new LinearValuation(660)),
        new("csk_scale", "damage_by_block", new LinearValuation(200)),
        new("csk_scale", "damage_by_forget_pile", new LinearValuation(700)),
        new("csk_scale", "damage_by_piano", new LinearValuation(900)),
        new("csk_scale", "damage_by_pressure", new LinearValuation(900)),
        new("csk_scale", "damage_by_target_pressure", new LinearValuation(300)),
        new("csk_scale", "damage_hits_by_piano", new LinearValuation(950)),
        new("csk_scale", "double", new LinearValuation(420)),
        new(SakiComponents.StatusOpcode, "fraud", new LinearValuation(1), NegativeLinearValuePerUnit: 420),
        new(SakiComponents.StatusOpcode, "shirk", new LinearValuation(1), NegativeLinearValuePerUnit: 420),
        new("csk_token", "consume_neko_damage", new LinearValuation(200)),
        new("csk_token", "neko", new LinearValuation(550)),
        new("csk_token", "neko_all_piles", new LinearValuation(700)),
        new("csk_token", "neko_draw", new LinearValuation(460)),
        new("csk_token", "neko_block", new LinearValuation(600)),
        new("csk_token", "neko_retain", new LinearValuation(720)),
        new("csk_token", "play_all_piano", new LinearValuation(760)),
        new("csk_token", "play_all_swords", new LinearValuation(380)),
        new("csk_token", "play_forget_memory", new LinearValuation(400)),
        new("csk_token", "play_neko_anywhere", new LinearValuation(720)),
        new("csk_token", "play_neko_draw", new LinearValuation(700)),
        new("csk_token", "play_piano_draw", new LinearValuation(360)),
        new("csk_token", "sword_boost", new LinearValuation(420)),
        new("csk_token", "sword_pressure_damage", new LinearValuation(720)),
        new("csk_token", "sword_upgrade", new LinearValuation(500)),
        new("csk_token", "upgrade_all_piano", new LinearValuation(540)),
        new("csk_token", "upgrade_neko", new LinearValuation(520)),
        new(SakiComponents.ParfaitOpcode, SakiComponents.ParfaitEatVariant, new LinearValuation(1), NegativeLinearValuePerUnit: ParfaitUnit),
    ];
}

/// <summary>线性估值：值 = 系数 × 第一个显式固定数值槽（缺省按 1）。</summary>
internal sealed class LinearValuation(int perUnit) : IComponentValuation
{
    public int Estimate(ComponentValuationContext context) =>
        Math.Max(1, perUnit * Math.Max(1, context.FirstExplicitFixedValue()));
}
