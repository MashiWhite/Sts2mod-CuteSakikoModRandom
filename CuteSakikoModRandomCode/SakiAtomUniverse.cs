using ChaosCardGenerator;
using MegaCrit.Sts2.Core.Logging;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 三个角色**各自的**组件全集：生成器只从本角色这一份里抽样。
///
/// ★ 为什么要按角色分开（2026-11-09 用户要求「角色机制不要串卡池」）：
///   之前所有角色共用一份组件库，导致爱音会抽到「芭菲」、乐奈会抽到「回忆」，
///   角色特色全串了。现在每个角色只拿自己的机制 + 通用效果。
///
/// 分工：
///   祥子 → 压力 / 遗忘 / 回忆（本体 PressureCmd / MemoryCmd）
///   爱音 → 四种音符 / 和弦演奏（本体 ChordNoteSystem）
///   乐奈 → 芭菲吃/加（本体 MatchaParfait）
///   通用 → 伤害 / 格挡 / 抽牌 / 能量 / 增益 / 减益 / 触发
///
/// ⚠️ AA 要求每个 atom 有非空且 profile 内唯一的 SemanticId，这里统一编号。
/// ⚠️ AA 要求「每种卡牌类型都能独立成立的正面组件」（攻击=伤害、技能=格挡、
///    能力=持续增益），否则兜底生成会失败。
/// </summary>
internal static class SakiAtomUniverse
{
    /// <summary>机制原子相对纯原版原子的权重（越大越像本角色）。</summary>
    private const int MechanicWeight = 3;
    /// <summary>
    /// 组件 **recipes**（保留重复）——同时是「出率权重」和「可选清单」的唯一来源。
    ///
    /// ★★ 浓度由「atom 在目录里重复几次」决定（§3.17 / §3.21）：
    ///   AA 的 AtomCounts = 某 SchemaKey 在 Recipes 里出现了几次，
    ///   所以想让某机制常见，就把同一个 atom 多写几遍。
    ///
    /// ★★ 每个副本必须有自己的 SemanticId（非空 + profile 内唯一），
    ///   否则 AA 的 validator 直接拒收整个包（§3.19）。
    /// </summary>
    internal static IReadOnlyList<ComponentAtom> RecipeAtoms(string profileId)
    {
        var atoms = new List<ComponentAtom>();
        atoms.AddRange(CommonAtoms());
        atoms.AddRange(CharacterAtoms(profileId));

        // ★ 2026-11-11 用户口径：音符是爱音独有机制，祥子/乐奈的**原子池**里就不能有。
        //   在注册给 AA 之前直接过滤，而不是生成后再剥卡。
        if (profileId != RandomProfiles.Anon)
        {
            var before = atoms.Count;
            atoms.RemoveAll(atom =>
                atom.RuntimeSpec?.Opcode == SakiComponents.NoteOpcode
                || atom.Template.Contains("note", StringComparison.OrdinalIgnoreCase));
            var removed = before - atoms.Count;
            if (removed > 0)
                Log.Info($"[原子池] {profileId} 移除非本角色音符原子 {removed} 个");
        }

        // ★ 2026-11-12 用户口径：回复生命的 VP 不降，但**抽到的概率要降**。
        //   AA 的出率来自 AtomCounts：同一个 atom.Key 在目录里出现几次，就有几倍权重。
        //   这里只保留每种回血原子的一份（去重），不会改数值，也不会完全删掉回血。
        static bool IsHealAtom(ComponentAtom atom) =>
            atom.Template.Contains("Heal", StringComparison.OrdinalIgnoreCase)
            || atom.ChineseText.Contains("回复", StringComparison.Ordinal);

        var beforeHeal = atoms.Count(IsHealAtom);
        var seenHeal = new HashSet<string>(StringComparer.Ordinal);
        atoms = atoms
            .Where(atom => !IsHealAtom(atom) || seenHeal.Add(atom.Key))
            .ToList();
        var afterHeal = atoms.Count(IsHealAtom);
        if (beforeHeal != afterHeal)
            Log.Info($"[原子池] {profileId} 回复生命原子去重 {beforeHeal}→{afterHeal}（降低出现概率，不改 VP）");

        var prefix = ShortName(profileId);
        return atoms
            .Select((atom, index) => atom with { SemanticId = $"csk/{prefix}/{index:D3}" })
            .ToArray();
    }
    /// <summary>供其它文件（如触发配方编号）使用的短名。</summary>
    internal static string ShortNameFor(string profileId) => ShortName(profileId);

    private static string ShortName(string profileId) => profileId switch
    {
        RandomProfiles.Saki => "saki",
        RandomProfiles.Anon => "anon",
        RandomProfiles.Rana => "rana",
        _ => "unknown"
    };

    private static IEnumerable<ComponentAtom> CharacterAtoms(string profileId) => profileId switch
    {
        RandomProfiles.Saki => SakiOnly(),
        RandomProfiles.Anon => AnonOnly().Concat(Repeat(new[] { SakiAtomLibrary.RerollEnemyIntent() }, 1)),
        RandomProfiles.Rana => RanaOnly(),
        _ => [],
    };

    // 通用效果（三角色共用）
    //
    // ★★ 分布按**本体卡池实测占比**配（用户反馈「为什么那么多回复生命和获得力量，像战士」）：
    //    逐卡统计三池（Saki 76 / Anon 62 / Rana 63 张可玩牌）：
    //      伤害 ≈ 40%  格挡 ≈ 24%  抽牌 ≈ 16%  能量 ≈ 11%
    //      减益 ≈ 3%   力量 ≈ 3%   治疗 ≈ 2%   敏捷 ≈ 2%
    //    ⇒ 治疗/力量/敏捷各只留 1 个组件，压低权重。
    // ─────────────────────────────────────────────────────────────
    private static IEnumerable<ComponentAtom> CommonAtoms()
    {
        // 伤害（最高占比）
        yield return SakiAtomLibrary.Damage(6);
        yield return SakiAtomLibrary.Damage(8);        yield return SakiAtomLibrary.Damage(10);
        yield return SakiAtomLibrary.Damage(20);
        yield return SakiAtomLibrary.Damage(14);        yield return SakiAtomLibrary.DamageAll(12);
        yield return SakiAtomLibrary.DamageAll(5);
        yield return SakiAtomLibrary.DamageAll(8);        yield return SakiAtomLibrary.DamageRandom(11);
        yield return SakiAtomLibrary.DamageRandom(7);
        // 格挡
        yield return SakiAtomLibrary.Block(5);        yield return SakiAtomLibrary.Block(16);
        yield return SakiAtomLibrary.Block(12);
        // 抽牌 / 能量
        yield return SakiAtomLibrary.Draw(1);        yield return SakiAtomLibrary.Draw(2);
        yield return SakiAtomLibrary.Draw(3);        yield return SakiAtomLibrary.EnergyGainTwo();

        // 减益
        yield return SakiAtomLibrary.ApplyWeak(1);
        yield return SakiAtomLibrary.ApplyVulnerable(1);

        // 结束你的回合（大负面 + 收益，2026-11-10 用户要求）
        yield return SakiAtomLibrary.EndTurnDraw(2);
        yield return SakiAtomLibrary.EndTurnDamage(12);

        // 改费用（寄生版，2026-11-10）
        yield return SakiAtomLibrary.FreeHandCost(1);
        yield return SakiAtomLibrary.TransformHand(1);

        // 本场成长（主效果 + 解耦成长，2026-11-10）
        yield return SakiAtomLibrary.DamageGrowthBlock(6, 2);
        yield return SakiAtomLibrary.DamageGrowthBlock(9, 3);

        yield return SakiAtomLibrary.DamageGrowthDraw(8, 1);
        yield return SakiAtomLibrary.BlockGrowthBlock(5, 2);

        // 本场成长（复合原子，2026-11-10 拆 C 项）
        yield return SakiAtomLibrary.DamageGrowth(6, 2);
        yield return SakiAtomLibrary.DamageGrowth(8, 3);

        // ★ 2026-11-11 用户口径：欺诈/逃避是**小祥专属的 token 状态牌**，不能进共用池
        //   （否则爱音/乐奈也会「获得欺诈/逃避」）。已移到 SakiOnly()。


        // 延迟结算：下回合获得能量 / 抽牌（2026-11-10 拆 C 项）
        yield return SakiAtomLibrary.NextTurnDraw(2);

        // 力量 / 敏捷（本体极少，各 1 个）
        yield return SakiAtomLibrary.Strength(1);
        yield return SakiAtomLibrary.Dexterity(1);

        // 手牌操作
        yield return SakiAtomLibrary.Discard(1);
        yield return SakiAtomLibrary.ExhaustHand(1);

        // 触发器与能力牌基础
        yield return SakiAtomLibrary.TriggerTurnStart();
        yield return SakiAtomLibrary.TriggerTurnEnd();
        yield return SakiAtomLibrary.PowerFoundation(2);
    }
    /// <summary>
    /// 祥子：压力 / 消耗压力 / 遗忘回忆 / 骑士之剑 + 本体专属能力。
    ///
    /// ★ 机制重复多遍不是凑数——AA 的出率来自 AtomCounts
    ///   （同一 atom.Key 在目录里出现几次），重复 = 提高该机制出现概率（§3.17 / §3.21）。
    /// </summary>
    private static IEnumerable<ComponentAtom> SakiOnly()
    {
        // 小祥专属 token 状态牌：欺诈 / 逃避（2026-11-11 用户要求只归小祥）
        yield return SakiAtomLibrary.AddFraud(1);
        yield return SakiAtomLibrary.AddShirk(1);
        // ★ 骑士之剑 / 回忆 是祥子专属机制 → 成长方向也必须限定给她，
        //   否则会出现「爱音的卡额外获得骑士之剑」这种串池（2026-11-10 实测）。
        yield return SakiAtomLibrary.DamageGrowthMemory(7, 1);
        yield return SakiAtomLibrary.BlockGrowthMemory(5, 1);
        // 档位多了（18 个），每个 ×2 保持总权重和原来 10×3 接近。
        foreach (var atom in Repeat(PressureAtoms(), 2)) yield return atom;

        // ★ 条件型效果（2026-11-10）：只给祥子。
        //   「压力翻倍；若因此触发崩溃 → 抽牌 + 能量」= 本体「如梦一场」。
        //   ⚠️ 之前误插到 NekoAtoms，导致乐奈池出现祥子的压力机制（已修）。
        foreach (var atom in Repeat(new[] { SakiAtomLibrary.DoubleCollapseMemory(1),
                                            SakiAtomLibrary.DamageMemoryOnKill(10, 1),
                                            SakiAtomLibrary.DamageVulnerableIfUnblocked(6, 2),
                                            SakiAtomLibrary.DamageVulnerableIfBlocked(6, 2),
                                            SakiAtomLibrary.DoubleCollapseDamage(10),
                                            SakiAtomLibrary.DoubleCollapseDraw(2),
        SakiAtomLibrary.DoubleCollapseBonus(2),
                                            SakiAtomLibrary.DoubleCollapsePower(1),
                                            SakiAtomLibrary.DamageShrinkIfLessPressure(10, 1),
                                            SakiAtomLibrary.DamageShrinkIfMorePressure(10, 1),
                                            SakiAtomLibrary.DamageAllOrHeal(12, 3) }, 4)) yield return atom;
        // 2026-11-12 对称拓展加权：完全格挡变体需要独立重复，否则会被原有 9 个原子稀释到抽不到。
        foreach (var atom in Repeat(new[] { SakiAtomLibrary.DamageShrinkIfMorePressure(10, 1),
                                            SakiAtomLibrary.DamageVulnerableIfBlocked(6, 2) }, 3))
            yield return atom;

        // ★ 2026-11-12 用户要求：压力比较的收益也要复合。
        //   同一套「目标压力大于/小于自身」条件，分别接易伤/压力/翻倍/额外伤害/格挡/抽牌/能量。
        foreach (var more in new[] { true, false })
        {
            foreach (var atom in Repeat(new[]
                     {
                         SakiAtomLibrary.PressureCompare(6, 1, "vulnerable", more),
                         SakiAtomLibrary.PressureCompare(6, 2, "pressure", more),
                         SakiAtomLibrary.PressureCompare(6, 1, "doublepressure", more),
                         SakiAtomLibrary.PressureCompare(4, 6, "bonusdamage", more),
                         SakiAtomLibrary.PressureCompare(6, 5, "block", more),
                         SakiAtomLibrary.PressureCompare(6, 1, "draw", more),
                         SakiAtomLibrary.PressureCompare(6, 1, "energy", more)
                     }, 4))
                yield return atom;
        }

        // ★ 2026-11-12：其他触发条件也按「条件 × 收益」复合。
        foreach (var condition in new[] { "unblocked", "blocked" })
        {
            foreach (var atom in Repeat(new[]
                     {
                         SakiAtomLibrary.DamageConditionPayoff(6, 5, condition, "block"),
                         SakiAtomLibrary.DamageConditionPayoff(6, 1, condition, "energy"),
                         SakiAtomLibrary.DamageConditionPayoff(4, 6, condition, "bonusdamage")
                     }, 8))
                yield return atom;
        }

        foreach (var condition in new[] { "kill", "notkill" })
        {
            foreach (var atom in Repeat(new[]
                     {
                         SakiAtomLibrary.DamageConditionPayoff(6, 1, condition, "draw"),
                         SakiAtomLibrary.DamageConditionPayoff(6, 5, condition, "block"),
                         SakiAtomLibrary.DamageConditionPayoff(6, 1, condition, "energy")
                     }, 8))
                yield return atom;
        }
        // 祥003 打出手牌并遗忘 / 爱042 消耗堆取牌（2026-11-10）
        foreach (var atom in Repeat(new[] { SakiAtomLibrary.PlayHandThenForget(1),
                                            SakiAtomLibrary.RecallFromExhaust(1) }, 3)) yield return atom;
        // 祥031 消耗压力获回忆（2026-11-10）
        foreach (var atom in Repeat(new[] { SakiAtomLibrary.PayPressureRecall(1),
                                            SakiAtomLibrary.PayPressureRecall(2) }, 4)) yield return atom;
        // 祥023/070/019（2026-11-10）：遗忘堆取牌 / 抽牌堆入手 / 弃牌堆遗忘
        foreach (var atom in Repeat(new[] { SakiAtomLibrary.RecallFromForget(1),
                                            SakiAtomLibrary.RecallFromForget(2),
                                            SakiAtomLibrary.DrawToHand(1),
                                            SakiAtomLibrary.ForgetFromDiscard(1) }, 3)) yield return atom;
        // 祥024 消耗压力换最大生命（2026-11-10）
        foreach (var atom in Repeat(new[] { SakiAtomLibrary.PressureMaxHp(5, 1),
                                            SakiAtomLibrary.PressureMaxHp(5, 2) }, 4)) yield return atom;
        // 祥060 回忆填满手牌 / 祥089 打出并遗忘（2026-11-10）
        foreach (var atom in Repeat(new[] { SakiAtomLibrary.FillHandRecall(1),
                                            SakiAtomLibrary.PlayAndForget(1) }, 4)) yield return atom;
        // 祥052 减少压力抽牌（2026-11-10）
        foreach (var atom in Repeat(new[] { SakiAtomLibrary.RelievePressureDraw(5, 2),
                                            SakiAtomLibrary.RelievePressureDraw(5, 3) }, 5)) yield return atom;
        // 祥045 随机打出手牌 / 祥072 遗忘所有手牌换回忆（2026-11-10）
        foreach (var atom in Repeat(new[] { SakiAtomLibrary.AutoPlayHand(1), SakiAtomLibrary.AutoPlayHand(2),
                                            SakiAtomLibrary.ForgetAllHand(1) }, 4)) yield return atom;
        // 祥048/075（2026-11-10）：消耗压力把这张牌返回手牌
        foreach (var atom in Repeat(new[] { SakiAtomLibrary.ReturnToHand(5) }, 5)) yield return atom;
        foreach (var atom in Repeat(PressureScaleAtoms(), 14)) yield return atom;
        foreach (var atom in Repeat(PressureScaleBlockAtoms(), 6)) yield return atom;
        foreach (var atom in Repeat(PressureSpendAtoms(), 2)) yield return atom;
        foreach (var atom in Repeat(PileAndGoldAtoms(), 6)) yield return atom;
        foreach (var atom in Repeat(new[] { SakiAtomLibrary.PayPressureWeakAll(1, 2), SakiAtomLibrary.PayPressureWeakAll(1, 3), SakiAtomLibrary.PayPressureWeakAll(2, 2), SakiAtomLibrary.PayPressureWeakAll(2, 3), SakiAtomLibrary.PayPressureWeakAll(3, 2), SakiAtomLibrary.PayPressureWeakAll(3, 3) }, 3)) yield return atom;
        foreach (var atom in Repeat(SwordPressureDamageAtoms(), 10)) yield return atom;
        foreach (var atom in Repeat(PayPressureAtoms(), 3)) yield return atom;
        foreach (var atom in Repeat(MemoryAtoms(), 2)) yield return atom;
        foreach (var atom in Repeat(SwordAtoms(), 5)) yield return atom;

        // ★ 2026-11-11 用户口径：意图重掷是**爱音专属**，祥子/乐奈池子里不能再出现。

        foreach (var atom in SakiPowerCatalog.For(RandomProfiles.Saki).Select(p => SakiPowerCatalog.Atom(p, 1)))
            yield return atom;
    }

    private static IEnumerable<ComponentAtom> PressureAtoms()
    {
        // ★ 2026-11-13 用户口径：「获得压力 / 给予压力的数值不能永远是 1，要随机」。
        //   我们的数值多样化靠**不同档位的固定原子**（AA 采样会把槽改成 sampled，
        //   卡面 {Amount0} 就没人填，2026-11-10 实机踩过）。
        //
        // ★★ 2026-11-14 用户口径：「给予和获得压力的数值太少了，对比本体」。
        //   本体实测（PowerVar<PressurePower> 基数）：
        //     给敌人：Glare 10 / GoWork 6 / NoWork 15 / AbsoluteAuthority 25 /
        //             Cafe 20 / AsahiBeer 15 / Obsession(全体) 3 / FeatherBirds(全体+自身) 8
        //     给自己：DefendSaki 1 / Work 2 / PianoStrike 1 / HeartSelf 6 / SakiSweep 5 /
        //             Unsheathe 5 / DontCareOthers 每敌 2
        //   ⇒ 随机池按同一档位重排：给敌人 3~15，全体 3~8，自己 4~8；
        //   并把数值上限从 6（旧 NumericCapFor 默认值）抬到 20（见 SakiValuePolicy /
        //   NumericCapFor），否则 8/10/12/15 这些档位会被夹回个位数。
        yield return SakiAtomLibrary.Pressure(3);
        yield return SakiAtomLibrary.Pressure(4);
        yield return SakiAtomLibrary.Pressure(6);
        yield return SakiAtomLibrary.Pressure(8);
        yield return SakiAtomLibrary.Pressure(10);
        yield return SakiAtomLibrary.Pressure(12);
        yield return SakiAtomLibrary.Pressure(15);
        yield return SakiAtomLibrary.PressureAll(3);
        yield return SakiAtomLibrary.PressureAll(4);
        yield return SakiAtomLibrary.PressureAll(6);
        yield return SakiAtomLibrary.PressureAll(8);
        yield return SakiAtomLibrary.PressureSelf(4);
        yield return SakiAtomLibrary.PressureSelf(6);
        yield return SakiAtomLibrary.PressureSelf(8);

        // ★ 2026-11-14：本体三张「伤害 + 目标压力」卡的联动效果（之前完全没拆）。
        //   心灵痛击（8 伤 + 目标压力翻倍）、需要练习（9 伤 + 一半压力平摊）、
        //   生气了（8 伤 + 每 8 层压力换 1 次额外伤害）。
        foreach (var atom in Repeat(new[]
                 {
                     SakiAtomLibrary.DamageDoubleTargetPressure(8),
                     SakiAtomLibrary.DamageSpreadTargetPressure(9),
                     SakiAtomLibrary.DamageConsumeTargetPressureHits(8, 8),
                     SakiAtomLibrary.DamageConsumeTargetPressureHits(6, 5)
                 }, 2))
            yield return atom;
    }

    private static IEnumerable<ComponentAtom> PayPressureAtoms()
    {
        yield return SakiAtomLibrary.PayPressureDamage(6);
        yield return SakiAtomLibrary.PayPressureDamage(8);
        yield return SakiAtomLibrary.PayPressureDamage(10);
        yield return SakiAtomLibrary.PayPressureBlock(5, 3);
        yield return SakiAtomLibrary.PayPressureBlock(7, 3);
        yield return SakiAtomLibrary.PayPressureBlock(9, 3);
        yield return SakiAtomLibrary.PayPressureDraw(6, 1);
        yield return SakiAtomLibrary.PayPressureDraw(8, 1);
    }

    private static IEnumerable<ComponentAtom> MemoryAtoms()
    {
        yield return SakiAtomLibrary.Forget(1);
        yield return SakiAtomLibrary.Forget(2);
        yield return SakiAtomLibrary.ForgetRandom(1);
        yield return SakiAtomLibrary.Recall(1);
        yield return SakiAtomLibrary.Recall(2);
        // ★ 2026-11-14（本体「背负回忆」）：随机让一张回忆获得重放 1~2。
        //   直接 yield（不再套 Repeat），权重与 Recall 同档。
        yield return SakiAtomLibrary.MemoryReplay(1);
        yield return SakiAtomLibrary.MemoryReplay(2);
    }

    /// <summary>A1 数值随压力缩放（T-014）。祥子 90 张里有 51 张涉及压力，这是主轴联动。</summary>
    private static IEnumerable<ComponentAtom> PressureScaleAtoms()
    {
        yield return SakiAtomLibrary.DamageByPressure(1);
        yield return SakiAtomLibrary.DamageByPressure(2);
        yield return SakiAtomLibrary.DamageByPressure(3);
    }

    /// <summary>A9/A10 遗忘堆缩放 / 金币（2026-11-10）。</summary>
    private static IEnumerable<ComponentAtom> PileAndGoldAtoms()
    {
        yield return SakiAtomLibrary.DamageByForgetPile(1);
        yield return SakiAtomLibrary.DamageByForgetPile(2);
        yield return SakiAtomLibrary.GainGold(10);
        yield return SakiAtomLibrary.GainGold(20);
        yield return SakiAtomLibrary.GainGold(30);
    }

    /// <summary>A2/A3 压力翻倍 / 消耗全部压力换能量（2026-11-10）。</summary>
    private static IEnumerable<ComponentAtom> PressureSpendAtoms()
    {
        yield return SakiAtomLibrary.DoublePressure(1);
        yield return SakiAtomLibrary.DoublePressure(2);
        // 带数字的版本（每3/4层换1能量）会让 AA 生成 {AmountN:diff()} 占位符而露字，
        // 统一改用无槽版（1 层 = 1 能量），语义不变、卡面干净（2026-11-10 实机「空利防御」）。
    }

    /// <summary>
    /// A5 打剑 + 压力转化伤害（2026-11-10）。
    /// ⚠️ 伤害侧的自定义 opcode 结构性稀少（§3.55），必须**单独给高权重**，
    /// 混在别的列表里几乎抽不到。
    /// </summary>
    private static IEnumerable<ComponentAtom> SwordPressureDamageAtoms()
    {
        yield return SakiAtomLibrary.SwordPressureDamage(6);
        yield return SakiAtomLibrary.SwordPressureDamage(10);
        yield return SakiAtomLibrary.SwordPressureDamage(14);
    }

    /// <summary>
    /// 压力缩放·格挡侧。
    /// ⚠️ 必须与伤害侧**分开给权重**：实测把两者放同一个列表时，
    /// 格挡侧出现 53 次而伤害侧只有 6 次（防御预算远比伤害预算容易满足）。
    /// </summary>
    private static IEnumerable<ComponentAtom> PressureScaleBlockAtoms()
    {
        yield return SakiAtomLibrary.BlockByPressure(1);
        yield return SakiAtomLibrary.BlockByPressure(2);
    }

    private static IEnumerable<ComponentAtom> SwordAtoms()
    {
        // A4（2026-11-10）：无论何处捞剑

        // T-024：改敌人意图

        // 琴奏计数 / 打出所有琴奏（T-030~T-032，2026-11-10）
        yield return SakiAtomLibrary.DamageByPiano(2);
        yield return SakiAtomLibrary.DamageHitsByPiano(4);
        yield return SakiAtomLibrary.BlockByPiano(2);
        yield return SakiAtomLibrary.PlayAllPiano();
        yield return SakiAtomLibrary.UpgradeAllPiano();

        // 祥049/063（2026-11-10）：遗忘堆回忆 / 抽牌堆琴奏
        yield return SakiAtomLibrary.PlayForgetMemory(1);
        yield return SakiAtomLibrary.PlayPianoFromDraw(1);
        // 祥073 打出手牌中所有剑（2026-11-10）
        yield return SakiAtomLibrary.PlayAllSwords(0);
        // T-013：升级全部剑 / 按压力给剑加伤（本体「磨刀」的两个用法）
        yield return SakiAtomLibrary.UpgradeSwords(1);
        yield return SakiAtomLibrary.UpgradeSwords(2);
        yield return SakiAtomLibrary.BoostSwordsByPressure(2);
        yield return SakiAtomLibrary.BoostSwordsByPressure(3);
    }

    /// <summary>
    /// 爱音：音符 / 清空 / 演奏和弦 + 本体专属能力。
    /// 本体主轴是「攒音符 → 演奏和弦」，这两块的权重必须最厚，否则玩起来像原版。
    /// </summary>
    private static IEnumerable<ComponentAtom> AnonOnly()
    {        // ★ 复合同子（T-003）：setup + payoff 写在同一句，不会被 AA 当纯 setup 换掉。
        foreach (var atom in Repeat(CompoundChordAtoms(), 16)) yield return atom;
        // ★ 2026-11-14（聪明阿侬 / 即兴演奏随机侧等价）：学习和弦 → 演奏已学会和弦。
        foreach (var atom in Repeat(new[] { SakiAtomLibrary.LearnRandomChords(1),
                                            SakiAtomLibrary.LearnRandomChords(2),
                                            SakiAtomLibrary.PlayLearnedChords(2),
                                            SakiAtomLibrary.PlayLearnedChords(3) }, 2))
            yield return atom;
        // ★ 2026-11-14 爱音专属和弦原子（解锁 + 演奏）。
        //   本体系里这 7 个和弦只能靠牌组里的和弦卡解锁，随机池没有和弦卡 → 原子自己解锁。
        yield return SakiAtomLibrary.ExclusiveChord(SakiComponents.ChordExclusiveAnonCVariant, "爱音C和弦", "Anon C Chord");
        yield return SakiAtomLibrary.ExclusiveChord(SakiComponents.ChordExclusiveAnonDVariant, "爱音D和弦", "Anon D Chord");
        yield return SakiAtomLibrary.ExclusiveChord(SakiComponents.ChordExclusiveAnonEVariant, "爱音E和弦", "Anon E Chord");
        yield return SakiAtomLibrary.ExclusiveChord(SakiComponents.ChordExclusiveAnonFVariant, "爱音F和弦", "Anon F Chord");
        yield return SakiAtomLibrary.ExclusiveChord(SakiComponents.ChordExclusiveAnonGVariant, "爱音G和弦", "Anon G Chord");
        yield return SakiAtomLibrary.ExclusiveChord(SakiComponents.ChordExclusiveGreyVariant, "灰爱音和弦", "Grey Anon Chord");
        yield return SakiAtomLibrary.ExclusiveChord(SakiComponents.ChordExclusiveHekitenVariant, "碧天伴走", "Hekitenbansou");
        // ★ 2026-11-14 只解锁不演奏版本（解锁后留给 play_learned / stored）。
        yield return SakiAtomLibrary.ExclusiveChordUnlock(SakiComponents.ChordUnlockAnonCVariant, "爱音C和弦", "Anon C Chord");
        yield return SakiAtomLibrary.ExclusiveChordUnlock(SakiComponents.ChordUnlockAnonDVariant, "爱音D和弦", "Anon D Chord");
        yield return SakiAtomLibrary.ExclusiveChordUnlock(SakiComponents.ChordUnlockAnonEVariant, "爱音E和弦", "Anon E Chord");
        yield return SakiAtomLibrary.ExclusiveChordUnlock(SakiComponents.ChordUnlockAnonFVariant, "爱音F和弦", "Anon F Chord");
        yield return SakiAtomLibrary.ExclusiveChordUnlock(SakiComponents.ChordUnlockAnonGVariant, "爱音G和弦", "Anon G Chord");
        yield return SakiAtomLibrary.ExclusiveChordUnlock(SakiComponents.ChordUnlockGreyVariant, "灰爱音和弦", "Grey Anon Chord");
        yield return SakiAtomLibrary.ExclusiveChordUnlock(SakiComponents.ChordUnlockHekitenVariant, "碧天伴走", "Hekitenbansou");
        // ★ 2026-11-14（连接之锚 / 扫弦）：选择型，复用本体选择界面。
        foreach (var atom in Repeat(new[] { SakiAtomLibrary.SelectAndPlayChords(2),
                                            SakiAtomLibrary.StrumNotes(3) }, 2))
            yield return atom;
        foreach (var atom in Repeat(StoredChordAtoms(), 10)) yield return atom;
        // ★ 2026-11-14（本体「消消气」）：使目标永久失去 2/3 点力量。
        foreach (var atom in Repeat(new[] { SakiAtomLibrary.LoseStrengthAll(1),
                                            SakiAtomLibrary.LoseStrengthAll(2) }, 2))
            yield return atom;
        foreach (var atom in Repeat(new[] { SakiAtomLibrary.LoseStrength(2),
                                            SakiAtomLibrary.LoseStrength(3) }, 2))
            yield return atom;

        // ★ 2026-11-13：和弦槽位（本体「团队的小太阳」机制）重新进池。
        //   T-016 曾判定它进不了生成，所以以前寄生在 remember 上；现在 remember 拆成
        //   「随机储存」+「随机槽位」两个独立原子，用独立模板重试一次。
        foreach (var atom in Repeat(ChordSlotAtoms(), 4)) yield return atom;
        // 爱A5 演奏 2~3 次（2026-11-10）：给低估值控制强度，不要和 1 次同价
        foreach (var atom in Repeat(new[] { SakiAtomLibrary.PlayStoredChordsMulti(2),
                                            SakiAtomLibrary.PlayStoredChordsMulti(3) }, 4)) yield return atom;
        // 爱A3 金币＝生物数×N
        foreach (var atom in Repeat(new[] { SakiAtomLibrary.GainGoldByCreatures(3),
                                            SakiAtomLibrary.GainGoldByCreatures(5) }, 5)) yield return atom;
        // ⚠️ 撤出池（2026-11-10，T-016）：该 variant 无论怎么调都进不了生成结果。
        //   下一轮改用「寄生在已可用的 remember 变体上」的方案，见 T-016。
        // 爱068 随机清除负面效果（2026-11-10）
        foreach (var atom in Repeat(new[] { SakiAtomLibrary.ClearDebuff(1) }, 5)) yield return atom;
        // 爱024 击杀→演奏已记忆和弦（2026-11-10）
        foreach (var atom in Repeat(new[] { SakiAtomLibrary.DamagePlayMemorizedOnKill(20), SakiAtomLibrary.DamagePlayMemorizedOnKill(14) }, 6)) yield return atom;
        // 爱002/爱049 随机音符（2026-11-10）
        // ★ 2026-11-11 用户要求（第 9 条）：单独「获得音符」权重压低，尽量复合出现。
        foreach (var atom in Repeat(new[] { SakiAtomLibrary.RandomNotes(1),
                                            SakiAtomLibrary.RandomNotes(2) }, 2)) yield return atom;
        // 获得音符 + 触发所有音符效果（本体「并触发音符对应效果」）
        foreach (var atom in Repeat(new[] { SakiAtomLibrary.AddNoteAndTrigger(1, 1),
                                            SakiAtomLibrary.AddNoteAndTrigger(1, 2),
                                            SakiAtomLibrary.AddNoteAndTrigger(2, 1) }, 4)) yield return atom;
        foreach (var atom in Repeat(new[] { SakiAtomLibrary.ExhaustStatusDraw(1),
                                            SakiAtomLibrary.CopySelf(1) }, 5)) yield return atom;

        // 爱A2 清除音符换格挡 / 抽牌（2026-11-10）
        foreach (var atom in Repeat(new[] { SakiAtomLibrary.ClearScaleBlock(4), SakiAtomLibrary.ClearScaleBlock(5),
                                            SakiAtomLibrary.ClearScaleDraw(1) }, 6)) yield return atom;
        // 爱A6 格挡＝储存和弦数 × N（2026-11-10）
        foreach (var atom in Repeat(new[] { SakiAtomLibrary.BlockByStoredChords(3),
                                            SakiAtomLibrary.BlockByStoredChords(4) }, 6)) yield return atom;

        // 爱A4 丢弃全部手牌换格挡（2026-11-10，本体「意外事件」）
        foreach (var atom in Repeat(new[] { SakiAtomLibrary.DiscardAllHand(6), SakiAtomLibrary.DiscardAllHand(9) }, 5))
            yield return atom;
        foreach (var atom in Repeat(CompoundNoteAtoms(), 4)) yield return atom;
        foreach (var atom in Repeat(NoteAtoms(), 1)) yield return atom;
        foreach (var atom in Repeat(ChordAtoms(), 10)) yield return atom;
        foreach (var atom in SakiPowerCatalog.For(RandomProfiles.Anon).Select(p => SakiPowerCatalog.Atom(p, 1)))
            yield return atom;
    }

    private static IEnumerable<ComponentAtom> NoteAtoms()
    {
        // ★ 2026-11-11 用户第 5 条：纯「获得N音符」是 0 作用牌，
        //   这里全部换成「获得音符 + 伤害/格挡」的复合原子。
        yield return SakiAtomLibrary.AddNoteAndDamage(1, NoteKind.Attack, 6);
        yield return SakiAtomLibrary.AddNoteAndDamage(1, NoteKind.Skill, 5);
        yield return SakiAtomLibrary.AddNoteAndBlock(1, NoteKind.Power, 5);
        yield return SakiAtomLibrary.AddNoteAndBlock(1, NoteKind.Special, 5);
        yield return SakiAtomLibrary.AddNoteAndDamage(2, NoteKind.Attack, 8);
        yield return SakiAtomLibrary.AddNoteAndBlock(2, NoteKind.Skill, 8);
        yield return SakiAtomLibrary.AddNoteAndDraw(1, NoteKind.Attack, 1);
        yield return SakiAtomLibrary.AddNoteAndEnergy(1, NoteKind.Attack, 1);
        yield return SakiAtomLibrary.ClearNotes(1);
        yield return SakiAtomLibrary.ClearNotes(2);
    }    /// <summary>爱A1 和弦槽位（2026-11-10，本体 6 张卡的机制）。</summary>
    private static IEnumerable<ComponentAtom> ChordSlotAtoms()
    {
        yield return SakiAtomLibrary.GrantChordSlot(1);
        yield return SakiAtomLibrary.GrantChordSlot(2);
    }

    /// <summary>爱音「储存和弦 / 演奏最后一个」（T-004）。</summary>
    private static IEnumerable<ComponentAtom> StoredChordAtoms()
    {
        // ★ 2026-11-13 用户口径：remember 拆成两个原子——
        //   「随机储存 N 个已记忆和弦」（随机、只储存、不自动演奏）
        //   +「获得 N 个随机和弦槽位，若已有则演奏」（见 ChordSlotAtoms）。
        yield return SakiAtomLibrary.StoreRandomChords(1);
        yield return SakiAtomLibrary.StoreRandomChords(2);
        yield return SakiAtomLibrary.StoreRandomChords(3);
        yield return SakiAtomLibrary.PlayLastChord(1);
        yield return SakiAtomLibrary.PlayLastChord(2);
    }

    /// <summary>爱音复合同子：演奏 + 伤害/格挡（T-003 的主力）。</summary>
    private static IEnumerable<ComponentAtom> CompoundChordAtoms()
    {
        yield return SakiAtomLibrary.PlayChordAndDamage(1, 6);
        yield return SakiAtomLibrary.PlayChordAndDamage(1, 9);
        yield return SakiAtomLibrary.PlayChordAndDamage(1, 12);
        yield return SakiAtomLibrary.PlayChordAndBlock(1, 8);
        yield return SakiAtomLibrary.PlayChordAndBlock(1, 12);
        yield return SakiAtomLibrary.PlayChordAndBlock(1, 16);
    }

    /// <summary>爱音复合同子：音符 + 伤害/格挡。</summary>
    private static IEnumerable<ComponentAtom> CompoundNoteAtoms()
    {
        yield return SakiAtomLibrary.AddNoteAndDamage(1, NoteKind.Attack, 6);
        yield return SakiAtomLibrary.AddNoteAndDamage(1, NoteKind.Attack, 9);
        yield return SakiAtomLibrary.AddNoteAndBlock(1, NoteKind.Skill, 8);
        yield return SakiAtomLibrary.AddNoteAndBlock(1, NoteKind.Skill, 12);
    }



    private static IEnumerable<ComponentAtom> ChordAtoms()
    {
        yield return SakiAtomLibrary.PlayStoredChords(1);
                yield return SakiAtomLibrary.PlayRandomChord(1);
        yield return SakiAtomLibrary.PlayRandomChord(2);
    }

    /// <summary>
    /// 乐奈：食用芭菲 + 大效果 / 获得芭菲 / 猫咪牌 / 糖果 + 本体专属能力。
    /// ★ 芭菲是消耗型资源（基准表 §1.2），「食用」必须与收益成对出现。
    /// </summary>
    private static IEnumerable<ComponentAtom> RanaOnly()
    {
        // ★ 芭菲方向成长只给乐奈（机制归属，见 §3.86）
        yield return SakiAtomLibrary.BlockGrowthParfait(5, 1);
        yield return SakiAtomLibrary.BlockGrowthParfait(6, 2);
        foreach (var atom in Repeat(ParfaitAtoms(), 3)) yield return atom;
        foreach (var atom in Repeat(NekoAtoms(), 4)) yield return atom;
        // ★ 莱芜（T-005）：确认它就是本体的 RanaLivePower（Counter 叠层，满 10 层自动转莱芜爽）。
        //   所以不需要新 opcode，只要把这条能力原子**多重复几遍**并给出本体的真实层数档位。
        //   定价：1 层 ≈ 0.4 VP = 80 单位（数值基准表 §1.5），越高层边际越贵，这里按简单线性处理。
        foreach (var atom in Repeat(LiveAtoms(), 4)) yield return atom;
        foreach (var atom in SakiPowerCatalog.For(RandomProfiles.Rana).Select(p => SakiPowerCatalog.Atom(p, 1)))
            yield return atom;
    }

    private static IEnumerable<ComponentAtom> ParfaitAtoms()
    {
        // 食用 = 代价，所以配超稀有度的数值（本体 SOFT_BIG_BELLY / PARFAIT_BIG_BANG 校准）
        yield return SakiAtomLibrary.EatParfaitBlock(1, 16);
        yield return SakiAtomLibrary.EatParfaitBlock(1, 22);
        yield return SakiAtomLibrary.EatParfaitBlock(2, 30);
        yield return SakiAtomLibrary.EatParfaitDamageAll(1, 14);
        yield return SakiAtomLibrary.EatParfaitDamageAll(2, 22);
        yield return SakiAtomLibrary.EatParfaitDamageAll(2, 34);
        yield return SakiAtomLibrary.EatParfaitHealDraw(1, 8);
        yield return SakiAtomLibrary.EatParfaitHealDraw(2, 14);
        yield return SakiAtomLibrary.EatParfaitDraw(1, 3);
        // 获得芭菲（资源积累）
        yield return SakiAtomLibrary.GainParfait(2);
        yield return SakiAtomLibrary.GainParfait(4);
        yield return SakiAtomLibrary.GainParfait(6);
    }

    private static IEnumerable<ComponentAtom> NekoAtoms()
    {
        // 乐A5 升级猫咪（2026-11-10）
        yield return SakiAtomLibrary.UpgradeNeko(1);
        yield return SakiAtomLibrary.UpgradeNeko(2);
        // 乐A6 格挡＝敌人数量 × N
        yield return SakiAtomLibrary.BlockByEnemyCount(3);
        yield return SakiAtomLibrary.BlockByEnemyCount(4);


        // ★ 独立回血原子**全部撤掉**（2026-11-10 用户口径：
        //   「乐奈回血也不多，只有基础牌」）。
        //   本体的回血只出现在基础牌（大口朵颐 / 防御）身上，随机卡不该大量回血。
        //   现在唯一的回血途径是「食用抹茶芭菲」类复合原子（对应本体大口朵颐）。
        // 乐050 猫咪保留（2026-11-10）
        yield return SakiAtomLibrary.NekoRetain(1);
        yield return SakiAtomLibrary.AddNekoAndBlock(1, 5);
        yield return SakiAtomLibrary.NekoRetain(2);
        // 乐030 未被完全格挡→抽牌 / 对称拓展：完全格挡→抽牌（2026-11-12）
        foreach (var atom in Repeat(new[] { SakiAtomLibrary.DamageDrawIfUnblocked(5, 1),
                                            SakiAtomLibrary.DamageDrawIfUnblocked(7, 2),
                                            SakiAtomLibrary.DamageDrawIfBlocked(5, 1),
                                            SakiAtomLibrary.DamageDrawIfBlocked(7, 2) }, 5)) yield return atom;
        // 2026-11-12 对称拓展加权：完全格挡抽牌独立重复，避免被原有未格挡原子稀释。
        foreach (var atom in Repeat(new[] { SakiAtomLibrary.DamageDrawIfBlocked(5, 1),
                                            SakiAtomLibrary.DamageDrawIfBlocked(7, 2) }, 3))
            yield return atom;
        // 乐038 挑食（2026-11-10）
        yield return SakiAtomLibrary.DiscardNonParfait(1);
        // 乐046 消耗手牌当芭菲 / 乐056 任何处打出猫咪（2026-11-10）
        yield return SakiAtomLibrary.ConsumeAsParfait(1);
        yield return SakiAtomLibrary.PlayNekoAnywhere(1);
        yield return SakiAtomLibrary.PlayNekoAnywhere(2);
        // 乐013/乐032 从抽牌堆打出猫咪（2026-11-10）
        yield return SakiAtomLibrary.PlayNekoFromDraw(1);
        yield return SakiAtomLibrary.PlayNekoFromDraw(2);
        // 乐A4 消耗手牌所有猫咪换伤害（2026-11-10）
        yield return SakiAtomLibrary.ConsumeNekoDamage(3);
        yield return SakiAtomLibrary.ConsumeNekoDamage(4);
        // 手牌操作（2026-11-10）：清状态牌换抽牌 / 复制本牌 / 压力换返回手牌
        foreach (var atom in Repeat(new[] { SakiAtomLibrary.ExhaustStatusDraw(1),
                                            SakiAtomLibrary.CopySelf(1) }, 5)) yield return atom;
        // 祥028/064 伤害＝目标压力层数×N（2026-11-10）
        yield return SakiAtomLibrary.DamageByTargetPressure(2);
        // 乐A1 伤害＝自身格挡（2026-11-10）
        yield return SakiAtomLibrary.DamageByBlock(4);
        // ★ 2026-11-14（本体「把衣服剪开」升级版）：清敌人格挡 + 按自身格挡 ×N 出伤。
        //   直接 yield，权重与 DamageByBlock 同档。
        yield return SakiAtomLibrary.BlockSlash(1);
        yield return SakiAtomLibrary.BlockSlash(2);
        // ★ 2026-11-14（本体「快速学习」）：对全体造成「敌人攻击意图总和 ×N」的伤害。
        yield return SakiAtomLibrary.DamageByEnemyIntent(1);
        yield return SakiAtomLibrary.DamageByEnemyIntent(2);


        // 乐A2/A3（2026-11-10）：买芭菲 / 猫咪进抽牌堆 / 三牌堆各一张
        yield return SakiAtomLibrary.BuyParfait(10);
        yield return SakiAtomLibrary.NekoToDraw(1);
        yield return SakiAtomLibrary.NekoToDraw(2);
        yield return SakiAtomLibrary.NekoToAllPiles(1);
        yield return SakiAtomLibrary.GainNeko(1);
        yield return SakiAtomLibrary.GainNeko(2);
        yield return SakiAtomLibrary.GainNeko(3);

        // ⚠️ 已移除「获得糖果牌」（T-006 调查结论）：
        //    `Pancake` 关键词在本体只挂在**一张**卡上（煎饼打击 PancakeStrike），
        //    而那张卡属于**彩蛋池**（CuteSakikoModEggCard），不在令牌池里。
        //    我们的 TokenCardHandler 是从 CuteSakikoTokenCardPool 里按关键词筛，
        //    筛不到就直接 return —— 于是「获得糖果牌」是个**死效果**（打出去什么都不发生）。
        //    死效果比没有更糟，所以整条移除。等本体真的往令牌池加松饼/糖果牌再加回来。
    }

    /// <summary>乐奈的莱芜（T-005）。本体卡给 2~4 层，满 10 层自动转莱芜爽。</summary>
    private static IEnumerable<ComponentAtom> LiveAtoms()
    {
        var live = SakiPowerCatalog.For(RandomProfiles.Rana)
            .FirstOrDefault(power => power.Variant == "rana_live");
        if (string.IsNullOrEmpty(live.Variant)) yield break;

        yield return SakiPowerCatalog.Atom(live, 2);
        yield return SakiPowerCatalog.Atom(live, 3);
        yield return SakiPowerCatalog.Atom(live, 4);
    }

    /// <summary>把一组组件原样重复 N 遍（提高该机制的出率权重）。</summary>
    private static IEnumerable<ComponentAtom> Repeat(IEnumerable<ComponentAtom> source, int times)
    {
        var list = source.ToArray();
        for (var i = 0; i < times; i++)
            foreach (var atom in list)
                yield return atom;
    }
}





































































































