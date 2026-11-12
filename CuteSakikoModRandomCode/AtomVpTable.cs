using ChaosCardGenerator;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 原子 VP 表（P1 只读评估用）：按 `提示词\卡牌数值基准表.txt` §1 的系数，
/// 从 RuntimeSpec 的实际效果反算 VP。
///
/// ★ 这张表**只用于 CardValuator 的日志**，不注册给 AA、不改生成行为。
///   注册给 AA 会重演 2026-11-11 A 方案的失败（AA 会把计数器抬到 10），
///   所以 P1 阶段严格隔离。
///
/// ★ 净价值：食用芭菲、结束回合、塞状态牌这类原子会返回接近 0 或负值。
/// </summary>
internal static class AtomVpTable
{
    // 本体 VP 系数（卡牌数值基准表 §1）
    private const double Dmg = 0.5;
    private const double Block = 1.0;
    private const double Draw = 2.75;
    private const double Energy = 5.75;
    private const double Debuff = 2.0;
    private const double Pressure = 0.75;
    private const double Parfait = 9.0;
    private const double Gold = 0.35;
    private const double Heal = 0.6;
    private const double Memory = 2.75;
    private const double Forget = 1.5;
    private const double Live = 0.4;
    private const double PowerLayer = 1.5;
    private const double ChordPlay = 2.75;
    private const double NoteAttack = 2.0;     // 4 伤
    private const double NoteSkill = 3.0;      // 3 挡
    private const double NotePower = 2.75;     // 抽 1
    private const double NoteSpecial = 5.75;   // 1 能量
    private const double Aoe = 1.3;

    /// <summary>一个原子的净 VP（不含费用；费用由整卡目标带负责）。</summary>
    internal static double Vp(OperationRuntimeSpec spec)
    {
        // 自定义原子走 csk_* 表；原生原子（T:D / N:B / N:Draw 等）走槽位通用估值。
        if (spec.Opcode.StartsWith("csk_", StringComparison.Ordinal))
            return VpCustom(spec);
        return VpNative(spec);
    }

    /// <summary>
    /// 原生原子：AA 的 RuntimeSpec 已经把数值放进约定槽位
    /// （damage / block / draw / energy / heal / gold），按槽位通用折算。
    /// 触发标记（A:turnStart 等）和未识别槽位返回 0。
    /// </summary>
    private static double VpNative(OperationRuntimeSpec spec)
    {
        int V(string id) => spec.Values.FirstOrDefault(value => value.Id == id)?.BaseValue ?? 0;
        var aoe = spec.Target == "all_enemies" ? Aoe : 1.0;
        var hits = Math.Max(1, V("hits"));
        var damage = V("damage");
        if (damage > 0) return damage * hits * Dmg * aoe;
        var block = V("block");
        if (block > 0) return block * Block;
        var draw = V("draw");
        if (draw > 0) return draw * Draw;
        var energy = V("energy");
        if (energy > 0) return energy * Energy;
        var heal = V("heal");
        if (heal > 0) return heal * Heal;
        // N:Heal 的槽位是 amount（不是 heal）；I:GainMaxHp 也是 amount。
        if (spec.Opcode == "heal") return V("amount") * Heal;
        if (spec.Opcode == "gain_max_hp") return V("amount") * 3.0;
        var gold = V("gold");
        if (gold > 0) return gold * Gold;
        return 0d;
    }

    private static double VpCustom(OperationRuntimeSpec spec)
    {
        int V(string id) => spec.Values.FirstOrDefault(value => value.Id == id)?.BaseValue ?? 0;
        double Damage(int amount, bool aoe = false) => amount * Dmg * (aoe ? Aoe : 1);
        double BlockValue(int amount) => amount * Block;
        double DrawValue(int amount) => amount * Draw;
        double EnergyValue(int amount) => amount * Energy;

        return spec.Opcode switch
        {
            "csk_pressure" => spec.Target == "self"
                ? V("amount") * 0.4
                : V("amount") * Pressure * (spec.Target == "all_enemies" ? Aoe : 1),
            "csk_energy" => EnergyValue(V("energy")),
            "csk_extreme" => spec.Variant switch
            {
                "end_turn_draw" => DrawValue(V("count")),
                "end_turn_energy" => EnergyValue(V("count")),
                "end_turn_damage" => Damage(V("damage"), true),
                _ => 0
            },
            "csk_cost" => spec.Variant switch
            {
                "free_hand" => DrawValue(1) + V("count") * 2.5,
                "transform_hand" => DrawValue(1) + V("count") * 2.0,
                _ => 0
            },
            "csk_growth" => spec.Variant switch
            {
                // 本场成长按平均 2.5 次触发估
                "damage_growth" => Damage(V("damage")) + V("growth") * Dmg * 2.5,
                "damage_growth_block" => Damage(V("damage")) + V("growth") * Block * 2.5,
                "damage_growth_draw" => Damage(V("damage")) + V("growth") * Draw * 2.5,
                "damage_growth_memory" => Damage(V("damage")) + V("growth") * Memory * 2.5,
                "damage_growth_sword" => Damage(V("damage")) + V("growth") * 2.0 * 2.5,
                "block_growth_block" => BlockValue(V("block")) + V("growth") * Block * 2.5,
                "block_growth_memory" => BlockValue(V("block")) + V("growth") * Memory * 2.5,
                "block_growth_parfait" => BlockValue(V("block")) + V("growth") * Parfait * 2.0,
                "block_growth_sword" => BlockValue(V("block")) + V("growth") * 2.0 * 2.5,
                "block_growth_chord" => BlockValue(V("block")) + V("growth") * ChordPlay * 2.5,
                _ => 0
            },
            "csk_status" => -2.0 * V("count"),
            "csk_condition" or "csk_condition_blocked" => spec.Variant switch
            {
                "damage_all_or_heal" => Math.Max(Damage(V("damage"), true), V("heal") * Heal),
                "damage_draw_if_unblocked" => (Damage(V("damage")) + DrawValue(V("draw"))) * 0.6,
            "damage_draw_if_blocked" => (Damage(V("damage")) + DrawValue(V("draw"))) * 0.6,
                "damage_memory_on_kill" => Damage(V("damage")) + Memory * V("count") * 0.4,
                "damage_play_memorized_on_kill" => Damage(V("damage")) + ChordPlay * 3 * 0.4,
                "damage_shrink_if_less_pressure" => Damage(V("damage")) + V("shrink") * 4.0 * 0.5,
            "damage_shrink_if_more_pressure" => Damage(V("damage")) + V("shrink") * 4.0 * 0.5,
                "damage_vulnerable_if_unblocked" => Damage(V("damage")) + V("count") * Debuff * 0.6,
            "damage_vulnerable_if_blocked" => Damage(V("damage")) + V("count") * Debuff * 0.6,
                "damage_energy_on_kill" => Damage(V("damage")) + EnergyValue(V("count")) * 0.4,
                "damage_sword_on_kill" => Damage(V("damage")) + 2.0 * V("count") * 0.4,
                "double_collapse_bonus" => (DrawValue(V("draw")) + EnergyValue(1)) * 0.4 + 1.5,
                "double_collapse_damage" => Damage(V("damage"), true) * 0.4 + 1.5,
                "double_collapse_draw" => DrawValue(V("count")) * 0.4 + 1.5,
                "double_collapse_power" => PowerLayer * V("count") * 0.4 + 1.5,
                "double_collapse_memory" => Memory * V("count") * 0.4 + 1.5,
                "double_collapse_sword" => 2.0 * V("count") * 0.4 + 1.5,
                "double_collapse_sword_boost" => 1.5 * V("count") * 0.4 + 1.5,
                "pressure_more_shrink" or "pressure_less_shrink" => Damage(V("damage")) + V("count") * 4.0 * 0.5,
                "pressure_more_vulnerable" or "pressure_less_vulnerable" =>
                    Damage(V("damage")) + V("count") * Debuff * 0.5,
                "pressure_more_pressure" or "pressure_less_pressure" =>
                    Damage(V("damage")) + V("count") * Pressure * 0.5,
                "pressure_more_doublepressure" or "pressure_less_doublepressure" =>
                    Damage(V("damage")) + 2.0,
                "pressure_more_bonusdamage" or "pressure_less_bonusdamage" =>
                    Damage(V("damage")) + Damage(V("bonus")) * 0.5,
                "pressure_more_block" or "pressure_less_block" =>
                    Damage(V("damage")) + BlockValue(V("block")) * 0.5,
                "pressure_more_draw" or "pressure_less_draw" =>
                    Damage(V("damage")) + DrawValue(V("draw")) * 0.5,
                "pressure_more_energy" or "pressure_less_energy" =>
                    Damage(V("damage")) + EnergyValue(V("energy")) * 0.5,
                "unblocked_block" => Damage(V("damage")) + BlockValue(V("block")) * 0.6,
                "unblocked_draw" => Damage(V("damage")) + DrawValue(V("draw")) * 0.6,
                "unblocked_energy" => Damage(V("damage")) + EnergyValue(V("energy")) * 0.6,
                "unblocked_bonusdamage" => Damage(V("damage")) + Damage(V("bonus")) * 0.6,
                "blocked_block" => Damage(V("damage")) + BlockValue(V("block")) * 0.5,
                "blocked_draw" => Damage(V("damage")) + DrawValue(V("draw")) * 0.5,
                "blocked_energy" => Damage(V("damage")) + EnergyValue(V("energy")) * 0.5,
                "blocked_bonusdamage" => Damage(V("damage")) + Damage(V("bonus")) * 0.5,
                "kill_block" => Damage(V("damage")) + BlockValue(V("block")) * 0.4,
                "kill_draw" => Damage(V("damage")) + DrawValue(V("draw")) * 0.4,
                "kill_energy" => Damage(V("damage")) + EnergyValue(V("energy")) * 0.4,
                "kill_bonusdamage" => Damage(V("damage")) + Damage(V("bonus")) * 0.4,
                "notkill_block" => Damage(V("damage")) + BlockValue(V("block")) * 0.6,
                "notkill_draw" => Damage(V("damage")) + DrawValue(V("draw")) * 0.6,
                "notkill_energy" => Damage(V("damage")) + EnergyValue(V("energy")) * 0.6,
                "notkill_bonusdamage" => Damage(V("damage")) + Damage(V("bonus")) * 0.6,
                _ => 0
            },
            "csk_deck" => spec.Variant switch
            {
                "autoplay_hand" => V("count") * 2.5,
                "clear_debuff" => V("count") * 2.0,
                "copy_self" => V("count") * 3.0,
                "discard_all" => BlockValue(V("count")) - 2.0,
                "discard_non_parfait" => DrawValue(2) - 2.0,
                "draw_to_hand" => DrawValue(V("count")) + 0.5,
                "exhaust_status" => DrawValue(V("count")) + 1.0,
                "fill_hand_recall" => Memory * V("count") + 3.0,
                "forget_all_hand" => Memory * 2 * 0.5,
                "forget_from_discard" => Forget * V("count"),
                "play_and_forget" => 2.5 - Forget,
                "play_hand_then_forget" => 2.5 - Forget,
                "pressure_max_hp" => V("maxhp") * 3.0 - V("pressure") * Pressure,
                "recall_from_exhaust" => Memory * V("count"),
                "recall_from_forget" => Memory * V("count"),
                "relieve_draw" => DrawValue(V("draw")) - V("pressure") * Pressure,
                "return_to_hand" => 3.0 - V("count") * Pressure,
                _ => 0
            },
            "csk_forget" => Forget * V("count") * (spec.Variant == "random" ? 0.85 : 1.0),
            "csk_gold" => Gold * V("count"),
            "csk_intent" => 2.0,
            "csk_note" => spec.Variant switch
            {
                "add_attack" => NoteAttack * V("count"),
                "add_skill" => NoteSkill * V("count"),
                "add_power" => NotePower * V("count"),
                "add_special" => NoteSpecial * V("count"),
                "add_attack_damage" => NoteAttack * V("count") + Damage(V("damage")),
                "add_skill_block" => NoteSkill * V("count") + BlockValue(V("block")),
                "add_attack_draw" => NoteAttack * V("count") + DrawValue(V("draw")),
                "add_attack_energy" => NoteAttack * V("count") + EnergyValue(V("energy")),
                "clear" => (NoteAttack + NoteSkill + NotePower + NoteSpecial) / 4 * V("count"),
                "clear_scale_block" => BlockValue(V("count")),
                "clear_scale_draw" => DrawValue(V("count")),
                "note_and_trigger" => NoteAttack * V("attack") + NoteSkill * V("skill"),
                "random" => (NoteAttack + NoteSkill + NotePower + NoteSpecial) / 4 * V("count"),
                _ => 0
            },
            "csk_parfait" => spec.Variant switch
            {
                "buy" => Parfait - Gold * V("count"),
                "charge" => Parfait * V("amount"),
                "consume_as_parfait" => (Parfait - 1.5) * V("count"),
                "eat" => -Parfait * V("amount"),
                _ => 0
            },
            "csk_parfait_combo" => spec.Variant switch
            {
                "eat_block" => BlockValue(V("block")) - Parfait * V("cups"),
                "eat_damage_all" => Damage(V("damage"), true) - Parfait * V("cups"),
                "eat_heal_draw" => V("heal") * Heal + DrawValue(1) - Parfait * V("cups"),
            "eat_draw" => DrawValue(V("draw")) - Parfait * V("cups"),
                _ => 0
            },
            "csk_pay_pressure" => spec.Variant switch
            {
                // 代价槽是「消耗几层压力」；必须按实际 cost 扣，否则升代价不会降 V。
                "block" => BlockValue(V("block")) + BlockValue(V("extra")) * 0.6
                           - Math.Max(1, V("cost")) * Pressure,
                "damage" => Damage(V("damage")) * (1 + V("hits") * 0.6)
                            - Math.Max(1, V("cost")) * Pressure,
                "draw" => Damage(V("damage")) + DrawValue(V("draw")) * 0.6
                          - Math.Max(1, V("cost")) * Pressure,
                "recall" => Memory * V("count") * 0.6 - Math.Max(1, V("cost")) * Pressure,
                // weak_all 的 cost 同时是「压力层数×倍率」的基数，升它=同时升收益，不能当纯代价。
                "weak_all" => V("cost") * V("multiplier") * Debuff * 0.8 - Pressure,
                _ => 0
            },
            "csk_play_chord" => spec.Variant switch
            {
                "stored" => ChordPlay * V("count"),
                "stored_multi" => ChordPlay * V("count"),
                "play_last" => ChordPlay * V("count"),
                "random_memorized" => ChordPlay * V("count"),
                "remember" => ChordPlay * V("count"),
                "play_stored_block" => ChordPlay * V("count") + BlockValue(V("block")),
                "play_stored_damage" => ChordPlay * V("count") + Damage(V("damage")),
                "grant_slot" => ChordPlay * V("count"),
                _ => 0
            },
            "csk_power" => spec.Variant switch
            {
                "intangible" => 9.0 * V("amount"),
                "rana_live" => Live * V("amount"),
                _ => PowerLayer * V("amount")
            },
            "csk_recall" => Memory * V("count"),
            "csk_scale" => spec.Variant switch
            {
                "block_by_pressure" => 3.0 * V("count"),
                "block_by_enemies" => 2.0 * V("count"),
                "block_by_piano" => 2.0 * V("count"),
                "block_by_stored_chords" => 1.5 * V("count"),
                "damage_by_pressure" => 1.5 * V("count"),
                "damage_by_block" => 0.5 * V("count"),
                "damage_by_forget_pile" => 1.0 * V("count"),
                "damage_by_piano" => 1.0 * V("count"),
                "damage_by_target_pressure" => 1.0 * V("count"),
                "damage_hits_by_piano" => Damage(V("damage")) * 3,
                "double" => 2.0,
                "spend_all_energy" => 4.0,
                "spend_all_energy_one" => 5.0,
                _ => 0
            },
            "csk_token" => spec.Variant switch
            {
                "neko" => Memory * V("amount"),
                "neko_all_piles" => Memory * 2 * Math.Max(1, V("count")),
                "neko_draw" => Memory * 0.8 * Math.Max(1, V("count")),
            "neko_block" => Memory * Math.Max(1, V("count")) + BlockValue(V("block")),
                "neko_retain" => 3.5 * V("count"),
                "play_all_piano" => 3.0,
                "play_all_swords" => 2.0 * Math.Max(1, V("count")),
                "play_forget_memory" => 3.0 * V("count"),
                "play_neko_anywhere" => Memory * V("count"),
                "play_neko_draw" => Memory * 0.9 * V("count"),
                "play_piano_draw" => ChordPlay * 0.9 * V("count"),
                "sword_boost" => 2.0 * V("count"),
                "sword_pressure_damage" => Damage(V("count") + 3),
                "sword_upgrade" => 2.5 * V("count"),
                "upgrade_all_piano" => 3.0,
                "upgrade_neko" => 3.0 * V("count"),
                "sword_from_anywhere" => 2.5 * V("count"),
                "sword" => 3.0,
                "candy" => 2.5,
                "consume_neko_damage" => Damage(V("count")) * 2,
                _ => 0
            },
            _ => 0
        };
    }
}


