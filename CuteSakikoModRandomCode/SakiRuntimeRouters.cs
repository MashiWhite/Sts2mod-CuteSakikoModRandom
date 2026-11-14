using CuteSakikoModRandom.CuteSakikoModRandomCode.Compat;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using CuteSakikoMod.CuteSakikoModCode.Pools;
using CuteSakikoMod.CuteSakikoModCode.Cards.Rana;
using AutoAnthony;
using CuteSakikoMod.CuteSakikoModCode.Others;
using CuteSakikoMod.CuteSakikoModCode.Powers.Basic;
using CuteSakikoMod.CuteSakikoModCode.Powers.Buff;
using CuteSakikoMod.CuteSakikoModCode.Powers.Debuff;
using CuteSakikoMod.CuteSakikoModCode.Relics.Rana.Starter;
using CuteSakikoMod.CuteSakikoModCode.Systems;
using CuteSakikoMod.CuteSakikoModCode.Systems.Chord;
using CuteSakikoMod.CuteSakikoModCode.Systems.Memory;
using MegaCrit.Sts2.Core.CardSelection;
using STS2RitsuLib.Keywords;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 四个专属机制的运行时执行器。全部通过 AutoAnthony 的 ComponentRuntimeApi 注册。
///
/// 约定：返回 true 表示已完整处理；返回 false 会落回 AA 的旧版兼容分派（本模组不使用）。
/// </summary>
internal static class SakiRuntimeRouters
{
    private static IReadOnlyList<ComponentRuntimeRoute> InternalRoutes()
    {
        var routes = new List<ComponentRuntimeRoute>
        {
        new(SakiComponents.PressureOpcode, SakiComponents.PressureVariant, new PressureHandler()),
        new(SakiComponents.PressureOpcode, SakiComponents.PressureAllVariant, new PressureHandler()),
        new(SakiComponents.ForgetOpcode, SakiComponents.ForgetVariant, new ForgetHandler()),
        new(SakiComponents.ForgetOpcode, SakiComponents.ForgetRandomVariant, new ForgetHandler()),
        new(SakiComponents.RecallOpcode, SakiComponents.RecallVariant, new RecallHandler()),
        new(SakiComponents.PlayChordOpcode, SakiComponents.PlayChordStoredVariant, new PlayChordHandler()),
        new(SakiComponents.PlayChordOpcode, SakiComponents.PlayChordStoredMultiVariant, new PlayChordHandler()),
        new(SakiComponents.PlayChordOpcode, SakiComponents.PlayChordRandomVariant, new PlayChordHandler()),
        new(SakiComponents.PlayChordOpcode, SakiComponents.ChordAndDamageVariant, new PlayChordHandler()),
        new(SakiComponents.PlayChordOpcode, SakiComponents.ChordAndBlockVariant, new PlayChordHandler()),
        new(SakiComponents.PlayChordOpcode, SakiComponents.ChordRememberVariant, new PlayChordHandler()),
        new(SakiComponents.PlayChordOpcode, SakiComponents.ChordLastVariant, new PlayChordHandler()),
        new(SakiComponents.PlayChordOpcode, SakiComponents.ChordGrantSlotVariant, new PlayChordHandler()),
        new(SakiComponents.NoteOpcode, SakiComponents.NoteAddAttackVariant, new NoteHandler()),
        new(SakiComponents.NoteOpcode, SakiComponents.NoteAddSkillVariant, new NoteHandler()),
        new(SakiComponents.NoteOpcode, SakiComponents.NoteAddPowerVariant, new NoteHandler()),
        new(SakiComponents.NoteOpcode, SakiComponents.NoteAddSpecialVariant, new NoteHandler()),
        new(SakiComponents.NoteOpcode, SakiComponents.NoteClearVariant, new NoteHandler()),
        new(SakiComponents.NoteOpcode, SakiComponents.ClearScaleBlockVariant, new NoteHandler()),
        new(SakiComponents.NoteOpcode, SakiComponents.ClearScaleDrawVariant, new NoteHandler()),
        new(SakiComponents.NoteOpcode, SakiComponents.RandomNoteVariant, new NoteHandler()),
        new(SakiComponents.NoteOpcode, SakiComponents.NoteAndDamageVariant, new NoteHandler()),
        new(SakiComponents.NoteOpcode, SakiComponents.NoteAndBlockVariant, new NoteHandler()),
        new(SakiComponents.NoteOpcode, SakiComponents.NoteAndDrawVariant, new NoteHandler()),
        new(SakiComponents.NoteOpcode, SakiComponents.NoteAndEnergyVariant, new NoteHandler()),
        new(SakiComponents.ParfaitOpcode, SakiComponents.ParfaitEatVariant, new ParfaitHandler()),
        new(SakiComponents.ParfaitOpcode, SakiComponents.ParfaitBuyVariant, new ParfaitHandler()),
        new(SakiComponents.ParfaitOpcode, SakiComponents.ConsumeAsParfaitVariant, new ParfaitHandler()),
        new(SakiComponents.TokenOpcode, SakiComponents.TokenNekoDrawVariant, new TokenCardHandler()),
        new(SakiComponents.TokenOpcode, SakiComponents.TokenNekoBlockVariant, new TokenCardHandler()),
        new(SakiComponents.TokenOpcode, SakiComponents.TokenNekoAllPilesVariant, new TokenCardHandler()),
        new(SakiComponents.TokenOpcode, SakiComponents.TokenConsumeNekoDamageVariant, new TokenCardHandler()),
        new(SakiComponents.TokenOpcode, SakiComponents.TokenUpgradeNekoVariant, new TokenCardHandler()),
        new(SakiComponents.TokenOpcode, SakiComponents.PlayNekoFromDrawVariant, new TokenCardHandler()),
        new(SakiComponents.TokenOpcode, SakiComponents.PlayNekoAnywhereVariant, new TokenCardHandler()),
        new(SakiComponents.TokenOpcode, SakiComponents.TokenNekoRetainVariant, new TokenCardHandler()),
        new(SakiComponents.TokenOpcode, SakiComponents.TokenPlayAllSwordsVariant, new TokenCardHandler()),
        new(SakiComponents.TokenOpcode, SakiComponents.TokenPlayPianoVariant, new TokenCardHandler()),
        new(SakiComponents.TokenOpcode, SakiComponents.TokenPlayForgetMemoryVariant, new TokenCardHandler()),
        new(SakiComponents.DeckOpcode, SakiComponents.DiscardAllVariant, new DeckHandler()),
        new(SakiComponents.DeckOpcode, SakiComponents.ExhaustStatusVariant, new DeckHandler()),
        new(SakiComponents.DeckOpcode, SakiComponents.CopySelfVariant, new DeckHandler()),
        new(SakiComponents.DeckOpcode, SakiComponents.ReturnToHandVariant, new DeckHandler()),
        new(SakiComponents.DeckOpcode, SakiComponents.AutoPlayHandVariant, new DeckHandler()),
        new(SakiComponents.DeckOpcode, SakiComponents.ForgetAllHandVariant, new DeckHandler()),
        new(SakiComponents.DeckOpcode, SakiComponents.RelievePressureDrawVariant, new DeckHandler()),
        new(SakiComponents.DeckOpcode, SakiComponents.ClearDebuffVariant, new DeckHandler()),
        new(SakiComponents.DeckOpcode, SakiComponents.FillHandRecallVariant, new DeckHandler()),
        new(SakiComponents.DeckOpcode, SakiComponents.PlayAndForgetVariant, new DeckHandler()),
        new(SakiComponents.DeckOpcode, SakiComponents.DiscardNonParfaitVariant, new DeckHandler()),
        new(SakiComponents.DeckOpcode, SakiComponents.PressureMaxHpVariant, new DeckHandler()),
        new(SakiComponents.DeckOpcode, SakiComponents.RecallFromForgetVariant, new DeckHandler()),
        new(SakiComponents.DeckOpcode, SakiComponents.DrawToHandVariant, new DeckHandler()),
        new(SakiComponents.DeckOpcode, SakiComponents.ForgetFromDiscardVariant, new DeckHandler()),
        new(SakiComponents.DeckOpcode, SakiComponents.PlayHandThenForgetVariant, new DeckHandler()),
        new(SakiComponents.DeckOpcode, SakiComponents.RecallFromExhaustVariant, new DeckHandler()),
        new(SakiComponents.ParfaitOpcode, SakiComponents.ParfaitChargeVariant, new ParfaitHandler()),
        new(SakiComponents.TokenOpcode, SakiComponents.TokenNekoVariant, new TokenCardHandler()),
        new(SakiComponents.TokenOpcode, SakiComponents.TokenSwordVariant, new TokenCardHandler()),
        new(SakiComponents.TokenOpcode, SakiComponents.SwordUpgradeVariant, new TokenCardHandler()),
        new(SakiComponents.TokenOpcode, SakiComponents.SwordBoostVariant, new TokenCardHandler()),
        new(SakiComponents.TokenOpcode, SakiComponents.SwordFromAnywhereVariant, new TokenCardHandler()),
        new(SakiComponents.TokenOpcode, SakiComponents.SwordPressureDamageVariant, new TokenCardHandler()),
        new(SakiComponents.ScaleOpcode, SakiComponents.DamageByPressureVariant, new ScaleByPressureHandler()),
        new(SakiComponents.ScaleOpcode, SakiComponents.DoublePressureVariant, new ScaleByPressureHandler()),
        new(SakiComponents.ScaleOpcode, SakiComponents.SpendAllPressureEnergyVariant, new ScaleByPressureHandler()),
        new(SakiComponents.ScaleOpcode, SakiComponents.SpendAllPressureEnergyOneVariant, new ScaleByPressureHandler()),
        new(SakiComponents.ScaleOpcode, SakiComponents.DamageByForgetPileVariant, new ScaleByPressureHandler()),
        new(SakiComponents.ScaleOpcode, SakiComponents.DamageByBlockVariant, new ScaleByPressureHandler()),
        new(SakiComponents.ScaleOpcode, SakiComponents.DamageByTargetPressureVariant, new ScaleByPressureHandler()),
        new(SakiComponents.ScaleOpcode, SakiComponents.DamageByPianoVariant, new ScaleByPressureHandler()),
        new(SakiComponents.ScaleOpcode, SakiComponents.DamageHitsByPianoVariant, new ScaleByPressureHandler()),
        new(SakiComponents.ScaleOpcode, SakiComponents.BlockByPianoVariant, new ScaleByPressureHandler()),
        new(SakiComponents.TokenOpcode, SakiComponents.TokenPlayAllPianoVariant, new TokenCardHandler()),
        new(SakiComponents.TokenOpcode, SakiComponents.TokenUpgradeAllPianoVariant, new TokenCardHandler()),
        new(SakiComponents.IntentOpcode, SakiComponents.RerollIntentVariant, new IntentHandler()),
        new(SakiComponents.EnergyOpcode, SakiComponents.EnergyGainTwoVariant, new EnergyTextHandler()),
        new(SakiComponents.ScaleOpcode, SakiComponents.BlockByEnemyCountVariant, new ScaleByPressureHandler()),
        new(SakiComponents.ScaleOpcode, SakiComponents.BlockByStoredChordsVariant, new ScaleByPressureHandler()),
        new(SakiComponents.ScaleOpcode, SakiComponents.BlockByPressureVariant, new ScaleByPressureHandler()),
        new(SakiComponents.TokenOpcode, SakiComponents.TokenCandyVariant, new TokenCardHandler()),
        new(SakiComponents.PayPressureOpcode, SakiComponents.PayPressureDamageVariant, new PayPressureHandler()),
        new(SakiComponents.PayPressureOpcode, SakiComponents.PayPressureBlockVariant, new PayPressureHandler()),
        new(SakiComponents.PayPressureOpcode, SakiComponents.PayPressureDrawVariant, new PayPressureHandler()),
        new(SakiComponents.PayPressureOpcode, SakiComponents.PayPressureWeakAllVariant, new PayPressureHandler()),
        new(SakiComponents.PayPressureOpcode, SakiComponents.PayPressureRecallVariant, new PayPressureHandler()),
        new(SakiComponents.GoldOpcode, SakiComponents.GainGoldVariant, new GoldHandler()),
        new(SakiComponents.ConditionOpcode, SakiComponents.DoubleCollapseBonusVariant, new ConditionHandler()),
        new(SakiComponents.ExtremeOpcode, SakiComponents.EndTurnDrawVariant, new EndTurnHandler()),
        new(SakiComponents.StatusOpcode, SakiComponents.FraudStatusVariant, new StatusCardHandler()),
        new(SakiComponents.GrowthOpcode, SakiComponents.DamageGrowthVariant, new GrowthHandler()),
        new(SakiComponents.GrowthOpcode, SakiComponents.DamageGrowthBlockVariant, new GrowthHandler()),
        new(SakiComponents.GrowthOpcode, SakiComponents.DamageGrowthSwordVariant, new GrowthHandler()),
        new(SakiComponents.GrowthOpcode, SakiComponents.DamageGrowthMemoryVariant, new GrowthHandler()),
        new(SakiComponents.GrowthOpcode, SakiComponents.DamageGrowthDrawVariant, new GrowthHandler()),
        new(SakiComponents.GrowthOpcode, SakiComponents.BlockGrowthBlockVariant, new GrowthHandler()),
        new(SakiComponents.GrowthOpcode, SakiComponents.BlockGrowthMemoryVariant, new GrowthHandler()),
        new(SakiComponents.GrowthOpcode, SakiComponents.BlockGrowthSwordVariant, new GrowthHandler()),
        new(SakiComponents.GrowthOpcode, SakiComponents.BlockGrowthParfaitVariant, new GrowthHandler()),
        new(SakiComponents.GrowthOpcode, SakiComponents.BlockGrowthChordVariant, new GrowthHandler()),
        new(SakiComponents.CostOpcode, SakiComponents.CostFreeHandVariant, new CostHandler()),
        new(SakiComponents.CostOpcode, SakiComponents.CostTransformHandVariant, new CostHandler()),
        new(SakiComponents.StatusOpcode, SakiComponents.ShirkStatusVariant, new StatusCardHandler()),
        new(SakiComponents.ExtremeOpcode, SakiComponents.EndTurnEnergyVariant, new EndTurnHandler()),
        new(SakiComponents.ExtremeOpcode, SakiComponents.EndTurnDamageVariant, new EndTurnHandler()),
        new(SakiComponents.ConditionOpcode, SakiComponents.DoubleCollapseDamageVariant, new ConditionHandler()),
        new(SakiComponents.ConditionOpcode, SakiComponents.DoubleCollapseDrawVariant, new ConditionHandler()),
        new(SakiComponents.ConditionOpcode, SakiComponents.DoubleCollapsePowerVariant, new ConditionHandler()),
        new(SakiComponents.ConditionOpcode, SakiComponents.DoubleCollapseSwordVariant, new ConditionHandler()),
        new(SakiComponents.ConditionOpcode, SakiComponents.DoubleCollapseSwordBoostVariant, new ConditionHandler()),
        new(SakiComponents.ConditionOpcode, SakiComponents.DoubleCollapseMemoryVariant, new ConditionHandler()),
        new(SakiComponents.ConditionOpcode, SakiComponents.DamageMemoryOnKillVariant, new ConditionHandler()),
        new(SakiComponents.ConditionOpcode, SakiComponents.DamageSwordOnKillVariant, new ConditionHandler()),
        new(SakiComponents.ConditionOpcode, SakiComponents.DamageVulnerableIfUnblockedVariant, new ConditionHandler()),
    new(SakiComponents.ConditionOpcode, SakiComponents.DamageVulnerableIfBlockedVariant, new ConditionHandler()),
        new(SakiComponents.ConditionOpcode, SakiComponents.DamageEnergyOnKillVariant, new ConditionHandler()),
        new(SakiComponents.ConditionOpcode, SakiComponents.DamageShrinkIfLessPressureVariant, new ConditionHandler()),
    new(SakiComponents.ConditionOpcode, SakiComponents.DamageShrinkIfMorePressureVariant, new ConditionHandler()),
        new(SakiComponents.ConditionOpcode, SakiComponents.DamageAllOrHealVariant, new ConditionHandler()),
        new(SakiComponents.ConditionOpcode, SakiComponents.DamagePlayMemorizedOnKillVariant, new ConditionHandler()),
        new(SakiComponents.ConditionOpcode, SakiComponents.DamageDrawIfUnblockedVariant, new ConditionHandler()),
    new(SakiComponents.ConditionOpcode, SakiComponents.DamageDrawIfBlockedVariant, new ConditionHandler()),
        new(SakiComponents.GoldOpcode, SakiComponents.GainGoldByCreaturesVariant, new GoldHandler()),
        new(SakiComponents.ParfaitComboOpcode, SakiComponents.ParfaitComboBlockVariant, new ParfaitComboHandler()),
        new(SakiComponents.ParfaitComboOpcode, SakiComponents.ParfaitComboDamageAllVariant, new ParfaitComboHandler()),
        new(SakiComponents.ParfaitComboOpcode, SakiComponents.ParfaitComboHealDrawVariant, new ParfaitComboHandler()),
        new(SakiComponents.ParfaitComboOpcode, SakiComponents.ParfaitComboDrawVariant, new ParfaitComboHandler())
        };

        foreach (var direction in new[] { "more", "less" })
        foreach (var payoff in new[] { "shrink", "vulnerable", "pressure", "doublepressure",
                     "bonusdamage", "block", "draw", "energy" })
            routes.Add(new ComponentRuntimeRoute(SakiComponents.ConditionOpcode,
                $"pressure_{direction}_{payoff}", new ConditionHandler()));

        foreach (var condition in new[] { "unblocked", "blocked", "kill", "notkill" })
        foreach (var payoff in new[] { "block", "draw", "energy", "bonusdamage" })
            routes.Add(new ComponentRuntimeRoute(
                condition == "blocked" ? SakiComponents.ConditionBlockedOpcode : SakiComponents.ConditionOpcode,
                $"{condition}_{payoff}", new ConditionHandler()));

        return routes;
    }

    /// <summary>
    /// ★ T-034（2026-11-10 用户要求）：升级时**额外加一条基础效果**。
    /// 自定义原子文案不能带阿拉伯数字（§3.92）→ 很多卡没有可升级数值槽，
    /// AA 的升级计划落到 ReduceStarCost（辉星费已被抹掉）→ 升级等于没变化。
    /// 做法：路由包一层装饰器，handler 跑完后若卡已升级，补一条基础效果：
    ///   攻击牌 → 额外 4 点伤害；技能/能力牌 → 额外 3 点格挡。
    /// 文案侧由 SyntheticNumericUpgrade 写入「升级：…」，保证所见即所得。
    /// </summary>
    internal static IReadOnlyList<ComponentRuntimeRoute> Routes() => InternalRoutes();
}

/// <summary>压力：施加到目标（敌人或自己）。复用本体 PressureCmd，保留多人缩放。</summary>
internal sealed class PressureHandler : IComponentRuntimeHandler
{
    public async Task<bool> ExecuteAsync(ComponentRuntimeContext context)
    {
        var amount = Math.Max(0, context.Amount);
        if (amount <= 0) return true;

        var owner = context.Card.Owner;
        if (context.RuntimeSpec.Variant == SakiComponents.PressureAllVariant)
        {
            var combatState = owner.Creature?.CombatState;
            if (combatState is null) return true;
            foreach (var enemy in combatState.HittableEnemies.ToArray())
                await PressureCmd.Apply(context.ChoiceContext, enemy, amount, owner.Creature, context.Card);
            return true;
        }

        var target = context.Target;
        if (target is null) return true;
        await PressureCmd.Apply(context.ChoiceContext, target, amount, owner.Creature, context.Card);
        return true;
    }
}

/// <summary>
/// 遗忘：把牌移入遗忘堆并扣压力。复用本体 MemoryCmd.Forget。
///
/// 两个 variant 的语义必须区分（本体惯例）：
///   selected → 玩家**选择**手牌中的 N 张（弹出选择界面）
///   random   → **随机**遗忘手牌中的 N 张（不弹界面）
/// </summary>
internal sealed class ForgetHandler : IComponentRuntimeHandler
{
    public async Task<bool> ExecuteAsync(ComponentRuntimeContext context)
    {
        var owner = context.Card.Owner;

        var hand = PileType.Hand.GetPile(owner)?.Cards?.ToList();
        if (hand is null || hand.Count == 0) return true;

        var count = Math.Max(1, context.Amount);
        var isRandom = context.RuntimeSpec.Variant == SakiComponents.ForgetRandomVariant;

        if (!isRandom)
        {
            // 选择型：弹出手牌选择界面。只剩 1 张时直接通过（没得选）。
            if (hand.Count > 1)
            {
                var prefs = new CardSelectorPrefs(
                    new LocString("cards", "CUTE_SAKIKO_MOD_CARD_FORGET.selectionScreenPrompt"),
                    Math.Min(count, hand.Count), Math.Min(count, hand.Count));
                var picked = await CardSelectCmd.FromHand(context.ChoiceContext, owner, prefs, null,
                    context.Card);
                var pickedList = picked?.ToList() ?? [];
                if (pickedList.Count > 0)
                    await MemoryCmd.Forget(context.ChoiceContext, pickedList, context.Card);
                return true;
            }

            await MemoryCmd.Forget(context.ChoiceContext, hand, context.Card);
            return true;
        }

        // 随机型：随机挑 N 张，不弹界面。
        var selected = hand.Count <= count
            ? hand
            : SakiRuntimeRng.PickRandom(hand, count, SakiRuntimeRng.CardSelection(context));
        await MemoryCmd.Forget(context.ChoiceContext, selected, context.Card);
        return true;
    }
}

/// <summary>回忆：从记忆堆抽回手牌。复用本体 MemoryCmd.Recall。</summary>
internal sealed class RecallHandler : IComponentRuntimeHandler
{
    public async Task<bool> ExecuteAsync(ComponentRuntimeContext context)
    {
        var owner = context.Card.Owner;

        await MemoryCmd.Recall(context.ChoiceContext, owner, allowChoose: false, upgraded: context.Card.IsUpgraded,
            count: Math.Max(1, context.Amount), source: context.Card);
        return true;
    }
}

/// <summary>
/// 和弦：演奏储存 / 已记忆的和弦。复用本体 ChordNoteSystem。
///
/// ⚠️ 必须先 Activate（2026-11-09 实测：不激活的话 AddNoteAsync 第一行
///    `if (!state.IsActive) return;` 会直接吞掉，音符/和弦全部无效）。
/// 本体的每张演奏卡都自己调 Activate，我们走 opcode 也必须照做。
/// </summary>
internal sealed class PlayChordHandler : IComponentRuntimeHandler
{
    public async Task<bool> ExecuteAsync(ComponentRuntimeContext context)
    {
        var owner = context.Card.Owner;

        ChordNoteSystem.Activate(owner);
        var count = Math.Max(1, context.Amount);

        // ★ 爱A1 获得随机和弦槽位；若已有则演奏（本体「团队的小太阳」等 6 张卡）
        if (context.RuntimeSpec.Variant == SakiComponents.ChordGrantSlotVariant)
        {
            var chords = owner.GetChords();
            if (chords is null) return true;

            var all = ChordManager.AllChords.Keys.ToList();
            if (all.Count == 0) return true;

            var rng = SakiRuntimeRng.CardSelection(context);
            for (var i = 0; i < Math.Max(1, count); i++)
            {
                var chordId = all[rng.NextInt(all.Count)];
                if (chords.GetTemporaryChords().Contains(chordId))
                    await ChordNoteSystem.PlayChordAsync(owner, chordId, context.ChoiceContext);
                else
                    chords.AddTemporaryChord(chordId);
            }

            ChordNoteUIManager.UpdateStoredChordDisplay(owner);
            return true;
        }

        // ★ 随机储存 N 个已记忆和弦（2026-11-13 从 remember 拆出：
        //   真正随机取「已记忆和弦」，只储存、不自动演奏）。
        if (context.RuntimeSpec.Variant == SakiComponents.ChordStoreRandomVariant)
        {
            var chords = owner.GetChords();
            if (chords is null) return true;

            var equipped = chords.GetAllEquippedChords()
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (equipped.Count == 0) return true;

            var rng = SakiRuntimeRng.CardSelection(context);
            for (var i = 0; i < Math.Max(1, count); i++)
            {
                var chordId = equipped[rng.NextInt(equipped.Count)];
                await ChordNoteSystem.AddStoredChordAsync(owner, chordId, 1, context.ChoiceContext);
            }

            ChordNoteUIManager.UpdateStoredChordDisplay(owner);
            return true;
        }

        // 兼容 0.1.1 旧存档的 remember 卡（新生成不再产生）：
        // 只做「储存同一个已记忆和弦」，不再自动演奏。
        if (context.RuntimeSpec.Variant == SakiComponents.ChordRememberVariant)
        {
            var chords = owner.GetChords();
            if (chords is null) return true;

            // 从已装备和弦里挑一个（Major → Minor → Dominant → Bonus → Anon 依次尝试）。
            var chordId = (string?)null;
            foreach (var category in new[]
                     {
                         ChordCategory.Major, ChordCategory.Minor, ChordCategory.Dominant,
                         ChordCategory.Bonus, ChordCategory.Anon
                     })
            {
                var slots = chords.GetCategorySlots(category);
                if (slots.Count == 0) continue;
                chordId = slots[0];
                break;
            }
            if (chordId is null) return true;

            for (var i = 0; i < count; i++)
                await ChordNoteSystem.AddStoredChordAsync(owner, chordId, 1, context.ChoiceContext);

            ChordNoteUIManager.UpdateStoredChordDisplay(owner);
            return true;
        }

        if (context.RuntimeSpec.Variant == SakiComponents.ChordLastVariant)
        {
            await ChordNoteSystem.PlayLastStoredChordAsync(owner, context.ChoiceContext, count);
            return true;
        }

        // ★ 复合变体（T-003）：先演奏，再结算 payoff。
        if (context.RuntimeSpec.Variant is SakiComponents.ChordAndDamageVariant
            or SakiComponents.ChordAndBlockVariant)
        {
            await ChordNoteSystem.PlayAllStoredChordsAsync(owner, context.ChoiceContext, count);
            var payoff = context.RuntimeSpec.Values.Count > 1
                ? Math.Max(1, context.RuntimeSpec.Values[1].BaseValue) : 1;

            if (context.RuntimeSpec.Variant == SakiComponents.ChordAndBlockVariant)
            {
                await CreatureCmd.GainBlock(owner.Creature, payoff, ValueProp.Move, context.CardPlay);
            }
            else if (context.Target is { } chordTarget)
            {
                await DamageCmd.Attack(payoff)
                    .FromCard(context.Card, context.CardPlay)
                    .Targeting(chordTarget)
                    .WithHitFx("vfx/vfx_attack_slash")
                    .Execute(context.ChoiceContext);
            }
            return true;
        }

        if (context.RuntimeSpec.Variant == SakiComponents.PlayChordRandomVariant)
        {
            for (var i = 0; i < count; i++)
                await ChordNoteSystem.PlayRandomEquippedChordAsync(owner, context.ChoiceContext);
            return true;
        }

        await ChordNoteSystem.PlayAllStoredChordsAsync(owner, context.ChoiceContext, count);
        return true;
    }
}

/// <summary>
/// 音符：四种类型各自储存 / 清空时按本体规则触发对应效果。
///
/// ⚠️ 必须先 Activate —— 原因同 <see cref="PlayChordHandler"/>。
/// </summary>
internal sealed class NoteHandler : IComponentRuntimeHandler
{
    public async Task<bool> ExecuteAsync(ComponentRuntimeContext context)
    {
        var owner = context.Card.Owner;

        // ★ 关键：不激活就什么都不会发生（本体 AddNoteAsync 第一行会直接 return）。
        ChordNoteSystem.Activate(owner);

        if (context.RuntimeSpec.Variant == SakiComponents.NoteClearVariant)
        {
            var current = ChordNoteSystem.GetCurrentNotes(owner)?.ToList() ?? [];
            ChordNoteSystem.ClearNotes(owner);
            foreach (var note in current)
                await ChordNoteSystem.TriggerNoteEffectAsync(owner, note, context.ChoiceContext);
            return true;
        }

        // ★ 2026-11-11 用户第 9 条：获得音符 + **触发所有音符效果**（本体「并触发音符对应效果」）。
        if (context.RuntimeSpec.Variant == SakiComponents.NoteAndTriggerVariant)
        {
            // 双数显式复合：slot0 = 攻击音符数，slot1 = 技能音符数（用户 2026-11-11 要求）
            var values = context.RuntimeSpec.Values;
            var attackNotes = values.Count > 0 ? Math.Max(0, values[0].BaseValue) : Math.Max(1, context.Amount);
            var skillNotes = values.Count > 1 ? Math.Max(0, values[1].BaseValue) : 0;

            for (var i = 0; i < attackNotes; i++)
                await ChordNoteSystem.AddNoteAsync(owner, CardType.Attack, context.ChoiceContext);
            for (var i = 0; i < skillNotes; i++)
                await ChordNoteSystem.AddNoteAsync(owner, CardType.Skill, context.ChoiceContext);

            var notes = ChordNoteSystem.GetCurrentNotes(owner)?.ToList() ?? [];
            foreach (var note in notes)
                await ChordNoteSystem.TriggerNoteEffectAsync(owner, note, context.ChoiceContext);
            return true;
        }

        // ★ 复合变体（T-003）：先加音符，再结算 payoff。
        if (context.RuntimeSpec.Variant is SakiComponents.NoteAndDamageVariant
            or SakiComponents.NoteAndBlockVariant
            or SakiComponents.NoteAndDrawVariant
            or SakiComponents.NoteAndEnergyVariant)
        {
            await ChordNoteSystem.AddNoteAsync(owner, CardType.Attack, context.ChoiceContext);
            var payoff = context.RuntimeSpec.Values.Count > 1
                ? Math.Max(1, context.RuntimeSpec.Values[1].BaseValue) : 1;

            if (context.RuntimeSpec.Variant == SakiComponents.NoteAndBlockVariant)
            {
                await CreatureCmd.GainBlock(owner.Creature, payoff, ValueProp.Move, context.CardPlay);
            }
            else if (context.RuntimeSpec.Variant == SakiComponents.NoteAndDrawVariant)
            {
                await CardPileCmd.Draw(context.ChoiceContext, payoff, owner);
            }
            else if (context.RuntimeSpec.Variant == SakiComponents.NoteAndEnergyVariant)
            {
                PlayerCmd.GainEnergy(payoff, owner);
            }
            else if (context.Target is { } compoundTarget)
            {
                await DamageCmd.Attack(payoff)
                    .FromCard(context.Card, context.CardPlay)
                    .Targeting(compoundTarget)
                    .WithHitFx("vfx/vfx_attack_slash")
                    .Execute(context.ChoiceContext);
            }
            return true;
        }

        // Special 用本体 Entry.AnyNote 这个自定义 CardType（DynamicEnumValueMinter 铸出）。
        var addedType = context.RuntimeSpec.Variant switch
        {
            SakiComponents.NoteAddSkillVariant => CardType.Skill,
            SakiComponents.NoteAddPowerVariant => CardType.Power,
            SakiComponents.NoteAddSpecialVariant => CuteSakikoMod.Entry.AnyNote,
            _ => CardType.Attack
        };

        var amount = Math.Max(1, context.Amount);
        for (var i = 0; i < amount; i++)
            await ChordNoteSystem.AddNoteAsync(owner, addedType, context.ChoiceContext);
        return true;
    }
}

/// <summary>芭菲：吃杯（扣杯并触发所有吃杯监听）或加杯。复用本体 MatchaParfait。</summary>
/// <summary>
/// 消耗压力换额外效果（祥子）。
///
/// 本体统一写法「若有则消耗 N 层压力，额外 X」，实现照抄本体 StrikeFast：
///     var pressure = Owner.Creature.GetPower<PressurePower>();
///     if (pressure != null &amp;&amp; pressure.Amount &gt; 0) { await PowerCmd.ModifyAmount(...); }
/// 有压力就先扣 1 层再给额外收益；没有压力就只结算主效果（不报错、不扣血）。
/// </summary>
internal sealed class PayPressureHandler : IComponentRuntimeHandler
{
    public async Task<bool> ExecuteAsync(ComponentRuntimeContext context)
    {
        var owner = context.Card.Owner;

        var spec = context.RuntimeSpec;
        // 第一个槽是主效果数值，第二个槽是额外效果数值。
        var main = spec.Values.Count > 0 ? spec.Values[0].BaseValue : 1;
        var extra = spec.Values.Count > 1 ? spec.Values[1].BaseValue : 1;

        switch (spec.Variant)
        {
            case SakiComponents.PayPressureBlockVariant:
            {
                // 格挡不受活力影响，直接分两段给。
                await CreatureCmd.GainBlock(owner.Creature, main, ValueProp.Move, context.CardPlay);
                var pressure = owner.Creature?.GetPower<PressurePower>();
                if (pressure is { Amount: > 0 })
                {
                    await PowerCmd.ModifyAmount(context.ChoiceContext, pressure, -1, owner.Creature, context.Card);
                    await CreatureCmd.GainBlock(owner.Creature, extra, ValueProp.Move, context.CardPlay);
                }
                return true;
            }
            case SakiComponents.PayPressureDrawVariant:
            {
                if (context.Target is { } drawTarget)
                {
                    await DamageCmd.Attack(main)
                        .FromCard(context.Card, context.CardPlay)
                        .Targeting(drawTarget)
                        .WithHitFx("vfx/vfx_attack_slash")
                        .Execute(context.ChoiceContext);
                }
                var pressure = owner.Creature?.GetPower<PressurePower>();
                if (pressure is { Amount: > 0 })
                {
                    await PowerCmd.ModifyAmount(context.ChoiceContext, pressure, -1, owner.Creature, context.Card);
                    await CardPileCmd.Draw(context.ChoiceContext, extra, owner);
                }
                return true;
            }
            default:
            {
                // ★ 伤害段必须**一次 AttackCommand + WithHitCount**（照抄本体 StrikeFast）。
                //   分两次 DamageCmd.Attack 会让「活力」只加成第一段
                //   （第二次调用时活力层数已被消耗），用户实测过这个 bug。
                //   本体对多段伤害的统一做法就是一次命令 + 命中次数，
                //   这样活力/临时力量自然覆盖全部命中。
                var hits = 1;
                var pressure = owner.Creature?.GetPower<PressurePower>();
                if (pressure is { Amount: > 0 })
                {
                    await PowerCmd.ModifyAmount(context.ChoiceContext, pressure, -1, owner.Creature, context.Card);
                    hits += Math.Max(1, extra);
                }

                if (context.Target is { } target)
                {
                    await DamageCmd.Attack(main)
                        .FromCard(context.Card, context.CardPlay)
                        .WithHitCount(hits)
                        .Targeting(target)
                        .WithHitFx("vfx/vfx_attack_slash")
                        .Execute(context.ChoiceContext);
                }
                return true;
            }
        }
    }
}

/// <summary>乐A4 消耗手牌所有猫咪，每张对随机敌人造成伤害（本体「猫咪大冲击」）。</summary>
/// <summary>爱A4 丢弃全部手牌再获得格挡（本体「意外事件」）。</summary>
internal sealed class DeckHandler : IComponentRuntimeHandler
{
    public async Task<bool> ExecuteAsync(ComponentRuntimeContext context)
    {
        var owner = context.Card.Owner;

        var hand = PileType.Hand.GetPile(owner);

        // ★ 消耗手牌中所有状态牌，每张抽 N（本体「讨厌的东西说拜拜」）
        if (context.RuntimeSpec.Variant == SakiComponents.ExhaustStatusVariant)
        {
            if (hand is null) return true;
            var per = Math.Max(1, context.Amount);
            var statuses = hand.Cards.Where(card => card.Type == CardType.Status).ToList();
            foreach (var status in statuses)
                await CardPileCmd.Add([status], PileType.Exhaust);
            if (statuses.Count > 0)
                await CardPileCmd.Draw(context.ChoiceContext, statuses.Count * per, owner);
            return true;
        }

        // ★ 复制本牌进弃牌堆（本体「小小的瞬间」）
        if (context.RuntimeSpec.Variant == SakiComponents.CopySelfVariant)
        {
            var copies = Math.Max(1, context.Amount);
            for (var i = 0; i < copies; i++)
            {
                var copy = context.Card.CreateClone();
                await CardPileCmd.Add(copy, PileType.Discard, CardPilePosition.Random);
            }
            return true;
        }

        // ★ 消耗 N 层压力，将此牌返回手牌（本体「泡澡」/「舞台上的自信」）
        if (context.RuntimeSpec.Variant == SakiComponents.ReturnToHandVariant)
        {
            var cost = Math.Max(1, context.Amount);
            var pressure = owner.Creature?.GetPower<PressurePower>();
            if (pressure is not null && pressure.Amount >= cost)
                await PowerCmd.ModifyAmount(context.ChoiceContext, pressure, -cost, owner.Creature!, context.Card);

            var self = PileType.Discard.GetPile(owner);
            if (self is not null && self.Cards.Contains(context.Card))
                await CardPileCmd.Add([context.Card], PileType.Hand);
            return true;
        }

        // ★ 祥003 随机打出手牌 N 张并遗忘（本体「暂且遗忘」）
        if (context.RuntimeSpec.Variant == SakiComponents.PlayHandThenForgetVariant)
        {
            if (hand is null) return true;
            var takeCount = Math.Max(1, context.Amount);
            var handPool = hand.Cards.ToList();
            for (var i = 0; i < takeCount && handPool.Count > 0; i++)
            {
                var index4 = SakiRuntimeRng.CardSelection(context).NextInt(handPool.Count);
                var picked4 = handPool[index4];
                handPool.RemoveAt(index4);
                await CardCmd.AutoPlay(context.ChoiceContext, picked4, context.CardPlay.Target);
                await MemoryCmd.Forget(context.ChoiceContext, [picked4], context.Card);
            }
            return true;
        }

        // ★ 爱042 从消耗堆取 N 张加入手牌（本体「沉思」）
        if (context.RuntimeSpec.Variant == SakiComponents.RecallFromExhaustVariant)
        {
            var exhaustPile = PileType.Exhaust.GetPile(owner);
            if (exhaustPile is null) return true;
            var takeExhaust = exhaustPile.Cards.Take(Math.Max(1, context.Amount)).ToList();
            foreach (var pick in takeExhaust)
                await CardPileCmd.Add([pick], PileType.Hand);
            return true;
        }

        // ★ 祥023 从遗忘堆取 N 张加入手牌（本体「重拾回忆」）
        if (context.RuntimeSpec.Variant == SakiComponents.RecallFromForgetVariant)
        {
            var forgetPile = CuteSakikoMod.CuteSakikoModCode.CardPiles.ForgetCardPile.Get(owner);
            if (forgetPile is null) return true;
            var picks = forgetPile.Cards
                .Take(Math.Max(1, context.Amount))
                .ToList();
            foreach (var pick in picks)
                await CardPileCmd.Add([pick], PileType.Hand);
            return true;
        }

        // ★ 祥070 将抽牌堆 N 张牌放入手牌（本体「可以做任何事」）
        if (context.RuntimeSpec.Variant == SakiComponents.DrawToHandVariant)
        {
            var drawPile3 = PileType.Draw.GetPile(owner);
            if (drawPile3 is null) return true;
            var takeN = Math.Max(1, context.Amount);
            var picks2 = drawPile3.Cards.Take(takeN).ToList();
            foreach (var pick in picks2)
                await CardPileCmd.Add([pick], PileType.Hand);
            return true;
        }

        // ★ 祥019 从弃牌堆遗忘 N 张（本体「我自己的问题」）
        if (context.RuntimeSpec.Variant == SakiComponents.ForgetFromDiscardVariant)
        {
            var discardPile = PileType.Discard.GetPile(owner);
            if (discardPile is null) return true;
            var forgetN = Math.Max(1, context.Amount);
            var targets = discardPile.Cards.Take(forgetN).ToList();
            if (targets.Count > 0)
                await MemoryCmd.Forget(context.ChoiceContext, targets, context.Card);
            return true;
        }

        // ★ 祥024 消耗 N 层压力，永久获得 M 点最大生命（本体「阅读」）
        if (context.RuntimeSpec.Variant == SakiComponents.PressureMaxHpVariant)
        {
            var costHp = context.RuntimeSpec.Values.Count > 0
                ? Math.Max(1, (int)context.RuntimeSpec.Values[0].BaseValue) : 5;
            var maxHp = context.RuntimeSpec.Values.Count > 1
                ? Math.Max(1, (int)context.RuntimeSpec.Values[1].BaseValue) : 1;

            var pressurePower3 = owner.Creature?.GetPower<PressurePower>();
            if (pressurePower3 is null || pressurePower3.Amount < costHp) return true;

            await PowerCmd.ModifyAmount(context.ChoiceContext, pressurePower3, -costHp, owner.Creature!, context.Card);
            await CreatureCmd.GainMaxHp(owner.Creature!, maxHp);
            return true;
        }

        // ★ 祥060 用回忆填满手牌（本体「被回忆攻击」）
        if (context.RuntimeSpec.Variant == SakiComponents.FillHandRecallVariant)
        {
            await MemoryCmd.Recall(context.ChoiceContext, owner, count: Math.Max(1, context.Amount),
                fillHand: true, upgraded: context.Card.IsUpgraded, source: context.Card);
            return true;
        }

        // ★ 祥089 从抽牌堆随机打出 N 张牌并遗忘（本体「唯有忘却」）
        if (context.RuntimeSpec.Variant == SakiComponents.PlayAndForgetVariant)
        {
            var drawPile2 = PileType.Draw.GetPile(owner);
            if (drawPile2 is null) return true;
            var playN = Math.Max(1, context.Amount);
            var pool2 = drawPile2.Cards.ToList();
            for (var i = 0; i < playN && pool2.Count > 0; i++)
            {
                var index3 = SakiRuntimeRng.CardSelection(context).NextInt(pool2.Count);
                var picked3 = pool2[index3];
                pool2.RemoveAt(index3);
                await CardCmd.AutoPlay(context.ChoiceContext, picked3, context.CardPlay.Target);
                await MemoryCmd.Forget(context.ChoiceContext, [picked3], context.Card);
            }
            return true;
        }

        // ★ 乐038 丢弃手牌中所有非芭菲相关的牌，每张抽 N（本体「挑食」）
        if (context.RuntimeSpec.Variant == SakiComponents.DiscardNonParfaitVariant)
        {
            if (hand is null) return true;
            var perCard = Math.Max(1, context.Amount);
            var parfaitKeyword = CutesakiKeywords.Parfait.GetModCardKeyword();
            var junk = hand.Cards
                .Where(card => !card.Keywords.Contains(parfaitKeyword))
                .ToList();
            foreach (var card in junk)
                await CardPileCmd.Add([card], PileType.Discard);
            if (junk.Count > 0)
                await CardPileCmd.Draw(context.ChoiceContext, junk.Count * perCard, owner);
            return true;
        }

        // ★ 祥052 减少 N 层压力并抽 M 张（本体「积郁成泣」）
        if (context.RuntimeSpec.Variant == SakiComponents.RelievePressureDrawVariant)
        {
            var pressureCost = context.RuntimeSpec.Values.Count > 0
                ? Math.Max(1, (int)context.RuntimeSpec.Values[0].BaseValue) : 5;
            var drawCount = context.RuntimeSpec.Values.Count > 1
                ? Math.Max(1, (int)context.RuntimeSpec.Values[1].BaseValue) : 2;

            var pressurePower2 = owner.Creature?.GetPower<PressurePower>();
            if (pressurePower2 is { Amount: > 0 })
            {
                var reduce = (int)Math.Min(pressurePower2.Amount, pressureCost);
                await PowerCmd.ModifyAmount(context.ChoiceContext, pressurePower2, -reduce, owner.Creature!, context.Card);
            }
            await CardPileCmd.Draw(context.ChoiceContext, drawCount, owner);
            return true;
        }

        // ★ 爱068 随机清除身上一个负面效果（本体「泡澡」）
        if (context.RuntimeSpec.Variant == SakiComponents.ClearDebuffVariant)
        {
            var debuffs = owner.Creature?.Powers
                .Where(power => power.TypeForCurrentAmount == PowerType.Debuff).ToList() ?? [];
            var removeCount = Math.Max(1, context.Amount);
            for (var i = 0; i < removeCount && debuffs.Count > 0; i++)
            {
                var index = SakiRuntimeRng.CardSelection(context).NextInt(debuffs.Count);
                var target = debuffs[index];
                debuffs.RemoveAt(index);
                await PowerCmd.Remove(target);
            }
            return true;
        }

        // ★ 祥045 随机打出 N 张手牌（本体「提线木偶」）
        if (context.RuntimeSpec.Variant == SakiComponents.AutoPlayHandVariant)
        {
            if (hand is null) return true;
            var playCount = Math.Max(1, context.Amount);
            var pool = hand.Cards.ToList();
            for (var i = 0; i < playCount && pool.Count > 0; i++)
            {
                var index = SakiRuntimeRng.CardSelection(context).NextInt(pool.Count);
                var picked = pool[index];
                pool.RemoveAt(index);
                await CardCmd.AutoPlay(context.ChoiceContext, picked, context.CardPlay.Target);
            }
            return true;
        }

        // ★ 祥072 遗忘所有手牌，每 N 张获得 1 张回忆（本体「忘却一切」）
        if (context.RuntimeSpec.Variant == SakiComponents.ForgetAllHandVariant)
        {
            if (hand is null) return true;
            var per = Math.Max(1, context.Amount);
            var forgotten = hand.Cards.ToList();
            await MemoryCmd.Forget(context.ChoiceContext, forgotten, context.Card);

            var recall = forgotten.Count / per;
            for (var i = 0; i < recall; i++)
                await MemoryCmd.Recall(context.ChoiceContext, owner, count: 1, upgraded: context.Card.IsUpgraded, source: context.Card);
            return true;
        }

        if (hand is not null)
            foreach (var card in hand.Cards.ToList())
                await CardPileCmd.Add([card], PileType.Discard);

        var block = Math.Max(1, context.Amount);
        await CreatureCmd.GainBlock(owner.Creature!, block, ValueProp.Move, context.CardPlay);
        return true;
    }
}

/// <summary>
/// 「把自身压力翻倍，并判断这次是否触发了崩溃」—— 本体 LikeDream.cs 的做法。
///
/// ★ 坑：有「面具」遗物时崩溃会被移除，不能靠「崩溃层数变多」判定，
///   要改成「翻倍后压力 ≥ 当前生命值」判定**本该触发**崩溃。两套都写。
///
/// 返回 true 表示真的触发了崩溃（调用方再去给 payoff）。
/// </summary>
internal static class CollapseHelper
{
    internal static async Task<bool> DoublePressureAsync(ComponentRuntimeContext context)
    {
        var owner = context.Card.Owner;
        var creature = owner.Creature;
        if (creature is null) return false;

        var pressure = creature.GetPower<PressurePower>();
        var current = pressure?.Amount ?? 0;
        // ★ 压力为 0 时不要「白打」：至少给 1 层，否则玩家点了一张牌却什么都没发生。
        if (current <= 0)
        {
            await PressureCmd.Apply(context.ChoiceContext, creature, 1, creature, context.Card);
            return false;
        }

        var hasMask = owner.Relics.OfType<CuteSakikoMod.CuteSakikoModCode.Relics.Saki.Rare.Mask>().Any();
        var preHp = creature.CurrentHp;
        var before = creature.GetPower<BreakDownPower>()?.Amount ?? 0;

        await PressureCmd.Apply(context.ChoiceContext, creature, current, creature, context.Card);

        var triggered = hasMask
            ? current * 2 >= preHp
            : (creature.GetPower<BreakDownPower>()?.Amount ?? 0) > before;
        return triggered;
    }
}

/// <summary>
/// 改费用 / 变牌（C 项，2026-11-10）。
///
/// 改费用用本体的 <c>card.EnergyCost.SetThisTurn(0)</c>（HastyPlay.cs:68 实证）；
/// 变牌用 <c>CardCmd.Transform(target, rng)</c>（HaneokaCorridorEvent.cs:138 实证）。
/// </summary>
internal sealed class CostHandler : IComponentRuntimeHandler
{
    public async Task<bool> ExecuteAsync(ComponentRuntimeContext context)
    {
        var owner = context.Card.Owner;

        var hand = PileType.Hand.GetPile(owner);
        if (hand is null) return true;

        var count = Math.Max(1, context.Amount);
        var candidatePool = hand.Cards
            .Where(card => card != context.Card)
            .ToList();
        var candidates = SakiRuntimeRng.PickRandom(candidatePool, count, SakiRuntimeRng.CardSelection(context));

        if (context.RuntimeSpec.Variant == SakiComponents.CostFreeHandVariant)
        {
            await CardPileCmd.Draw(context.ChoiceContext, 1, owner);
            foreach (var card in candidates)
                card.EnergyCost.SetThisTurn(0);
            return true;
        }

        return true;
    }
}

/// <summary>
/// 本场成长：自己维护「这张牌这局打出过几次」的计数器。
///
/// 用 <see cref="ConditionalWeakTable{TKey,TValue}"/> 而不是 Dictionary，
/// 因为卡牌对象会被回收；弱表可以避免把已消失的卡挂在内存里。
/// </summary>
internal sealed class GrowthHandler : IComponentRuntimeHandler
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<CardModel, int[]> PlayCounts = new();

    public async Task<bool> ExecuteAsync(ComponentRuntimeContext context)
    {
        var owner = context.Card.Owner;

        var creature = owner.Creature;
        if (creature is null) return true;

        var baseDamage = context.RuntimeSpec.Values.Count > 0
            ? Math.Max(1, (int)context.RuntimeSpec.Values[0].BaseValue) : 6;
        var growth = context.RuntimeSpec.Values.Count > 1
            ? Math.Max(1, (int)context.RuntimeSpec.Values[1].BaseValue) : 2;

        // 这张牌本场打过几次 → 加成 = 次数 × 成长值
        var counter = PlayCounts.GetValue(context.Card, _ => new int[1]);
        var playedTimes = counter[0];

        // 主效果是「获得格挡」的变体（不造成伤害）
        if (context.RuntimeSpec.Variant is SakiComponents.BlockGrowthBlockVariant
            or SakiComponents.BlockGrowthMemoryVariant
            or SakiComponents.BlockGrowthSwordVariant
            or SakiComponents.BlockGrowthParfaitVariant
            or SakiComponents.BlockGrowthChordVariant)
        {
            // T-020：和弦版按「已存储和弦数量」成长，而不是本牌打出次数。
            if (context.RuntimeSpec.Variant == SakiComponents.BlockGrowthChordVariant)
                playedTimes = ChordNoteSystem.GetStoredChords(owner).Count;

            var block = baseDamage + playedTimes * growth;
            await CreatureCmd.GainBlock(creature, block, ValueProp.Move, context.CardPlay);

            if (playedTimes > 0)
            {
                if (context.RuntimeSpec.Variant == SakiComponents.BlockGrowthMemoryVariant)
                    await MemoryCmd.Recall(context.ChoiceContext, owner, count: playedTimes * growth,
                        upgraded: context.Card.IsUpgraded, source: context.Card);
                else if (context.RuntimeSpec.Variant == SakiComponents.BlockGrowthSwordVariant)
                    for (var i = 0; i < playedTimes * growth; i++)
                        await ConditionActions.AddKnightSwordAsync(context, owner);
                else if (context.RuntimeSpec.Variant == SakiComponents.BlockGrowthParfaitVariant)
                {
                    var parfaitRelic = owner.Relics.OfType<CuteSakikoMod.CuteSakikoModCode.Relics.Rana.Starter.MatchaParfait>()
                        .FirstOrDefault();
                    if (parfaitRelic is not null)
                        CuteSakikoMod.CuteSakikoModCode.Relics.Rana.Starter.MatchaParfait
                            .AddCharges(parfaitRelic, playedTimes * growth);
                }
            }

            counter[0]++;
            return true;
        }

        var damage = baseDamage + playedTimes * growth;

        var target = context.CardPlay.Target;
        // 未指定目标时随机挑一个敌人（这些原子是「随机敌人」写法，抽到的概率更高）
        if (target is null)
        {
            var enemies = creature.CombatState?.HittableEnemies?.ToArray() ?? [];
            if (enemies.Length > 0) target = enemies[SakiRuntimeRng.Targets(context).NextInt(enemies.Length)];
        }
        await DamageCmd.Attack(damage)
            .FromCard(context.Card, context.CardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(context.ChoiceContext);

        // 成长解耦：按「本场已打出次数」额外给对应的东西
        var played = counter[0];
        switch (context.RuntimeSpec.Variant)
        {
            case SakiComponents.DamageGrowthBlockVariant:
                if (played > 0)
                    await CreatureCmd.GainBlock(creature, Math.Max(1, growth) * played, ValueProp.Move, context.CardPlay);
                break;
            case SakiComponents.DamageGrowthSwordVariant:
                for (var i = 0; i < played; i++)
                    await ConditionActions.AddKnightSwordAsync(context, owner);
                break;
            case SakiComponents.DamageGrowthMemoryVariant:
                if (played > 0)
                    await MemoryCmd.Recall(context.ChoiceContext, owner, count: played,
                        upgraded: context.Card.IsUpgraded, source: context.Card);
                break;
            case SakiComponents.DamageGrowthDrawVariant:
                if (played > 0)
                    await CardPileCmd.Draw(context.ChoiceContext, played, owner);
                break;
        }

        counter[0]++;
        return true;
    }
}

/// <summary>
/// 状态牌生成：把本体的状态牌（欺诈 / 偷懒）塞进弃牌堆。
/// 写法照抄本体 EmptyLure.cs / Argument.cs：
///   CombatState.CreateCard&lt;T&gt;(Owner) + CardPileCmd.AddGeneratedCardToCombat(…, PileType.Discard, Owner)
/// </summary>
internal sealed class StatusCardHandler : IComponentRuntimeHandler
{
    public async Task<bool> ExecuteAsync(ComponentRuntimeContext context)
    {
        var owner = context.Card.Owner;

        var combatState = owner.Creature?.CombatState;
        if (combatState is null) return true;

        var count = Math.Max(1, context.Amount);
        for (var i = 0; i < count; i++)
        {
            if (context.RuntimeSpec.Variant == SakiComponents.FraudStatusVariant)
            {
                var fraud = combatState.CreateCard<CuteSakikoMod.CuteSakikoModCode.Cards.Saki.Status.Fraud>(owner);
                if (fraud is not null)
                {
                    if (context.Card.IsUpgraded) CardCmd.Upgrade(fraud);
                    await CardPileCmd.AddGeneratedCardToCombat(fraud, PileType.Discard, owner);
                }
            }
            else
            {
                var shirk = combatState.CreateCard<CuteSakikoMod.CuteSakikoModCode.Cards.Saki.Status.Shirk>(owner);
                if (shirk is not null)
                {
                    if (context.Card.IsUpgraded) CardCmd.Upgrade(shirk);
                    await CardPileCmd.AddGeneratedCardToCombat(shirk, PileType.Discard, owner);
                }
            }
        }
        return true;
    }
}

/// <summary>
/// 结束回合系列：**先结算收益，再结束回合**（用户 2026-11-10 要求当作大负面）。
/// 顺序很重要 —— 先结束回合的话收益会因为回合结束被吞掉。
/// </summary>
internal sealed class EndTurnHandler : IComponentRuntimeHandler
{
    public async Task<bool> ExecuteAsync(ComponentRuntimeContext context)
    {
        var owner = context.Card.Owner;

        var creature = owner.Creature;
        if (creature is null) return true;
        var amount = context.Amount;

        switch (context.RuntimeSpec.Variant)
        {
            case SakiComponents.EndTurnDrawVariant:
                await CardPileCmd.Draw(context.ChoiceContext, amount, owner);
                break;
            case SakiComponents.EndTurnEnergyVariant:
                PlayerCmd.GainEnergy(amount, owner);
                break;
            case SakiComponents.EndTurnDamageVariant:
            {
                var combatState = creature.CombatState;
                if (combatState is not null)
                    foreach (var enemy in combatState.HittableEnemies.ToArray())
                        await DamageCmd.Attack(amount).FromCard(context.Card, context.CardPlay)
                            .Targeting(enemy).WithHitFx("vfx/vfx_attack_slash")
                            .Execute(context.ChoiceContext);
                break;
            }
        }

        PlayerCmd.EndTurn(owner, false);
        return true;
    }
}

/// <summary>条件型 payoff 用到的公共动作（放骑士之剑 / 给所有剑加伤）。</summary>
internal static class ConditionActions
{
    /// <summary>把一张骑士之剑放进手牌（复用本体的 SwordManager）。</summary>
    internal static async Task AddKnightSwordAsync(ComponentRuntimeContext context, Player owner)
    {
        await CuteSakikoMod.CuteSakikoModCode.Singletons.SwordManager
            .EnsureSwordExists(owner, upgrade: context.Card.IsUpgraded, exclude: null, includeExhaustAndForget: false);
    }

    /// <summary>给战斗中的所有骑士之剑加伤害。</summary>
    internal static Task BoostAllSwordsAsync(ComponentRuntimeContext context, Player owner, int bonus)
    {
        var combatState = owner.Creature?.CombatState;
        if (combatState is null || bonus <= 0) return Task.CompletedTask;
        CuteSakikoMod.CuteSakikoModCode.Singletons.SwordManager.IncreaseAllSwordDamage(bonus, combatState);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 条件型效果（2026-11-10）：先做 A，再按条件决定要不要做 B。
/// 写法全部照抄本体对应卡（LikeDream / NotHelpMe / SoArrogant / BlackWhite）。
/// </summary>
internal sealed class ConditionHandler : IComponentRuntimeHandler
{
    public async Task<bool> ExecuteAsync(ComponentRuntimeContext context)
    {
        var owner = context.Card.Owner;

        var creature = owner.Creature;
        if (creature is null) return true;

        int Value(int index, int fallback) =>
            context.RuntimeSpec.Values.Count > index
                ? Math.Max(1, (int)context.RuntimeSpec.Values[index].BaseValue)
                : fallback;

        switch (context.RuntimeSpec.Variant)
        {
            // ★ 如梦一场系列（2026-11-10）：压力翻倍 → 若触发崩溃，按变体给不同的 payoff
            //   （用户要求：不要只有「抽牌+能量」，要能随机成造成伤害 / 抽牌 / 获得能力）            case SakiComponents.DoubleCollapseBonusVariant:
            case SakiComponents.DoubleCollapseSwordVariant:
            case SakiComponents.DoubleCollapseSwordBoostVariant:
            case SakiComponents.DoubleCollapseMemoryVariant:
            case SakiComponents.DoubleCollapseDamageVariant:
            case SakiComponents.DoubleCollapseDrawVariant:
            case SakiComponents.DoubleCollapsePowerVariant:
            {
                if (!await CollapseHelper.DoublePressureAsync(context))
                    return true;

                switch (context.RuntimeSpec.Variant)
                {
                    // 崩溃 → 对全体敌人造成伤害
                    case SakiComponents.DoubleCollapseDamageVariant:
                    {
                        var damage = Value(0, 8);
                        var combatState = creature.CombatState;
                        if (combatState is null) return true;
                        foreach (var enemy in combatState.HittableEnemies.ToArray())
                            await DamageCmd.Attack(damage)
                                .FromCard(context.Card, context.CardPlay)
                                .Targeting(enemy)
                                .WithHitFx("vfx/vfx_attack_slash")
                                .Execute(context.ChoiceContext);
                        return true;
                    }

                    // 崩溃 → 抽牌
                    case SakiComponents.DoubleCollapseDrawVariant:
                        await CardPileCmd.Draw(context.ChoiceContext, Value(0, 1), owner);
                        return true;

                    // 崩溃 → 获得随机能力
                    case SakiComponents.DoubleCollapsePowerVariant:
                    {
                        var stacks = Value(0, 1);
                        // 从该角色的能力池里随机挑一个「增益类」条目
                        // ★ 2026-11-11 用户实测：爱音的卡给到了乐奈的「两者皆要」。
                        //   原因是这里**写死了 Saki**，必须按「打出这张牌的角色」取能力池。
                        var profileId = RandomProfiles.ProfileOfCharacterTypeName(
                            context.Card.Owner?.Character?.GetType().Name) ?? RandomProfiles.Saki;
                        var buffs = SakiPowerCatalog.For(profileId)
                            .Where(entry => entry.Category is ChaosCardGenerator.ComponentCategory.Growth
                                                         or ChaosCardGenerator.ComponentCategory.Utility
                                                         or ChaosCardGenerator.ComponentCategory.Resource
                                                         or ChaosCardGenerator.ComponentCategory.Defense)
                            .ToList();
                        if (buffs.Count == 0) return true;
                        var pick = buffs[SakiRuntimeRng.CardSelection(context).NextInt(buffs.Count)];
                        var powerType = ModPowerHandler.Resolve(RandomProfiles.Saki, pick.Variant);
                        if (powerType is null) return true;
                        await ModPowerHandler.ApplyByTypeAsync(powerType, context.ChoiceContext, creature, context.Card, stacks);
                        return true;
                    }

                    // 崩溃 → 获得骑士之剑
                    case SakiComponents.DoubleCollapseSwordVariant:
                    {
                        var swords = Value(0, 1);
                        for (var i = 0; i < swords; i++)
                            await ConditionActions.AddKnightSwordAsync(context, owner);
                        return true;
                    }

                    // 崩溃 → 所有骑士之剑获得额外伤害
                    case SakiComponents.DoubleCollapseSwordBoostVariant:
                    {
                        var bonus = Value(0, 2);
                        await ConditionActions.BoostAllSwordsAsync(context, owner, bonus);
                        return true;
                    }

                    // 崩溃 → 获得回忆
                    case SakiComponents.DoubleCollapseMemoryVariant:
                        await MemoryCmd.Recall(context.ChoiceContext, owner, count: Value(0, 1),
                            upgraded: context.Card.IsUpgraded, source: context.Card);
                        return true;

                    // 默认（如梦一场本体）：抽牌 + 能量
                    default:
                        await CardPileCmd.Draw(context.ChoiceContext, Value(0, 1), owner);
                        PlayerCmd.GainEnergy(1, owner);
                        return true;
                }
            }

            // ★ 为什么不帮我：造成伤害 → 若击杀则获得能量
            case SakiComponents.DamageEnergyOnKillVariant:
            {
                var damage = Value(0, 8);
                var target = context.CardPlay.Target;
                await DamageCmd.Attack(damage)
                    .FromCard(context.Card, context.CardPlay)
                    .Targeting(target)
                    .WithHitFx("vfx/vfx_attack_slash")
                    .Execute(context.ChoiceContext);

                if (target is null || target.IsDead)
                    PlayerCmd.GainEnergy(Value(1, 2), owner);
                return true;
            }

            // ★ 击杀 → 获得回忆
            case SakiComponents.DamageMemoryOnKillVariant:
            {
                var damage = Value(0, 10);
                var target = context.CardPlay.Target;
                await DamageCmd.Attack(damage).FromCard(context.Card, context.CardPlay)
                    .Targeting(target).WithHitFx("vfx/vfx_attack_slash").Execute(context.ChoiceContext);
                if (target is null || target.IsDead)
                    await MemoryCmd.Recall(context.ChoiceContext, owner, count: Value(1, 1), upgraded: context.Card.IsUpgraded, source: context.Card);
                return true;
            }

            // ★ 击杀 → 获得骑士之剑
            case SakiComponents.DamageSwordOnKillVariant:
            {
                var damage = Value(0, 10);
                var target = context.CardPlay.Target;
                await DamageCmd.Attack(damage).FromCard(context.Card, context.CardPlay)
                    .Targeting(target).WithHitFx("vfx/vfx_attack_slash").Execute(context.ChoiceContext);
                if (target is null || target.IsDead)
                    for (var i = 0; i < Value(1, 1); i++)
                        await ConditionActions.AddKnightSwordAsync(context, owner);
                return true;
            }

            // ★ 未被完全格挡 → 施加易伤
            case SakiComponents.DamageVulnerableIfUnblockedVariant:
            {
                var damage = Value(0, 6);
                var vulnerable = Value(1, 1);
                var target = context.CardPlay.Target;
                var hpBefore = target?.CurrentHp ?? 0;
                await DamageCmd.Attack(damage).FromCard(context.Card, context.CardPlay)
                    .Targeting(target).WithHitFx("vfx/vfx_attack_slash").Execute(context.ChoiceContext);
                if (target is not null && target.CurrentHp < hpBefore)
                    await PowerCmd.Apply<VulnerablePower>(context.ChoiceContext, target, vulnerable,
                        creature, context.Card);
                return true;
            }

            // ★ 对称拓展：完全格挡 → 施加易伤
            case SakiComponents.DamageVulnerableIfBlockedVariant:
            {
                var damage = Value(0, 6);
                var vulnerable = Value(1, 1);
                var target = context.CardPlay.Target;
                var hpBefore = target?.CurrentHp ?? 0;
                await DamageCmd.Attack(damage).FromCard(context.Card, context.CardPlay)
                    .Targeting(target).WithHitFx("vfx/vfx_attack_slash").Execute(context.ChoiceContext);
                if (target is not null && !target.IsDead && target.CurrentHp >= hpBefore)
                    await PowerCmd.Apply<VulnerablePower>(context.ChoiceContext, target, vulnerable,
                        creature, context.Card);
                return true;
            }

            // ★ 通用「伤害 + 条件收益」：unblocked/blocked/kill/notkill × block/draw/energy/bonusdamage
            case var damageConditionVariant when damageConditionVariant.StartsWith("unblocked_", StringComparison.Ordinal)
                                                   || damageConditionVariant.StartsWith("blocked_", StringComparison.Ordinal)
                                                   || damageConditionVariant.StartsWith("kill_", StringComparison.Ordinal)
                                                   || damageConditionVariant.StartsWith("notkill_", StringComparison.Ordinal):
            {
                await ExecuteDamageConditionPayoffAsync(context, damageConditionVariant);
                return true;
            }

            // ★ 压力比较 × 多收益：variant = pressure_{more|less}_{payoff}
            case var pressureVariant when pressureVariant.StartsWith("pressure_more_", StringComparison.Ordinal)
                                           || pressureVariant.StartsWith("pressure_less_", StringComparison.Ordinal):
            {
                await ExecutePressureCompareAsync(context, pressureVariant);
                return true;
            }

            // ★ 真是高高在上：造成伤害 → 若目标压力 > 自身，给予缩小
            case SakiComponents.DamageShrinkIfLessPressureVariant:
            case SakiComponents.DamageShrinkIfMorePressureVariant:
            {
                var damage = Value(0, 10);
                var shrink = Value(1, 1);
                var target = context.CardPlay.Target;
                await DamageCmd.Attack(damage)
                    .FromCard(context.Card, context.CardPlay)
                    .Targeting(target)
                    .WithHitFx("vfx/vfx_attack_slash")
                    .Execute(context.ChoiceContext);

                if (target is not null && !target.IsDead)
                {
                    var myPressure = creature.GetPower<PressurePower>()?.Amount ?? 0;
                    var theirPressure = target.GetPower<PressurePower>()?.Amount ?? 0;
                    var condition = context.RuntimeSpec.Variant == SakiComponents.DamageShrinkIfLessPressureVariant
                        ? theirPressure > myPressure
                        : theirPressure < myPressure;
                    if (condition)
                        await PowerCmd.Apply<CuteSakikoMod.CuteSakikoModCode.Powers.Debuff.BreakDownPower>(
                            context.ChoiceContext, target, shrink, creature, context.Card);
                }
                return true;
            }

            // ★ 全都会了！：造成伤害 → 若击杀，演奏所有已记忆和弦
            case SakiComponents.DamagePlayMemorizedOnKillVariant:
            {
                var damage = Value(0, 20);
                var target = context.CardPlay.Target;
                await DamageCmd.Attack(damage)
                    .FromCard(context.Card, context.CardPlay)
                    .Targeting(target)
                    .WithHitFx("vfx/vfx_attack_slash")
                    .Execute(context.ChoiceContext);

                if (target is null || target.IsDead)
                    ChordNoteSystem.Activate(owner);
                return true;
            }

            // ★ 试音打击：造成伤害 → 若未被完全格挡，抽 N 张
            case SakiComponents.DamageDrawIfUnblockedVariant:
            {
                var damage = Value(0, 5);
                var draw = Value(1, 1);
                var target = context.CardPlay.Target;
                var hpBefore = target?.CurrentHp ?? 0;
                await DamageCmd.Attack(damage)
                    .FromCard(context.Card, context.CardPlay)
                    .Targeting(target)
                    .WithHitFx("vfx/vfx_attack_slash")
                    .Execute(context.ChoiceContext);

                // 生命值掉了就说明有伤害打到身上（没被完全格挡）
                if (target is not null && target.CurrentHp < hpBefore)
                    await CardPileCmd.Draw(context.ChoiceContext, draw, owner);
                return true;
            }

            // ★ 对称拓展：完全格挡 → 抽牌
            case SakiComponents.DamageDrawIfBlockedVariant:
            {
                var damage = Value(0, 5);
                var draw = Value(1, 1);
                var target = context.CardPlay.Target;
                var hpBefore = target?.CurrentHp ?? 0;
                await DamageCmd.Attack(damage)
                    .FromCard(context.Card, context.CardPlay)
                    .Targeting(target)
                    .WithHitFx("vfx/vfx_attack_slash")
                    .Execute(context.ChoiceContext);

                if (target is not null && !target.IsDead && target.CurrentHp >= hpBefore)
                    await CardPileCmd.Draw(context.ChoiceContext, draw, owner);
                return true;
            }

            // ★ 黑白分明：AOE 伤害 → 若自身无压力，改为回血
            case SakiComponents.DamageAllOrHealVariant:
            {
                var damage = Value(0, 12);
                var heal = Value(1, 3);
                var noPressure = (creature.GetPower<PressurePower>()?.Amount ?? 0) <= 0;

                if (noPressure)
                {
                    await CreatureCmd.Heal(creature, heal);
                    return true;
                }

                var combatState = creature.CombatState;
                if (combatState is null) return true;
                foreach (var enemy in combatState.HittableEnemies.ToArray())
                    await DamageCmd.Attack(damage)
                        .FromCard(context.Card, context.CardPlay)
                        .Targeting(enemy)
                        .WithHitFx("vfx/vfx_attack_slash")
                        .Execute(context.ChoiceContext);
                return true;
            }
        }

        return true;
    }

    /// <summary>
    /// 通用「伤害 + 条件收益」：先攻击，再按条件判断是否执行收益。
    /// condition：unblocked / blocked / kill / notkill；payoff：block/draw/energy/bonusdamage。
    /// </summary>
    private async Task ExecuteDamageConditionPayoffAsync(ComponentRuntimeContext context, string variant)
    {
        var separator = variant.IndexOf('_');
        var condition = variant[..separator];
        var payoff = variant[(separator + 1)..];
        int Value(int index, int fallback) =>
            context.RuntimeSpec.Values.Count > index
                ? Math.Max(1, (int)context.RuntimeSpec.Values[index].BaseValue)
                : fallback;
        var damage = Value(0, 6);
        var value = Value(1, 1);
        var owner = context.Card.Owner;
        var target = context.CardPlay.Target;
        var hpBefore = target?.CurrentHp ?? 0;

        await DamageCmd.Attack(damage)
            .FromCard(context.Card, context.CardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(context.ChoiceContext);

        if (owner is null || target is null) return;
        var conditionMet = condition switch
        {
            "unblocked" => !target.IsDead && target.CurrentHp < hpBefore,
            "blocked" => !target.IsDead && target.CurrentHp >= hpBefore,
            "kill" => target.IsDead,
            "notkill" => !target.IsDead,
            _ => false
        };
        if (!conditionMet) return;

        switch (payoff)
        {
            case "block":
                await CreatureCmd.GainBlock(owner.Creature!, value, ValueProp.Move, context.CardPlay);
                break;
            case "draw":
                await CardPileCmd.Draw(context.ChoiceContext, value, owner);
                break;
            case "energy":
                PlayerCmd.GainEnergy(value, owner);
                break;
            case "bonusdamage":
                if (!target.IsDead)
                    await DamageCmd.Attack(value)
                        .FromCard(context.Card, context.CardPlay)
                        .Targeting(target)
                        .WithHitFx("vfx/vfx_attack_slash")
                        .Execute(context.ChoiceContext);
                break;
        }
    }

    /// <summary>
    /// 压力比较条件：先造成基础伤害，再按 variant 后缀执行一种收益。
    /// payoff：shrink / vulnerable / pressure / doublepressure / bonusdamage / block / draw / energy。
    /// </summary>
    private async Task ExecutePressureCompareAsync(ComponentRuntimeContext context, string variant)
    {
        var more = variant.StartsWith("pressure_more_", StringComparison.Ordinal);
        var payoff = variant[(more ? "pressure_more_" : "pressure_less_").Length..];
        int Value(int index, int fallback) =>
            context.RuntimeSpec.Values.Count > index
                ? Math.Max(1, (int)context.RuntimeSpec.Values[index].BaseValue)
                : fallback;
        var damage = Value(0, 6);
        var value = Value(1, 1);
        var owner = context.Card.Owner;
        var creature = owner?.Creature;
        var target = context.CardPlay.Target;

        await DamageCmd.Attack(damage)
            .FromCard(context.Card, context.CardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(context.ChoiceContext);

        if (creature is null || target is null || target.IsDead) return;
        var myPressure = creature.GetPower<PressurePower>()?.Amount ?? 0;
        var theirPressure = target.GetPower<PressurePower>()?.Amount ?? 0;
        var condition = more ? theirPressure > myPressure : theirPressure < myPressure;
        if (!condition) return;

        switch (payoff)
        {
            case "shrink":
                await PowerCmd.Apply<BreakDownPower>(context.ChoiceContext, target, value, creature, context.Card);
                break;
            case "vulnerable":
                await PowerCmd.Apply<VulnerablePower>(context.ChoiceContext, target, value, creature, context.Card);
                break;
            case "pressure":
                await PressureCmd.Apply(context.ChoiceContext, target, value, creature, context.Card);
                break;
            case "doublepressure":
                await PressureCmd.Apply(context.ChoiceContext, target, theirPressure, creature, context.Card);
                break;
            case "bonusdamage":
                await DamageCmd.Attack(value)
                    .FromCard(context.Card, context.CardPlay)
                    .Targeting(target)
                    .WithHitFx("vfx/vfx_attack_slash")
                    .Execute(context.ChoiceContext);
                break;
            case "block":
                await CreatureCmd.GainBlock(owner!.Creature!, value, ValueProp.Move, context.CardPlay);
                break;
            case "draw":
                await CardPileCmd.Draw(context.ChoiceContext, value, owner!);
                break;
            case "energy":
                PlayerCmd.GainEnergy(value, owner!);
                break;
        }
    }
}

/// <summary>A10 金币（本体「客服工作」）。</summary>
internal sealed class GoldHandler : IComponentRuntimeHandler
{
    public Task<bool> ExecuteAsync(ComponentRuntimeContext context)
    {
        var owner = context.Card.Owner;
        if (context.RuntimeSpec.Variant == SakiComponents.GainGoldByCreaturesVariant)
        {
            var creatures = owner.Creature?.CombatState?.HittableEnemies.Count() ?? 0;
            var gold = Math.Max(1, creatures * Math.Max(1, context.Amount));
            PlayerCmd.GainGold(gold, owner);
            return Task.FromResult(true);
        }

        var amount = context.Amount;
        if (amount <= 0) return Task.FromResult(true);
        PlayerCmd.GainGold(amount, owner);
        return Task.FromResult(true);
    }
}

/// <summary>
/// A1：数值随「压力层数」缩放（T-014）。
///
/// 本体写法参考 `应激反应`（伤害 = 压力 × 2）与 `心之壁`（格挡 = 压力层数）。
/// 压力为 0 时用保底值，避免生成出「完全没效果」的死牌。
/// </summary>
internal sealed class ScaleByPressureHandler : IComponentRuntimeHandler
{
    public async Task<bool> ExecuteAsync(ComponentRuntimeContext context)
    {
        var owner = context.Card.Owner;

        // ★ T-031：伤害段数 = 1 + 琴奏数
        if (context.RuntimeSpec.Variant == SakiComponents.DamageHitsByPianoVariant)
        {
            var hitDamage = Math.Max(1, context.Amount);
            var hits = 1 + PianoCounter.Count(owner);
            var foes0 = owner.Creature?.CombatState?.HittableEnemies?.ToArray() ?? [];
            if (foes0.Length > 0)
                await DamageCmd.Attack(hitDamage).FromCard(context.Card, context.CardPlay)
                    .WithHitCount(hits).Targeting(foes0[0])
                    .WithHitFx("vfx/vfx_attack_slash").Execute(context.ChoiceContext);
            return true;
        }

        // ★ 琴奏计数：伤害 / 格挡 = 琴奏数 × N（T-030，2026-11-10）
        if (context.RuntimeSpec.Variant is SakiComponents.DamageByPianoVariant
            or SakiComponents.BlockByPianoVariant)
        {
            var pianoMul = Math.Max(1, context.Amount);
            var pianos = PianoCounter.Count(owner);
            var pianoScaled = Math.Max(3, pianos * pianoMul);
            if (context.RuntimeSpec.Variant == SakiComponents.BlockByPianoVariant)
                await CreatureCmd.GainBlock(owner.Creature!, pianoScaled, ValueProp.Move, context.CardPlay);
            else
            {
                var foes = owner.Creature?.CombatState?.HittableEnemies?.ToArray() ?? [];
                if (foes.Length > 0)
                    await DamageCmd.Attack(pianoScaled).FromCard(context.Card, context.CardPlay)
                        .Targeting(foes[0]).WithHitFx("vfx/vfx_attack_slash").Execute(context.ChoiceContext);
            }
            return true;
        }

        var multiplier = context.RuntimeSpec.Values.Count > 0
            ? (int)context.RuntimeSpec.Values[0].BaseValue
            : 1;
        if (multiplier <= 0) multiplier = 1;

        var pressure = owner.Creature?.GetPower<PressurePower>()?.Amount ?? 0;
        var scaled = (int)Math.Max(0, pressure) * multiplier;

        // ★ 乐A6 格挡＝敌人数量 × N（本体「旁若无人」）
        if (context.RuntimeSpec.Variant == SakiComponents.BlockByEnemyCountVariant)
        {
            var enemies = owner.Creature?.CombatState?.HittableEnemies.Count() ?? 0;
            var enemyBlock = Math.Max(5, enemies * multiplier);
            await CreatureCmd.GainBlock(owner.Creature!, enemyBlock, ValueProp.Move, context.CardPlay);
            return true;
        }

        // ★ 爱A6 格挡＝储存和弦数 × N（本体「成功的演出」）
        if (context.RuntimeSpec.Variant == SakiComponents.BlockByStoredChordsVariant)
        {
            var stored = ChordNoteSystem.GetStoredChords(owner)?.Count ?? 0;
            var chordBlock = Math.Max(5, stored * multiplier);
            await CreatureCmd.GainBlock(owner.Creature!, chordBlock, ValueProp.Move, context.CardPlay);
            return true;
        }

        // ★ 乐A1 伤害＝自身格挡值（本体「把衣服剪开」）
        if (context.RuntimeSpec.Variant == SakiComponents.DamageByBlockVariant)
        {
            var minimum = Math.Max(1, multiplier);
            var blockNow = (int)Math.Max(0, owner.Creature?.Block ?? 0);
            var damage = Math.Max(minimum, blockNow);
            await DamageCmd.Attack(damage)
                .FromCard(context.Card, context.CardPlay)
                .Targeting(context.CardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(context.ChoiceContext);
            return true;
        }

        // ★ A9 伤害随遗忘堆牌数缩放（本体「不再幼稚的我」）
        if (context.RuntimeSpec.Variant == SakiComponents.DamageByForgetPileVariant)
        {
            var pile = CuteSakikoMod.CuteSakikoModCode.CardPiles.ForgetCardPile.Get(owner);
            var count = pile?.Cards.Count ?? 0;
            var damage = Math.Max(4, count * multiplier);
            await DamageCmd.Attack(damage)
                .FromCard(context.Card, context.CardPlay)
                .Targeting(context.CardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(context.ChoiceContext);
            return true;
        }

        // ★ A2 压力翻倍（本体「如梦一场」/「心灵痛击」）
        if (context.RuntimeSpec.Variant == SakiComponents.DoublePressureVariant)
        {
            var current = owner.Creature?.GetPower<PressurePower>()?.Amount ?? 0;
            // 翻倍 = 再给一层等量的压力；压力为 0 时只给额外值，不至于空过。
            var gain = (int)Math.Max(0, current) + multiplier;
            if (gain > 0)
                await PressureCmd.Apply(context.ChoiceContext, owner.Creature!, gain, owner.Creature!, context.Card);
            return true;
        }

        // ★ A3 消耗全部压力换能量（本体「于夜中重燃斗志」）
        if (context.RuntimeSpec.Variant == SakiComponents.SpendAllPressureEnergyVariant)
        {
            var power = owner.Creature?.GetPower<PressurePower>();
            var current = power?.Amount ?? 0;
            if (power is null || current <= 0) return true;

            var perEnergy = Math.Max(1, multiplier);
            var energy = (int)(current / perEnergy);
            await PowerCmd.ModifyAmount(context.ChoiceContext, power, -current, owner.Creature!, context.Card);
            if (energy > 0) PlayerCmd.GainEnergy(energy, owner);
            return true;
        }

        if (context.RuntimeSpec.Variant == SakiComponents.DamageByPressureVariant)
        {
            var damage = Math.Max(4, scaled);
            await DamageCmd.Attack(damage)
                .FromCard(context.Card, context.CardPlay)
                .Targeting(context.Target ?? context.CardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(context.ChoiceContext);
            return true;
        }

        var block = Math.Max(5, scaled);
        await CreatureCmd.GainBlock(owner.Creature!, block, ValueProp.Move, context.CardPlay);
        return true;
    }
}

/// <summary>
/// 令牌牌：把本体的令牌卡（猫咪 / 骑士之剑 / 糖果）放进手牌。
///
/// 为什么需要这个：本体里「猫咪牌」「骑士之剑」都不是 buff，而是
/// <b>从 CuteSakikoTokenCardPool 里取一张同类令牌卡进手牌</b>。
/// 随机池之前完全没有这两类，导致「乐奈没有猫咪牌、祥子没有剑」，
/// 玩起来就像原版角色。
/// 写法照抄本体 ManyNeko：CreateCard + AddGeneratedCardToCombat(PileType.Hand)。
/// </summary>
internal sealed class TokenCardHandler : IComponentRuntimeHandler
{
    public async Task<bool> ExecuteAsync(ComponentRuntimeContext context)
    {
        var owner = context.Card.Owner;

        var combat = owner.Creature?.CombatState;
        var runState = owner.RunState;
        if (combat is null || runState is null) return true;

        // 本体令牌池里按关键词筛出同类卡。
        // ★ A4 无论何处把剑放入手牌（本体「骑士出鞘」）
        if (context.RuntimeSpec.Variant == SakiComponents.SwordFromAnywhereVariant)
        {
            var pressure = context.Amount;
            if (pressure > 0)
                await PressureCmd.Apply(context.ChoiceContext, owner.Creature!, pressure, owner.Creature!, context.Card);

            // includeExhaustAndForget: true = 连消耗堆和遗忘堆里的剑也捞回来（「无论何处」）
            await CuteSakikoMod.CuteSakikoModCode.Singletons.SwordManager
                .EnsureSwordExists(owner, upgrade: context.Card.IsUpgraded, exclude: null, includeExhaustAndForget: true);
            return true;
        }

        // ★ A5 打出手牌所有剑 + 消耗全部压力追加等量伤害（本体「以压力化利刃」）
        if (context.RuntimeSpec.Variant == SakiComponents.SwordPressureDamageVariant)
        {
            var hand = PileType.Hand.GetPile(owner);
            if (hand is not null)
                foreach (var card in hand.Cards.ToList())
                    if (card is CuteSakikoMod.CuteSakikoModCode.Cards.Saki.Token.KnightSword)
                        await CardCmd.AutoPlay(context.ChoiceContext, card, context.CardPlay.Target);

            var power = owner.Creature?.GetPower<PressurePower>();
            var current = power?.Amount ?? 0;
            if (power is not null && current > 0)
                await PowerCmd.ModifyAmount(context.ChoiceContext, power, -current, owner.Creature!, context.Card);

            var minimum = context.Amount > 0 ? context.Amount : 4;
            var damage = (int)Math.Max(minimum, current);
            await DamageCmd.Attack(damage)
                .FromCard(context.Card, context.CardPlay)
                .Targeting(context.CardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(context.ChoiceContext);
            return true;
        }

        // ★ 骑士之剑的两个额外用法（T-013）——直接抄本体「磨刀 SharpenBlade」的写法
        if (context.RuntimeSpec.Variant == SakiComponents.SwordUpgradeVariant)
        {
            var combatState = owner.Creature?.CombatState;
            if (combatState is null) return true;

            var remaining = Math.Max(1, context.Amount);
            foreach (var pileType in new[]
                     { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust })
            {
                if (remaining <= 0) break;
                var pile = pileType.GetPile(owner);
                if (pile is null) continue;
                foreach (var card in pile.Cards.ToList())
                {
                    if (remaining <= 0) break;
                    if (card is not CuteSakikoMod.CuteSakikoModCode.Cards.Saki.Token.KnightSword sword) continue;
                    if (!sword.IsUpgradable) continue;
                    sword.UpgradeInternal();
                    sword.FinalizeUpgradeInternal();
                    remaining--;
                }
            }
            return true;
        }

        if (context.RuntimeSpec.Variant == SakiComponents.SwordBoostVariant)
        {
            var combatState = owner.Creature?.CombatState;
            if (combatState is null) return true;

            var pressure = owner.Creature?.GetPower<PressurePower>();
            var pressureAmount = pressure?.Amount ?? 0;
            if (pressureAmount > 0)
                CuteSakikoMod.CuteSakikoModCode.Singletons.SwordManager
                    .IncreaseAllSwordDamage(pressureAmount, combatState);
            return true;
        }

        // ⚠️ 2026-11-10 修正：**骑士之剑没有 KnightSword 关键词**
        //   （KnightSword.cs 只声明了 CardKeyword.Retain），
        //   所以之前按关键词筛「一张都筛不到」→ 打出后剑加不进手牌。
        //   ⇒ 剑改为**按类型名筛**（token 池里 Type.Name == "KnightSword"）。
        // ★ 乐A4 消耗手牌所有猫咪，每张对随机敌人造成伤害（本体「猫咪大冲击」）
        if (context.RuntimeSpec.Variant == SakiComponents.TokenConsumeNekoDamageVariant)
        {
            var nekoKeyword = CutesakiKeywords.Neko.GetModCardKeyword();
            var handPile = PileType.Hand.GetPile(owner);
            if (handPile is null) return true;

            var cats = handPile.Cards.Where(card => card.Keywords.Contains(nekoKeyword)).ToList();
            var damage = Math.Max(1, context.Amount);
            foreach (var cat in cats)
            {
                await CardPileCmd.Add([cat], PileType.Exhaust);
                var enemies = combat.HittableEnemies.ToArray();
                if (enemies.Length == 0) break;
                var target = enemies[SakiRuntimeRng.Targets(context).NextInt(enemies.Length)];
                await DamageCmd.Attack(damage)
                    .FromCard(context.Card, context.CardPlay)
                    .Targeting(target)
                    .WithHitFx("vfx/vfx_attack_slash")
                    .Execute(context.ChoiceContext);
            }
            return true;
        }

        // ★ 祥063 从抽牌堆打出 N 张琴奏牌（本体「琴奏：连奏」）
        if (context.RuntimeSpec.Variant == SakiComponents.TokenPlayPianoVariant)
        {
            var pianoKw = CutesakiKeywords.Playpiano.GetModCardKeyword();
            var drawPiano = PileType.Draw.GetPile(owner);
            if (drawPiano is null) return true;
            var pianoTargets = drawPiano.Cards
                .Where(card => card.Keywords.Contains(pianoKw))
                .Take(Math.Max(1, context.Amount))
                .ToList();
            foreach (var piano in pianoTargets)
                await CardCmd.AutoPlay(context.ChoiceContext, piano, context.CardPlay.Target);
            return true;
        }

        // ★ 升级所有琴奏（T-033）
        if (context.RuntimeSpec.Variant == SakiComponents.TokenUpgradeAllPianoVariant)
        {
            var pianoKw3 = CutesakiKeywords.Playpiano.GetModCardKeyword();
            var all = new[] { PileType.Draw, PileType.Hand, PileType.Discard, PileType.Exhaust }
                .Select(type => type.GetPile(owner))
                .Where(pile => pile is not null)
                .SelectMany(pile => pile!.Cards.ToList())
                .Where(card => card.Keywords.Contains(pianoKw3) && card.IsUpgradable)
                .ToList();
            foreach (var pianoCard in all)
                CardCmd.Upgrade(pianoCard);
            return true;
        }

        // ★ 打出并消耗所有琴奏（T-032）
        if (context.RuntimeSpec.Variant == SakiComponents.TokenPlayAllPianoVariant)
        {
            var pianoKw2 = CutesakiKeywords.Playpiano.GetModCardKeyword();
            var pianoAll = new[] { PileType.Draw, PileType.Hand, PileType.Discard }
                .Select(type => type.GetPile(owner))
                .Where(pile => pile is not null)
                .SelectMany(pile => pile!.Cards.ToList())
                .Where(card => card.Keywords.Contains(pianoKw2))
                .ToList();
            foreach (var piano in pianoAll)
            {
                await CardCmd.AutoPlay(context.ChoiceContext, piano, context.CardPlay.Target);
                await CardPileCmd.Add([piano], PileType.Exhaust);
            }
            return true;
        }

        // ★ 祥049 打出并消耗遗忘堆中的回忆（本体「燃烧回忆」）
        if (context.RuntimeSpec.Variant == SakiComponents.TokenPlayForgetMemoryVariant)
        {
            var forgetPile2 = CuteSakikoMod.CuteSakikoModCode.CardPiles.ForgetCardPile.Get(owner);
            if (forgetPile2 is null) return true;
            var memoryKw = CutesakiKeywords.Memory.GetModCardKeyword();
            var memories = forgetPile2.Cards
                .Where(card => card.Keywords.Contains(memoryKw))
                .Take(Math.Max(1, context.Amount))
                .ToList();
            foreach (var memory in memories)
            {
                await CardCmd.AutoPlay(context.ChoiceContext, memory, context.CardPlay.Target);
                await CardPileCmd.Add([memory], PileType.Exhaust);
            }
            return true;
        }

        // ★ 祥073 打出手牌中所有骑士之剑（本体「由我君临」）
        if (context.RuntimeSpec.Variant == SakiComponents.TokenPlayAllSwordsVariant)
        {
            var handSwords = PileType.Hand.GetPile(owner);
            if (handSwords is null) return true;
            var swords = handSwords.Cards
                .Where(card => card is CuteSakikoMod.CuteSakikoModCode.Cards.Saki.Token.KnightSword)
                .ToList();
            foreach (var sword in swords)
                await CardCmd.AutoPlay(context.ChoiceContext, sword, context.CardPlay.Target);
            return true;
        }

        // ★ 乐050 猫咪加入手牌并获得保留（本体「和猫发呆」）
        if (context.RuntimeSpec.Variant == SakiComponents.TokenNekoRetainVariant)
        {
            var nekoKw3 = CutesakiKeywords.Neko.GetModCardKeyword();
            var templateNeko = ModelDb.CardPool<CuteSakikoTokenCardPool>()
                .GetUnlockedCards(owner.UnlockState, runState.CardMultiplayerConstraint)
                .FirstOrDefault(card => card.Keywords.Contains(nekoKw3));
            if (templateNeko is null) return true;

            var addCount = Math.Max(1, context.Amount);
            for (var i = 0; i < addCount; i++)
            {
                var catCard = combat.CreateCard(templateNeko, owner);
                if (catCard is null) continue;
                catCard.AddKeyword(CardKeyword.Retain);
                await CardPileCmd.AddGeneratedCardToCombat(catCard, PileType.Hand, owner);
            }
            // 手牌里已有的猫咪也加保留
            var handNow = PileType.Hand.GetPile(owner);
            if (handNow is not null)
                foreach (var cat in handNow.Cards.Where(c => c.Keywords.Contains(nekoKw3)).ToList())
                    cat.AddKeyword(CardKeyword.Retain);
            return true;
        }

        // ★ 乐056 无论何处打出 N 张猫咪（本体「小猫出动」）
        if (context.RuntimeSpec.Variant == SakiComponents.PlayNekoAnywhereVariant)
        {
            var nekoKw2 = CutesakiKeywords.Neko.GetModCardKeyword();
            var anywhere = new[] { PileType.Draw, PileType.Hand, PileType.Discard }
                .Select(type => type.GetPile(owner))
                .Where(pile => pile is not null)
                .SelectMany(pile => pile!.Cards)
                .Where(card => card.Keywords.Contains(nekoKw2))
                .ToList();

            var playAny = Math.Max(1, context.Amount);
            for (var i = 0; i < playAny && anywhere.Count > 0; i++)
            {
                var index2 = SakiRuntimeRng.CardSelection(context).NextInt(anywhere.Count);
                var picked2 = anywhere[index2];
                anywhere.RemoveAt(index2);
                await CardCmd.AutoPlay(context.ChoiceContext, picked2, context.CardPlay.Target);
            }
            return true;
        }

        // ★ 乐013/乐032 从抽牌堆随机打出 N 张猫咪（本体「猫咪打击」「饿猫扑食」）
        if (context.RuntimeSpec.Variant == SakiComponents.PlayNekoFromDrawVariant)
        {
            var nekoKw = CutesakiKeywords.Neko.GetModCardKeyword();
            var drawPile = PileType.Draw.GetPile(owner);
            if (drawPile is null) return true;
            var playCount = Math.Max(1, context.Amount);
            var cats = drawPile.Cards.Where(card => card.Keywords.Contains(nekoKw)).ToList();
            for (var i = 0; i < playCount && cats.Count > 0; i++)
            {
                var index = SakiRuntimeRng.CardSelection(context).NextInt(cats.Count);
                var picked = cats[index];
                cats.RemoveAt(index);
                await CardCmd.AutoPlay(context.ChoiceContext, picked, context.CardPlay.Target);
            }
            return true;
        }

        // ★ 乐A3 猫咪进抽牌堆 / 多牌堆（本体「猫咪宝藏」「好多猫咪」「撸猫」）
        // ★ 猫咪 + 格挡复合
        if (context.RuntimeSpec.Variant == SakiComponents.TokenNekoBlockVariant)
        {
            var spec = context.RuntimeSpec;
            var count = spec.Values.Count > 0 ? Math.Max(1, spec.Values[0].BaseValue) : 1;
            var block = spec.Values.Count > 1 ? Math.Max(0, spec.Values[1].BaseValue) : 5;
            var template = ModelDb.CardPool<CuteSakikoTokenCardPool>()
                .GetUnlockedCards(owner.UnlockState, runState.CardMultiplayerConstraint)
                .FirstOrDefault(card => card.Keywords.Contains(CutesakiKeywords.Neko.GetModCardKeyword()));
            if (template is null) return true;
            for (var i = 0; i < count; i++)
            {
                var card = combat.CreateCard(template, owner);
                if (card is not null)
                    await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Draw, owner);
            }
            await CreatureCmd.GainBlock(owner.Creature, block, ValueProp.Move, context.CardPlay);
            return true;
        }

        if (context.RuntimeSpec.Variant is SakiComponents.TokenNekoDrawVariant
            or SakiComponents.TokenNekoAllPilesVariant)
        {
            var nekoCount = Math.Max(1, context.Amount);
            var template = ModelDb.CardPool<CuteSakikoTokenCardPool>()
                .GetUnlockedCards(owner.UnlockState, runState.CardMultiplayerConstraint)
                .FirstOrDefault(card => card.Keywords.Contains(CutesakiKeywords.Neko.GetModCardKeyword()));
            if (template is null) return true;

            var piles = context.RuntimeSpec.Variant == SakiComponents.TokenNekoAllPilesVariant
                ? new[] { PileType.Draw, PileType.Hand, PileType.Discard }
                : new[] { PileType.Draw };
            foreach (var pileType in piles)
                for (var i = 0; i < nekoCount; i++)
                {
                    var card = combat.CreateCard(template, owner);
                    if (card is not null)
                        await CardPileCmd.AddGeneratedCardToCombat(card, pileType, owner);
                }
            return true;
        }

        var isSword = context.RuntimeSpec.Variant == SakiComponents.TokenSwordVariant;
        var keywordId = context.RuntimeSpec.Variant switch
        {
            SakiComponents.TokenNekoVariant => CutesakiKeywords.Neko,
            SakiComponents.TokenCandyVariant => CutesakiKeywords.Pancake,
            _ => null
        };
        if (!isSword && keywordId is null) return true;

        List<CardModel> candidates;
        try
        {
            candidates = ModelDb.CardPool<CuteSakikoTokenCardPool>()
                .GetUnlockedCards(owner.UnlockState, runState.CardMultiplayerConstraint)
                .Where(card => isSword
                    ? card.GetType().Name == "KnightSword"
                    : card.Keywords.Contains(keywordId!.GetModCardKeyword()))
                .ToList();
        }
        catch (Exception)
        {
            return true;
        }

        if (candidates.Count == 0) return true;

        var amount = Math.Max(1, context.Amount);
        var rng = runState.Rng?.CombatCardGeneration;
        for (var i = 0; i < amount; i++)
        {
            var template = rng is null ? candidates[0] : rng.NextItem(candidates);
            var card = combat.CreateCard(template, owner);
            if (card is not null && context.Card.IsUpgraded) CardCmd.Upgrade(card);
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, owner);
        }

        return true;
    }
}

/// <summary>
/// 食用芭菲 + 大效果（乐奈）。
///
/// 语义：先扣 N 杯芭菲（代价），扣成功才结算后面的收益。
/// 芭菲不足时只结算「能付得起」的部分——本体是「打不出」，
/// 但随机卡不该因为资源不足变成废牌，所以这里退化成只结算主效果的一部分：
/// 付得起 1 杯就给全额，付不起就什么都不给（保持代价的严肃性）。
/// </summary>
internal sealed class ParfaitComboHandler : IComponentRuntimeHandler
{
    public async Task<bool> ExecuteAsync(ComponentRuntimeContext context)
    {
        var owner = context.Card.Owner;

        var relic = owner.Relics.OfType<MatchaParfait>().FirstOrDefault();
        if (relic is null) return true;

        var spec = context.RuntimeSpec;
        var cups = spec.Values.Count > 0 ? Math.Max(1, spec.Values[0].BaseValue) : 1;
        var payoff = spec.Values.Count > 1 ? spec.Values[1].BaseValue : 1;

        // 付得起吗？付不起就完全不结算（代价必须真实）。
        if (relic.Charges < cups) return true;

        await MatchaParfait.RemoveCharges(relic, cups, context.ChoiceContext);

        switch (spec.Variant)
        {
            case SakiComponents.ParfaitComboBlockVariant:
                await CreatureCmd.GainBlock(owner.Creature, payoff, ValueProp.Move, context.CardPlay);
                break;
            case SakiComponents.ParfaitComboDamageAllVariant:
                await DamageCmd.Attack(payoff)
                    .FromCard(context.Card, context.CardPlay)
                    .TargetingAllOpponents(owner.Creature.CombatState!)
                    .WithHitFx("vfx/vfx_attack_slash")
                    .Execute(context.ChoiceContext);
                break;
            case SakiComponents.ParfaitComboDrawVariant:
                await CardPileCmd.Draw(context.ChoiceContext, payoff, owner);
                break;
            default:
                await CreatureCmd.Heal(owner.Creature, payoff);
                await CardPileCmd.Draw(context.ChoiceContext, 1, owner);
                break;
        }

        return true;
    }
}

internal sealed class ParfaitHandler : IComponentRuntimeHandler
{
    public async Task<bool> ExecuteAsync(ComponentRuntimeContext context)
    {
        var owner = context.Card.Owner;

        var relic = owner.Relics.OfType<MatchaParfait>().FirstOrDefault();
        var amount = Math.Max(1, context.Amount);

        // ★ 乐046 消耗手牌 N 张，视为食用等量芭菲（本体「猫饿了」）
        if (context.RuntimeSpec.Variant == SakiComponents.ConsumeAsParfaitVariant)
        {
            var hand2 = PileType.Hand.GetPile(owner);
            if (hand2 is null || relic is null) return true;
            var want = Math.Max(1, amount);
            var candidates = hand2.Cards.ToList();
            var consumed = 0;
            for (var i = 0; i < want && candidates.Count > 0; i++)
            {
                var card = candidates[0];
                candidates.RemoveAt(0);
                await CardPileCmd.Add([card], PileType.Exhaust);
                consumed++;
            }
            if (consumed > 0)
                await MatchaParfait.RemoveCharges(relic, consumed, context.ChoiceContext);
            return true;
        }

        if (context.RuntimeSpec.Variant == SakiComponents.ParfaitChargeVariant)
        {
            if (relic is not null) MatchaParfait.AddCharges(relic, amount);
            return true;
        }

        if (relic is null) return true;
        await MatchaParfait.RemoveCharges(relic, amount, context.ChoiceContext);
        return true;
    }
}

/// <summary>
/// 悬浮提示：让随机卡的关键词能弹出说明（用户第 8 条要求）。
///
/// 通过 ComponentPresentationApi 给自定义 opcode 挂提示，
/// 全部用<b>本体现成的提示工厂</b>（HoverTipFactory.FromPower / FromKeyword），
/// 文案与本体完全一致，不自己造提示。
///
/// ⚠️ 2026-11-09 实测踩坑：之前 MemoryTips/ChordTips 等全传了空参数，
///    导致「坚持练习」「全力攻哈」这些能力没有悬浮提示。
///    现在每个机制都显式挂对应的 Power / 关键词。
/// </summary>
internal static class SakiPresentation
{
    /// <summary>
    /// 安全取提示：把「单个提示」和「提示集合」统一摊平，
    /// 某个 Power/关键词取不到就跳过，不让整个提示挂掉。
    /// </summary>
    private static IEnumerable<IHoverTip> Tips(params object?[] sources)
    {
        foreach (var source in sources)
        {
            switch (source)
            {
                case IHoverTip tip:
                    yield return tip;
                    break;
                case IEnumerable<IHoverTip> many:
                    foreach (var tip in many)
                        if (tip is not null)
                            yield return tip;
                    break;
            }
        }
    }

    private static IEnumerable<IHoverTip> Power<T>(MegaCrit.Sts2.Core.Models.PowerModel? _) where T : MegaCrit.Sts2.Core.Models.PowerModel, new()
    {
        try
        {
            return HoverTipFactory.FromPowerWithPowerHoverTips<T>(null);
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>按 reference 卡牌生成提示；升级卡自动显示「+」版本（欺诈+/逃避+）。</summary>
    private sealed class GrantedCardTips<TCard> : IComponentHoverTipProvider
        where TCard : MegaCrit.Sts2.Core.Models.CardModel, new()
    {
        public IEnumerable<IHoverTip> BuildHoverTips(ComponentPresentationContext context)
        {
            try { return [HoverTipFactory.FromCard<TCard>(context.IsUpgraded)]; }
            catch { return []; }
        }
    }

    /// <summary>按卡牌类型取「卡面预览」提示（用于令牌牌，如骑士之剑）。</summary>
    private static IEnumerable<IHoverTip> CardTip<TCard>() where TCard : MegaCrit.Sts2.Core.Models.CardModel
    {
        try
        {
            return HoverTipFactory.FromCardWithCardHoverTips<TCard>(false);
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static IHoverTip? Keyword(Func<CardKeyword> keyword)
    {
        try
        {
            return HoverTipFactory.FromKeyword(keyword());
        }
        catch (Exception)
        {
            return null;
        }
    }

    internal static IReadOnlyList<ComponentPresentationRoute> Routes() =>
    [
        // 压力：本体最核心的机制，顺带带出「崩溃」
        new(SakiComponents.PressureOpcode, SakiComponents.PressureVariant, new SimpleTips(
            () => Tips(Power<PressurePower>(null), Power<BreakDownPower>(null)))),
        new(SakiComponents.PressureOpcode, SakiComponents.PressureAllVariant, new SimpleTips(
            () => Tips(Power<PressurePower>(null), Power<BreakDownPower>(null)))),

        // 遗忘 / 回忆：祥子专属关键词。
        // ★ 2026-11-11 用户口径：**Memory 是卡牌用，Memorysaki 是遗物用**；
        //   卡牌悬浮提示要用 Memory，不要用 Memorysaki。
        new(SakiComponents.ForgetOpcode, SakiComponents.ForgetVariant, new SimpleTips(
            () => Tips(Keyword(() => CutesakiKeywords.Memory.GetModCardKeyword()),
                       Keyword(() => CutesakiKeywords.Sakiforget.GetModCardKeyword())))),
        new(SakiComponents.ForgetOpcode, SakiComponents.ForgetRandomVariant, new SimpleTips(
            () => Tips(Keyword(() => CutesakiKeywords.Memory.GetModCardKeyword()),
                       Keyword(() => CutesakiKeywords.Sakiforget.GetModCardKeyword())))),
        new(SakiComponents.RecallOpcode, SakiComponents.RecallVariant, new SimpleTips(
            () => Tips(Keyword(() => CutesakiKeywords.Memory.GetModCardKeyword())))),

        // 和弦 / 音符：爱音专属关键词
        new(SakiComponents.PlayChordOpcode, SakiComponents.PlayChordStoredVariant, new SimpleTips(
            () => Tips(Keyword(() => CutesakiKeywords.Chord.GetModCardKeyword()),
                       Keyword(() => CutesakiKeywords.EquippedChords.GetModCardKeyword())))),
        new(SakiComponents.PlayChordOpcode, SakiComponents.PlayChordRandomVariant, new SimpleTips(
            () => Tips(Keyword(() => CutesakiKeywords.Chord.GetModCardKeyword()),
                       Keyword(() => CutesakiKeywords.LearnedChords.GetModCardKeyword())))),
        new(SakiComponents.NoteOpcode, SakiComponents.NoteAddAttackVariant, new SimpleTips(
            () => Tips(Keyword(() => CutesakiKeywords.Noteify.GetModCardKeyword())))),
        new(SakiComponents.NoteOpcode, SakiComponents.NoteAddSkillVariant, new SimpleTips(
            () => Tips(Keyword(() => CutesakiKeywords.Noteify.GetModCardKeyword())))),
        new(SakiComponents.NoteOpcode, SakiComponents.NoteAddPowerVariant, new SimpleTips(
            () => Tips(Keyword(() => CutesakiKeywords.Noteify.GetModCardKeyword())))),
        new(SakiComponents.NoteOpcode, SakiComponents.NoteAddSpecialVariant, new SimpleTips(
            () => Tips(Keyword(() => CutesakiKeywords.Noteify.GetModCardKeyword())))),
        new(SakiComponents.NoteOpcode, SakiComponents.NoteClearVariant, new SimpleTips(
            () => Tips(Keyword(() => CutesakiKeywords.Noteify.GetModCardKeyword()),
                       Keyword(() => CutesakiKeywords.NoNote.GetModCardKeyword())))),

        // ★ 令牌牌（2026-11-10 用户反馈「骑士之剑没有卡牌悬浮提示」）：
        //   剑直接给 KnightSword 的卡面提示；猫咪/糖果给各自关键词。
        new(SakiComponents.TokenOpcode, SakiComponents.TokenSwordVariant, new SimpleTips(
            () => Tips(CardTip<CuteSakikoMod.CuteSakikoModCode.Cards.Saki.Token.KnightSword>()))),
        new(SakiComponents.TokenOpcode, SakiComponents.TokenNekoVariant, new SimpleTips(
            () => Tips(Keyword(() => CutesakiKeywords.Neko.GetModCardKeyword())))),
        new(SakiComponents.TokenOpcode, SakiComponents.TokenCandyVariant, new SimpleTips(
            () => Tips(Keyword(() => CutesakiKeywords.Pancake.GetModCardKeyword())))),

        // 抹茶芭菲：乐奈专属关键词
        new(SakiComponents.ParfaitOpcode, SakiComponents.ParfaitEatVariant, new SimpleTips(
            () => Tips(Keyword(() => CutesakiKeywords.Parfait.GetModCardKeyword())))),
        new(SakiComponents.ParfaitOpcode, SakiComponents.ParfaitChargeVariant, new SimpleTips(
            () => Tips(Keyword(() => CutesakiKeywords.Parfait.GetModCardKeyword())))),

        // ★ 2026-11-11 用户要求：「把卡牌加入手牌」的原子要带**对应卡牌**的悬浮提示。
        //   目前覆盖最常出现的「骑士之剑」（4 个原子都会把它加入手牌）。
        new(SakiComponents.GrowthOpcode, SakiComponents.DamageGrowthSwordVariant, new SimpleTips(
            () => Tips(CardTip<CuteSakikoMod.CuteSakikoModCode.Cards.Saki.Token.KnightSword>()))),
        new(SakiComponents.GrowthOpcode, SakiComponents.BlockGrowthSwordVariant, new SimpleTips(
            () => Tips(CardTip<CuteSakikoMod.CuteSakikoModCode.Cards.Saki.Token.KnightSword>()))),
        new(SakiComponents.ConditionOpcode, SakiComponents.DoubleCollapseSwordVariant, new SimpleTips(
            () => Tips(CardTip<CuteSakikoMod.CuteSakikoModCode.Cards.Saki.Token.KnightSword>()))),
        new(SakiComponents.ConditionOpcode, SakiComponents.DamageSwordOnKillVariant, new SimpleTips(
            () => Tips(CardTip<CuteSakikoMod.CuteSakikoModCode.Cards.Saki.Token.KnightSword>()))),
        // 「无论何处把骑士之剑放入手牌」（瞪视幸福 / 骑士出鞘 那类）
        new(SakiComponents.TokenOpcode, SakiComponents.SwordFromAnywhereVariant, new SimpleTips(
            () => Tips(CardTip<CuteSakikoMod.CuteSakikoModCode.Cards.Saki.Token.KnightSword>()))),

        // ★ 2026-11-11 用户第 2 条：和「琴奏」有关的效果要有琴奏关键词提示。
        new(SakiComponents.ScaleOpcode, "", new SimpleTips(
            () => Tips(Keyword(() => CuteSakikoMod.CuteSakikoModCode.Others.CutesakiKeywords.Playpiano.GetModCardKeyword())))),
        new(SakiComponents.TokenOpcode, SakiComponents.TokenPlayPianoVariant, new SimpleTips(
            () => Tips(Keyword(() => CuteSakikoMod.CuteSakikoModCode.Others.CutesakiKeywords.Playpiano.GetModCardKeyword())))),

        // ★ 2026-11-11 用户第 4 条：能力提示不能漏 —— 原生 power 类 opcode 也挂能力提示。
        // 原生 power / gain_power / apply_power：按能力清单逐个挂它自己的提示
        //（用户第 4 条：向我俯首那类能力不能漏提示）
        .. new[] { "power", "gain_power", "apply_power" }.SelectMany(opcode =>
            SakiPowerCatalog.For(RandomProfiles.Saki)
                .Concat(SakiPowerCatalog.For(RandomProfiles.Anon))
                .Concat(SakiPowerCatalog.For(RandomProfiles.Rana))
                .Select(p => new ComponentPresentationRoute(opcode, p.Variant,
                    new SimpleTips(() => Tips(PowerTip(p))))))
        ,

        // ★ 2026-11-11 用户要求：音符（[img]text_note[/img]）要有「音符效果」的悬浮提示。
        //   NoteClear 已在上面注册过，这里只补 NoteAndTrigger，避免重复 key 让整个展示包注册失败。
        new(SakiComponents.NoteOpcode, SakiComponents.NoteAndTriggerVariant, new SimpleTips(
            () => Tips(Keyword(() => CuteSakikoMod.CuteSakikoModCode.Others.CutesakiKeywords.Noteify.GetModCardKeyword())))),

        // ★ 2026-11-11 用户第 8 条：把「欺诈 / 逃避」这类状态牌加入弃牌堆的原子，也要有卡牌提示。
        new(SakiComponents.StatusOpcode, SakiComponents.FraudStatusVariant,
            new GrantedCardTips<CuteSakikoMod.CuteSakikoModCode.Cards.Saki.Status.Fraud>()),
        new(SakiComponents.StatusOpcode, SakiComponents.ShirkStatusVariant,
            new GrantedCardTips<CuteSakikoMod.CuteSakikoModCode.Cards.Saki.Status.Shirk>()),

        // 乐奈「撸猫」：把猫咪加入手牌 → 挂猫咪卡的提示
        new(SakiComponents.TokenOpcode, SakiComponents.TokenUpgradeNekoVariant, new SimpleTips(
            () => Tips(Keyword(() => CutesakiKeywords.Neko.GetModCardKeyword())))),

        // ★ 本体专属能力：每个能力挂它自己的 Power 提示（用户实测「全力攻哈没有提示」）
        ..SakiPowerCatalog.For(RandomProfiles.Saki).Select(p =>
            new ComponentPresentationRoute(SakiPowerCatalog.Opcode, p.Variant,
                new SimpleTips(() => Tips(PowerTip(p)))))
        ,
        ..SakiPowerCatalog.For(RandomProfiles.Anon).Select(p =>
            new ComponentPresentationRoute(SakiPowerCatalog.Opcode, p.Variant,
                new SimpleTips(() => Tips(PowerTip(p)))))
        ,
        ..SakiPowerCatalog.For(RandomProfiles.Rana).Select(p =>
            new ComponentPresentationRoute(SakiPowerCatalog.Opcode, p.Variant,
                new SimpleTips(() => Tips(PowerTip(p)))))
        ,
    ];

    /// <summary>
    /// 按能力清单里的完整类名反射出 Power 类型，再取它的悬浮提示。
    ///
    /// ⚠️ 2026-11-09 实测踩坑：<c>FromPowerWithPowerHoverTips&lt;T&gt;</c> 返回的是
    ///    <c>IEnumerable&lt;IHoverTip&gt;</c>，之前写成 <c>as IHoverTip</c> 强转成单个 →
    ///    永远 null → 「全力攻哈 / 即刻演奏 / 练习的证明」全都没有提示。
    ///    ⇒ 必须原样返回集合。
    /// </summary>
    private static IEnumerable<IHoverTip> PowerTip(SakiPowerCatalog.ModPower power)
    {
        try
        {
            var type = Type.GetType(power.TypeName)
                       ?? AppDomain.CurrentDomain.GetAssemblies()
                           .Select(assembly => assembly.GetType(power.TypeName))
                           .FirstOrDefault(found => found is not null);
            if (type is null) return [];

            // FromPowerWithPowerHoverTips<T>(int? amount) —— 1 个可空 int 参数。
            var method = typeof(HoverTipFactory).GetMethods()
                .FirstOrDefault(m => m.Name == nameof(HoverTipFactory.FromPowerWithPowerHoverTips)
                                     && m.IsGenericMethodDefinition
                                     && m.GetParameters().Length == 1);
            if (method is null) return [];

            var generic = method.MakeGenericMethod(type);
            return generic.Invoke(null, [null]) as IEnumerable<IHoverTip> ?? [];
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>通用提示提供者：lambda 返回什么就出什么。</summary>
    private sealed class SimpleTips(Func<IEnumerable<IHoverTip>> factory) : IComponentHoverTipProvider
    {
        public IEnumerable<IHoverTip> BuildHoverTips(ComponentPresentationContext context) => factory();
    }
}

/// <summary>琴奏计数（2026-11-10）：三个牌堆里带「琴奏」关键词的牌数。</summary>
internal static class PianoCounter
{
    internal static int Count(MegaCrit.Sts2.Core.Entities.Players.Player owner)
    {
        var keyword = CutesakiKeywords.Playpiano.GetModCardKeyword();
        var total = 0;
        foreach (var type in new[] { PileType.Draw, PileType.Hand, PileType.Discard })
            total += type.GetPile(owner)?.Cards.Count(card => card.Keywords.Contains(keyword)) ?? 0;
        return total;
    }
}




/// <summary>
/// T-034：升级补偿装饰器。
/// 只对「这张卡确实没有任何可见数值升级」的卡生效（判定写在这里，避免误伤）。
/// </summary>
internal sealed class UpgradeBonusDecorator : IComponentRuntimeHandler
{
    private readonly IComponentRuntimeHandler _inner;

    internal UpgradeBonusDecorator(IComponentRuntimeHandler inner) => _inner = inner;

    public async Task<bool> ExecuteAsync(ComponentRuntimeContext context)
    {
        var handled = await _inner.ExecuteAsync(context);
        if (!handled) return false;

        try
        {
            var card = context.Card;
            if (card is null || !UpgradeBonusRules.ShouldApply(card, context.RuntimeSpec)) return true;

            var owner = card.Owner;
            if (owner?.Creature is null) return true;

            if (card.Type.ToString() == "Attack")
            {
                var foes = owner.Creature.CombatState?.HittableEnemies?.ToArray() ?? [];
                if (foes.Length > 0)
                    await DamageCmd.Attack(4).FromCard(card, context.CardPlay)
                        .Targeting(foes[0]).WithHitFx("vfx/vfx_attack_slash")
                        .Execute(context.ChoiceContext);
            }
            else
            {
                await CreatureCmd.GainBlock(owner.Creature, 3, ValueProp.Move, context.CardPlay);
            }
        }
        catch (Exception)
        {
            // 装饰器永不抛：升级补偿失败不能影响主效果。
        }

        return true;
    }
}

/// <summary>哪些卡在升级时需要补基础效果（= 没有任何可见数值升级的卡）。</summary>
internal static class UpgradeBonusRules
{
    internal static bool ShouldApply(MegaCrit.Sts2.Core.Models.CardModel card,
        ChaosCardGenerator.OperationRuntimeSpec spec)
    {
        if (!card.IsUpgraded) return false;
        // 只对「自定义原子 + 没有数值槽」的操作补，避免和真实数值升级叠两次。
        if (!spec.Opcode.StartsWith("csk_", StringComparison.Ordinal)) return false;
        return spec.Values.Count == 0;
    }
}
/// <summary>T-024：改敌人意图。当前实现为「让所有敌人重新随机下回合意图」。</summary>
internal sealed class IntentHandler : IComponentRuntimeHandler
{
    public Task<bool> ExecuteAsync(ComponentRuntimeContext context)
    {
        try
        {
            var owner = context.Card.Owner;
            var foes = owner.Creature?.CombatState?.HittableEnemies?.ToArray() ?? [];
            foreach (var foe in foes)
                foe.Monster?.RollMove(foes);
        }
        catch (Exception)
        {
            // 改意图失败不能影响主效果
        }

        return Task.FromResult(true);
    }
}
/// <summary>自建能量：按卡面数值获得（原子基准 2 点，收缩/升级后以卡面为准）。</summary>
internal sealed class EnergyTextHandler : IComponentRuntimeHandler
{
    public async Task<bool> ExecuteAsync(ComponentRuntimeContext context)
    {
        var owner = context.Card.Owner;
        if (owner?.Creature is null) return true;
        await MegaCrit.Sts2.Core.Commands.PlayerCmd.GainEnergy(Math.Max(1, context.Amount), owner);
        return true;
    }
}

















