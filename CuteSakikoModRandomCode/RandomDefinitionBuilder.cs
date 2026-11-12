using MegaCrit.Sts2.Core.Logging;
using AutoAnthony;
using ChaosCardGenerator;
// 2026-11-11 用户三条：描述重建 / 预算下限 / 名字关键词（见 BuildOneSlot 注释）。

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 把 AutoAnthony 生成出来的 <see cref="GeneratedCard"/> 组装成可安装的
/// <see cref="ChaosCardDefinition"/>。
///
/// ★ 辉星（Star）必须清掉（2026-11-09 用户要求）：
///   祥子和乐奈借用了 <c>GeneratedCharacter.Regent</c> 作为平衡原型，
///   而 AutoAnthoney 用 <c>_character == Regent</c> 判断「这个角色有辉星资源」，
///   于是按 25% 概率给生成的牌加**辉星费用**。
///   但祥子/爱音/乐奈根本没有辉星池 —— 加上的辉星费让这些牌**根本打不出去**。
///   ⇒ 生成完立刻把 StarCost 归 -1、HasStarCostX 归 false，并把描述里的辉星标记清掉。
///
/// 关键约束（InstallDefinitions 会逐条校验）：
///   · Slot 从 0 连续；
///   · Card.Character 必须等于注册的 BalanceArchetype；
///   · 每个 operation 都必须带已校验的 RuntimeSpec；
///   · 槽位数量必须等于 RegisterRuntime 声明的 CardCount。
/// </summary>
internal static class RandomDefinitionBuilder
{
    /// <summary>
    /// 生成期的**确定性**随机源。
    ///
    /// ★★ 2026-11-12 联机实测：以前这里直接用 <c>Random.Shared</c>（进程级随机），
    ///   同一 seed 下主机和客户端生成的卡池指纹完全不同（saki 5C66… vs B0F1…），
    ///   进战斗第一回合 checksum 就直接分叉（StateDivergence）。
    ///   ⇒ 改成按 (profileId, seed) 播种的 <see cref="Random"/>：
    ///     同种子、同生成顺序 → 同结果。BuildCore 每次开始都会重置。
    ///   ★ 不要在这里放跨线程共享：生成可能在不同线程跑（单人抢跑 / 联机 RunStartedEvent），
    ///     所以用 [ThreadStatic]，每个线程一份。
    /// </summary>
    [ThreadStatic]
    private static Random? _generationRng;

    private static Random GenerationRng =>
        _generationRng ??= new Random(0x5A4B1);

    private static void SeedGenerationRng(int stableSeed)
    {
        // 和真正喂给生成器的种子分开，避免「调随机」污染卡池生成本身。
        _generationRng = new Random(unchecked(stableSeed * 397 ^ 0x5F3759DF));
    }

    /// <summary>
    /// 生成整套定义（带 AA 原生进度层）。
    ///
    /// ★ 关键：AA 自己的生成也是**后台线程 + 主线程轮询**（ChaosRunDefinitions.cs:652-666）：
    ///     var task = Task.Run(...);
    ///     while (!task.Wait(8)) { progress.Report(latest); DisplayServer.ProcessEvents(); }
    /// 只在主线程同步跑的话，Label 排版永远等不到帧，文字就画不出来（我们踩了一整轮）。
    /// </summary>
    internal static IReadOnlyList<ChaosCardDefinition> Build(string profileId, string seed)
    {
        var expected = RandomCardSlots.CountFor(profileId);
        IReadOnlyList<ChaosCardDefinition> result = null!;
        Exception? failure = null;
        var latest = 0;

        RandomProgressHub.StartProfile(profileId);
        RandomProgressHub.ReportLocal(1);

        // 生成放后台线程；主线程一边报进度一边泵事件，进度层才画得出来。
        var task = System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                result = BuildCore(profileId, seed,
                    current => System.Threading.Volatile.Write(ref latest, current));
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        while (!task.Wait(8))
        {
            RandomProgressHub.ReportLocal(Math.Clamp(System.Threading.Volatile.Read(ref latest), 1, expected));
            if (!RandomProgressHub.SuppressPumping) RandomProgressHub.Pump();
        }

        task.Wait();
        if (failure is not null) throw new InvalidOperationException("随机卡生成失败", failure);
        return result;
    }

    private static IReadOnlyList<ChaosCardDefinition> BuildCore(string profileId, string seed, Action<int>? report)
    {
        var archetype = RandomProfiles.ArchetypeFor(profileId);
        var expected = RandomCardSlots.CountFor(profileId);
        var shells = RandomShells.For(profileId);

        // 种子必须稳定（跨存档 / 跨联机一致）：SHA-256 而不是 string.GetHashCode。
        var stableSeed = StableSeed(profileId, seed);
        // ★★ 2026-11-12：生成期的所有「随机取舍」都走这个确定性 RNG，
        //   不能再用 Random.Shared（进程级随机 → 联机两端生成不同卡池 → checksum 分叉）。
        SeedGenerationRng(stableSeed);
        // ★ 2026-11-11 用户要求：「设置里开启原版数值后，VP 要符合文档的计算方案」。
        //   根因：我们之前构造生成器时**没传 balancedValues**（默认 false），
        //   所以 AA 的「原版数值」开关对我们的随机卡完全不生效。
        //   这里读取 AA 的开关并透传（读不到就按 true 处理，与 AA 默认一致）。
        var balancedValues = true;
        try { balancedValues = AutoAnthony.ChaosRunDefinitions.ActiveNumericBalanceOptimization; }
        catch { }

        var generator = new RandomCardGenerator(
            new ComponentProfileRequest(profileId, archetype, false), stableSeed, balancedValues);
        // 便于实机排错：把「角色 + 原始种子 + 稳定种子」写进日志，离线复现时照抄这个整数。
        Log.Info($"[CuteSakikoModRandom] {RandomProfiles.DisplayNameFor(profileId)} 池子种子 raw={seed} stable={stableSeed}");

        // 卡图候选：本角色真实卡池里所有卡的卡面路径。
        

        // 诊断用：记录生成耗时（如果哪天卡住，看这两行就知道是慢还是死锁）
        var startedAt = System.Diagnostics.Stopwatch.StartNew();

        var definitions = new List<ChaosCardDefinition>(expected);
        // ★ 2026-11-11 用户实测「跨池共享去重反而重复率更高」：三个池共用一个集合，
        //   很快耗尽 → 全部落到同一个兜底图。所以改为**每池独立去重**。
        // 设置：禁止重复卡图（默认开）。关掉时每次都不共享已用集合 → 允许重复。
        var usedPortraits = new HashSet<string>(StringComparer.Ordinal);
        var generateCalls = 0;
        var buildCalls = 0;
        long generateTicks = 0;
        long upgradeTicks = 0;
        long descriptionTicks = 0;
        var mechanicRetries = 0;
        var bareRetries = 0;
        var perfWatch = System.Diagnostics.Stopwatch.StartNew();
        GeneratedCard Generate(GeneratedRarity rarity)
        {
            generateCalls++;
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            var generated = generator.Generate(rarity);
            generateTicks += System.Diagnostics.Stopwatch.GetTimestamp() - started;
            return generated;
        }

        report?.Invoke(0);

        // ------------------------------------------------------------------
        // 一个槽位的**完整**流水线（局部函数；捕获 generator/profileId/archetype）。
        //
        // 为什么抽出来：预算（上限/下限）不达标时要用**同一条完整流水线**重抽，
        // 不能像以前那样只调 Prepare() —— 那会绕过下面所有内容闸门。
        // ------------------------------------------------------------------
        GeneratedCard BuildOneSlot(GeneratedRarity rarity, int slot)
        {
            buildCalls++;
            var retriesLeft = 1;
            GeneratedCard card;
            try
            {
                card = OperationRuntimeSpecCompiler.Attach(Generate(rarity));
                // ★ 费用校正（2026-11-10 离线模拟开局发现的问题）：
                //   费用来自**壳表**、效果强度来自**稀有度预算**，两者会脱节 ——
                //   实测出现「0 费造成 20 点伤害」「2 费只给 13 金币」。
                card = CardCostCalibration.Apply(card);
                // 设置：复合效果 → 单效果卡重抽（用户要求「不会有纯粹的单效果卡牌」）。
                if (RandomConfig.CompoundEffect)
                {
                    for (var retry = 0; retry < 8 && card.Operations.Count < 2; retry++)
                        card = OperationRuntimeSpecCompiler.Attach(Generate(rarity));
                // ★ 费用校正（2026-11-10 离线模拟开局发现的问题）：
                //   费用来自**壳表**、效果强度来自**稀有度预算**，两者会脱节 ——
                //   实测出现「0 费造成 20 点伤害」「2 费只给 13 金币」。
                card = CardCostCalibration.Apply(card);
                }
            }
            catch (Exception)
            {
                // 兜底：用最朴素的伤害组件保证每个槽位一定有牌，绝不让整局起不来。
                card = FallbackCard(rarity, archetype);
            }

            // ★ 防线（2026-11-10）：**触发型 + 结束回合**不能同时出现。
            //   AA 自己会拒绝这种组合（HasValidTriggeredEndTurnAssembly），
            //   但我们用的是自建 opcode，绕过了它的校验，
            //   结果生成出「在你的回合开始时，结束你的回合」这种逻辑坏掉的卡。
            //   这里命中就重抽这个槽位。
            while (HasTriggerWithEndTurn(card) && retriesLeft-- > 0)
                card = Prepare(Generate(rarity));

            // ★ B3 缓解（2026-11-10 实机）：自定义原子的数值占位符在**第 2 段及以后**会被渲染成
            //   {Amount0:diff()}（占位符按「原子内槽序号」编号，而 DynamicVar 按「卡内操作序号」建，
            //   复合卡里两者对不上）。成因未 100% 坐实，但重抽能显著减少坏卡，且无副作用。
            while (HasCustomNumberedBeyondFirst(card) && retriesLeft-- > 0)
                card = Prepare(Generate(rarity));

            // ★ 清掉辉星：祥子/爱音/乐奈没有辉星池，带辉星费的牌打不出去。
            card = StripStarCost(card);

            // ★ 2026-11-11：纯原版卡（伤害/格挡/抽牌，没有任何机制）观感太差，轻微重抽。
            //   次数必须**很小**：AA 有「唯一性预算」，重抽太多会抛
            //   "Could not produce a unique emergency fallback"（实测 24 次必炸）。
            // ★ P8 性能：不再为「没有机制」重抽（每次完整 Generate 约 20ms，
            //   一次重抽就是 20~60ms）。纯原版卡改由 P2/P3 的复合/补基础效果兜底。


            // ★ B12（2026-11-10）：**技能/能力牌不应该带伤害效果**。
            //   实测本体卡池里「技能却带伤害」的卡数量 = 0，所以这是生成器的类型/类别错配。
            while (IsTypeMismatched(card) && retriesLeft-- > 0)
                card = Prepare(Generate(rarity));

            // ★ B5 兜底（2026-11-10）：任何操作出现离谱数值（如「给予99层虚弱」）就重抽。
            //   实测来源：`apply_power|weak` 的槽被写成 99（src=fixed, up=False），
            //   这不是我们定义的值（我们只给 ApplyWeak(1)），是 AA 侧生成的异常。
            while (HasAbsurdValue(card) && retriesLeft-- > 0)
                card = Prepare(Generate(rarity));

            // ★ 辅助效果 / 纯负面效果**不许单独成卡**（2026-11-10 用户实测：（2026-11-10 用户实测：
            //   「客服诱人：消耗5层压力，将此牌返回手牌」——这种只有附属效果的卡毫无意义；
            //   「心必得：获得2层破防」——纯负面）。
            while (IsSupportOnlyCard(card) && retriesLeft-- > 0)
                card = Prepare(Generate(rarity));

            // ★ 抽牌数额离谱 → 重抽
            while (HasAbsurdDraw(card) && retriesLeft-- > 0)
                card = Prepare(Generate(rarity));

            // ★ 能量数额离谱 → 重抽（保证文案 = 实际效果）
            while (HasAbsurdEnergy(card) && retriesLeft-- > 0)
                card = Prepare(Generate(rarity));

            // ★ 2026-11-11 用户第 1/3/5 条（这些闸门之前被重构误删，现在补回来）：
            //   · 能力（csk_power）只能是 **Power 牌** 且 **蓝卡以上**；
            //   · 只有素效果（单操作：伤害/格挡/抽牌/金币/能量/回血/能力）的卡一律重抽；
            //   · 只有「食用芭菲」没有收益的卡一律重抽。
            while (HasPowerOperation(card)
                   && (rarity is GeneratedRarity.Basic or GeneratedRarity.Common || card.Type != GeneratedCardType.Power)
                   && retriesLeft-- > 0)
                card = Prepare(Generate(rarity));

            while (IsBareSingleEffect(card) && retriesLeft-- > 0)
            {
                bareRetries++;
                card = Prepare(Generate(rarity));
            }

            while (IsParfaitEatOnly(card) && retriesLeft-- > 0)
                card = Prepare(Generate(rarity));

            // ★ P8 性能：升级计划挪到 FinishSlot 最终卡上生成一次，重抽候选不再各生成一次。

            // ★ 名字带机制词 → 补对应关键词（2026-11-10 用户要求）。
            card = ApplyNameKeywords(card, profileId);

            // ★★ 2026-11-11 用户第 2 条：两位数数值的**真正来源**找到在 AA 源码：
            //   ComponentAssemblyGenerator.RaiseNumericRewardsToTopRarityFloorCore 会为了「达到顶级稀有度
            //   的最低价值」反复把数值**抬高**（scale 1.05~4，最多 32 轮），
            //   对我们的自定义原子（csk_*）尤其明显 → 就出现「11 杯芭菲 / 34 层和声」。
            //   官方这个路径在生成后不再复查上限，所以我们在定义前做最后一道**数值兜底**。
            card = ClampCardOperations(card);

            // ★ 2026-11-11 用户第 5 条：基础效果（伤害/格挡/抽牌/能量）要尽量多出现，
            //   和 mod 机制复合，而不是一张卡只有一条机制。生成后按概率补一个基础效果。
            card = AppendBasicEffect(card);

            // ★★ 2026-11-11 用户第 1 条：卡面文字是**冻结字符串**，改 operation 不会改文本。
            //   AA 有公开的 CardDescriptionRenderer.Render(operations)（0.3.156 已核实），
            //   所以改完数值必须按 operations 重建中文/英文描述 + 升级描述。
            // ★ P8 性能：描述重建挪到 FinishSlot 的最终卡上，避免每个候选（重抽/预算）都重建一次。
            return card;
        }

        // P8 性能：砍掉高值兜底的重抽后，少数卡仍会略高于 band；
        // 这里不再重抽，直接移除最后一个非触发 operation（每次约 0ms），
        // 最坏退化成本角色的朴素兜底卡，保证不会出现「高值」。
        GeneratedCard ForceHighFallback(GeneratedCard card)
        {
            try
            {
                var band = CardValueProfile.For(card.Rarity, card.Cost);
                if (band is null) return card;
                var operations = card.Operations.ToList();
                var guard = 0;
                while (operations.Count > 1
                       && CardValuator.Evaluate(card with { Operations = operations.ToArray() }) > band.Value.High
                       && guard++ < 6)
                {
                    var index = operations.FindLastIndex(operation =>
                        !operation.Template.StartsWith("A:", StringComparison.Ordinal));
                    if (index < 0) index = operations.Count - 1;
                    operations.RemoveAt(index);
                }

                var trimmed = card with { Operations = operations.ToArray() };
                if (CardValuator.Evaluate(trimmed) <= band.Value.High)
                {
                    if (!operations.SequenceEqual(card.Operations))
                        Log.Info($"[高位兜底] 「{card.Name?.Chinese}」{card.Rarity} {card.Cost}c "
                                 + $"ops {card.Operations.Count}→{operations.Count}");
                    return trimmed;
                }

                Log.Info($"[高位兜底] 「{card.Name?.Chinese}」{card.Rarity} {card.Cost}c 退化为基础卡");
                return FallbackCard(card.Rarity, archetype);
            }
            catch { return card; }
        }

        // 预算 best-of-N：不在合法区间时用**同一条完整流水线**重抽，取最接近区间的那张。
        // Rare/Ancient 的**下限**也在这里兜住（用户 2026-11-11 第 2 条）。
        GeneratedCard FitBudget(GeneratedCard card, GeneratedRarity rarity, int slot)
        {
            var best = card;
            var bestScore = BudgetFitScore(card);
            if (bestScore >= 1d) return card;
            // ★ P8 性能：只给 Ancient 多 1 次；Rare/Common/Uncommon 不再重抽，
            //   价值由 P2/P3/P4 兜，生成量优先压到 3~5 秒。
            var attempts = 1;
            for (var attempt = 0; attempt < attempts && bestScore < 1d; attempt++)
            {
                try
                {
                    var candidate = BuildOneSlot(rarity, slot);
                    var score = BudgetFitScore(candidate);
                    if (score > bestScore)
                    {
                        best = candidate;
                        bestScore = score;
                    }
                }
                catch { break; }
            }

            return best;
        }

        // P1 只读：最终卡 vs 本体同稀有度/同费用的目标带。只打「带外」的卡，避免日志刷屏。
        void LogValueAlignment(GeneratedCard card)
        {
            try
            {
                var band = CardValueProfile.For(card.Rarity, card.Cost);
                if (band is null) return;
                var value = CardValuator.Evaluate(card);
                if (band.Value.Contains(value)) return;
                var tag = value < band.Value.Low ? "低" : "高";
                var profile = RandomProfiles.DisplayNameFor(profileId);
                Log.Info($"[价值对齐] [{profile}] 「{card.Name?.Chinese}」{card.Rarity} {card.Cost}c "
                         + $"V={value:F1} 目标={band.Value.Low:F1}~{band.Value.High:F1} {tag}");

                // 严重带外（>1.5×上限 或 <0.5×下限）时打明细，用来分辨「估值器漏变体」还是「卡真的超模」。
                // 现在所有带外都打，方便直接定位具体 op。
                var details = string.Join(", ", card.Operations.Select(operation =>
                {
                    var spec = OperationRuntimeSpecCompiler.GetOrCompile(operation);
                    var vp = AtomVpTable.Vp(spec);
                    // 和 CardValuator 保持一致：永久 power 在非 Power 卡上 ×1.5。
                    var boosted = card.Type != GeneratedCardType.Power && AtomVpTable.IsPermanentPower(spec);
                    if (boosted) vp *= CardValuator.OffTypePermanentPowerMultiplier;
                    return $"{operation.Template}={vp:F1}{(boosted ? "(×1.5)" : "")}({operation.ChineseText})";
                }));
                Log.Info($"[价值对齐][明细] [{profile}] 「{card.Name?.Chinese}」{card.Rarity} {card.Cost}c "
                         + $"V={value:F1} ops=[{details}]");
            }
            catch { }
        }

        var seenSignatures = new HashSet<string>(StringComparer.Ordinal);
        var templateCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        static string TemplateSignature(GeneratedCard candidate) =>
            string.Join("|", candidate.Operations.Select(operation => operation.Template));

        GeneratedCard FinishSlot(GeneratedCard candidate, int slotIndex, GeneratedRarity rarity)
        {
            candidate = FitBudget(candidate, rarity, slotIndex);
            candidate = LiftToBudgetFloor(candidate, profileId);
            candidate = TrimToValueBand(candidate, profileId, slotIndex);
            candidate = AppendRidersToBand(candidate, profileId);
            candidate = TrimToValueBand(candidate, profileId, slotIndex);
            // ★ P8：补机制放在最终 Trim 之后，避免刚补的机制又被 P3 当最高 op 删掉。
            candidate = EnsureMechanic(candidate, profileId);
            // ★ P1：补机制可能把贴上限的卡顶出去；先按安全槽缩数值，保住机制，再兜高值。
            candidate = TrimToValueBand(candidate, profileId, slotIndex);
            candidate = ForceHighFallback(candidate);
            // ★ P8.5 折中：高值兜底可能把刚补的机制删掉；给纯原版卡补一个
            //   本角色关键词（0 VP），既不抬数值，也不会看起来完全是原版卡。
            candidate = EnsureKeywordIdentity(candidate, profileId);
        // ★ 2026-11-12 P0：补关键词在 ForceHighFallback 之后，可能把价值顶出上限
        //   （例：爱音「共同前嫌」格挡9+伤害3 补 Playguitar 后 V=11.0 > 10.5）。
        //   关键词补完必须再兜一次高值。
        candidate = ForceHighFallback(candidate);
            // ★ P1 最后一道：芭菲负成本/超杯数在这里统一替换成正面基础效果，
            //   之后不再有 Trim/Lift 把负成本加回来。
            candidate = SanitizeParfaitCosts(candidate, profileId, out var parfaitChanged);
            candidate = ForceHighFallback(candidate);
            if (parfaitChanged)
            {
                // 芭菲清理后可能掉到低值：只在这种情况下补，避免全池多跑一遍。
                candidate = LiftToBudgetFloor(candidate, profileId);
                candidate = AppendRidersToBand(candidate, profileId);
                candidate = TrimToValueBand(candidate, profileId, slotIndex);
                candidate = ForceHighFallback(candidate);
            }
            // ★ P8 性能：升级计划只对最终卡生成一次；也保证基于最终 operations。
            var upgradeStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            candidate = RegenerateUpgrade(candidate, profileId, slotIndex);
            upgradeTicks += System.Diagnostics.Stopwatch.GetTimestamp() - upgradeStarted;
            // ★ P8 性能：只在最终卡上重建描述一次。
            var descriptionStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            candidate = RebuildDescriptions(candidate);
            descriptionTicks += System.Diagnostics.Stopwatch.GetTimestamp() - descriptionStarted;
            candidate = SanitizeEnergyText(candidate, profileId);
            // ★ P1 最后保证：任何后续变换都不能把机制漏掉。
            if (!HasMechanicOperation(candidate))
            {
                candidate = EnsureMechanic(candidate, profileId);
                candidate = RebuildDescriptions(candidate);
                candidate = SanitizeEnergyText(candidate, profileId);
            }
            return candidate;
        }

        static string SignatureOf(GeneratedCard candidate) =>
            string.Join("|", candidate.Operations.Select(operation => operation.Template))
            + "#" + candidate.ChineseDescription;

        for (var slot = 0; slot < expected; slot++)
        {
            report?.Invoke(slot + 1);
            var rarity = shells[slot].Rarity;

            var card = FinishSlot(BuildOneSlot(rarity, slot), slot, rarity);
            // ★ 2026-11-11 用户第 3/6 条：同一池子重复卡太多（乐奈「无论何处打出猫咪」），
            //   文本完全相同的卡重抽，最多 2 次，提升区分度。
            for (var retry = 0; retry < 3; retry++)
            {
                var textSignature = SignatureOf(card);
                var templateSignature = TemplateSignature(card);
                if (!seenSignatures.Contains(textSignature)
                    && templateCounts.GetValueOrDefault(templateSignature) < 2) break;
                card = FinishSlot(BuildOneSlot(rarity, slot), slot, rarity);
            }
            seenSignatures.Add(SignatureOf(card));
            var finalTemplate = TemplateSignature(card);
            templateCounts[finalTemplate] = templateCounts.GetValueOrDefault(finalTemplate) + 1;
            // P1 只读：把最终卡和本体目标带对比，只打日志，不改卡。
            LogValueAlignment(card);

            var portrait = RandomPortraits.Pick(profileId, card, slot,
                RandomConfig.NoDuplicateArt ? usedPortraits : new HashSet<string>(StringComparer.Ordinal));

            definitions.Add(new ChaosCardDefinition(
                Slot: slot,
                Card: card,
                PortraitPath: portrait,
                HitFx: "vfx/vfx_attack_slash",
                AttackAnimation: "Attack",
                PowerIconPath: string.Empty,
                PowerBigIconPath: string.Empty,
                RuntimeSpecs: card.Operations
                    .Select(OperationRuntimeSpecCompiler.RequireStructured).ToArray(),
                UpgradeValueSlots: card.Upgrade?.Effects
                    .Select(effect => effect.ValueSlotId).ToArray() ?? []));
        }

        startedAt.Stop();
        Log.Info($"[CuteSakikoModRandom] {RandomProfiles.DisplayNameFor(profileId)} "
                 + $"生成 {definitions.Count} 张卡耗时 {startedAt.ElapsedMilliseconds} ms。");
        MegaCrit.Sts2.Core.Logging.Log.Info(
            $"[性能] {profileId} slots={definitions.Count} build={buildCalls} "
            + $"generate={generateCalls} total={perfWatch.ElapsedMilliseconds}ms");
        MegaCrit.Sts2.Core.Logging.Log.Info(
            $"[性能细分] {profileId} generate={generateTicks * 1000 / System.Diagnostics.Stopwatch.Frequency}ms "
            + $"upgrade={upgradeTicks * 1000 / System.Diagnostics.Stopwatch.Frequency}ms "
            + $"desc={descriptionTicks * 1000 / System.Diagnostics.Stopwatch.Frequency}ms "
            + $"mechanic={mechanicRetries} bare={bareRetries}");

        // 诊断：这一局有多少张卡完全没有机制操作（应为 0）
        try
        {
            var noMech = definitions.Count(d => !HasMechanicOperation(d.Card));
            MegaCrit.Sts2.Core.Logging.Log.Info($"[CuteSakikoModRandom] {profileId} 纯原版卡 {noMech} / {definitions.Count} 张");
            if (noMech > 0)
            {
                var samples = definitions.Where(d => !HasMechanicOperation(d.Card)).Take(3)
                    .Select(d => $"{d.Card.Name?.Chinese}[{string.Join(",", d.Card.Operations.Select(o => o.Template))}]");
                MegaCrit.Sts2.Core.Logging.Log.Info($"[纯原版明细] {profileId} {string.Join("；", samples)}");
            }
            var healCards = definitions.Count(d =>
                (d.Card.ChineseDescription ?? string.Empty).Contains("回复", StringComparison.Ordinal));
            MegaCrit.Sts2.Core.Logging.Log.Info($"[回血频率] {profileId} {healCards} / {definitions.Count} 张");
            var pressureLess = definitions.Count(d =>
                (d.Card.ChineseDescription ?? string.Empty).Contains("小于自身", StringComparison.Ordinal));
            var fullyBlocked = definitions.Count(d =>
                (d.Card.ChineseDescription ?? string.Empty).Contains("若伤害被完全格挡", StringComparison.Ordinal));
            MegaCrit.Sts2.Core.Logging.Log.Info(
                $"[拓展原子] {profileId} 压力小于自身={pressureLess} 完全格挡={fullyBlocked} / {definitions.Count}");
            var pressureGreater = definitions.Count(d =>
                (d.Card.ChineseDescription ?? string.Empty).Contains("大于自身", StringComparison.Ordinal));
            var doublePressure = definitions.Count(d =>
                (d.Card.ChineseDescription ?? string.Empty).Contains("[/gold]翻倍", StringComparison.Ordinal));
            var extraDamage = definitions.Count(d =>
                (d.Card.ChineseDescription ?? string.Empty).Contains("额外造成", StringComparison.Ordinal));
            MegaCrit.Sts2.Core.Logging.Log.Info(
                $"[压力复合] {profileId} 大于={pressureGreater} 小于={pressureLess} 翻倍={doublePressure} 额外伤害={extraDamage}");
            var unblockedCards = definitions.Count(d =>
                (d.Card.ChineseDescription ?? string.Empty).Contains("未被完全格挡", StringComparison.Ordinal));
            var killCards = definitions.Count(d =>
                (d.Card.ChineseDescription ?? string.Empty).Contains("若击杀敌人", StringComparison.Ordinal));
            var notKillCards = definitions.Count(d =>
                (d.Card.ChineseDescription ?? string.Empty).Contains("若未击杀敌人", StringComparison.Ordinal));
            var fatalCards = definitions.Count(d =>
                (d.Card.ChineseDescription ?? string.Empty).Contains("斩杀时", StringComparison.Ordinal));
            var turnStartCards = definitions.Count(d =>
                (d.Card.ChineseDescription ?? string.Empty).Contains("在你的回合开始时", StringComparison.Ordinal));
            var conditionOps = definitions.Sum(d => d.Card.Operations.Count(operation =>
                operation.Template.StartsWith("csk:condition", StringComparison.Ordinal)));
            MegaCrit.Sts2.Core.Logging.Log.Info(
                $"[条件原子] {profileId} 未格挡={unblockedCards} 完全格挡={fullyBlocked} "
                + $"击杀={killCards} 未击杀={notKillCards} 斩杀={fatalCards} 回合开始={turnStartCards} "
                + $"conditionOps={conditionOps} / {definitions.Count}");
            foreach (var sample in definitions
                         .Where(d => d.Card.Operations.Any(operation =>
                             operation.Template.StartsWith("csk:condition", StringComparison.Ordinal)))
                         .Take(3))
            {
                MegaCrit.Sts2.Core.Logging.Log.Info(
                    $"[条件文本] {profileId} slot={sample.Slot} {sample.Card.Rarity} {sample.Card.Cost}c "
                    + $"{(sample.Card.ChineseDescription ?? string.Empty).Replace("\n", " / ")}"
                    + $" || 升级：{(sample.Card.Upgrade?.UpgradedChineseDescription ?? "无").Replace("\n", " / ")}");
            }
        }
        catch { }

        report?.Invoke(expected);

        // ★ 2026-11-11：用 AA 官方预算口径（NetValue vs WholeCardBudgetBounds 的上下限）
        //   统计「超标 / 低于下限」情况。下限只对 Rare/Ancient 生效（用户第 2 条口径）。
        try
        {
            var byRarity = definitions
                .Select(d =>
                {
                    var band = ReadBudget(d.Card);
                    return new
                    {
                        d.Card.Rarity,
                        band.Net,
                        band.Lower,
                        band.Upper
                    };
                })
                .GroupBy(x => x.Rarity)
                .OrderBy(g => g.Key.ToString(), StringComparer.Ordinal);

            foreach (var group in byRarity)
            {
                var rows = group.ToArray();
                Log.Info($"[CuteSakikoModRandom][预算统计] {group.Key} {rows.Length} 张 "
                         + $"均值净={rows.Average(r => r.Net):F0} "
                         + $"下限={rows.Max(r => r.Lower):F0} 上限={rows.Max(r => r.Upper):F0} "
                         + $"低于下限={rows.Count(r => r.Lower > 0 && r.Net < r.Lower)} 张 "
                         + $"超上限={rows.Count(r => r.Net > r.Upper)} 张");
            }

            // 诊断：仍低于下限的卡逐张列出（抬升/重抽后的漏网之鱼）。
            foreach (var definition in definitions)
            {
                var band = ReadBudget(definition.Card);
                if (band.Lower <= 0 || band.Net >= band.Lower) continue;
                Log.Warn($"[预算下限] 「{definition.Card.Name?.Chinese}」{definition.Card.Rarity} "
                         + $"cost={definition.Card.Cost} net={band.Net:F0} < lower={band.Lower:F0} | "
                         + definition.Card.ChineseDescription.Replace("\n", " / "));
            }

            // 诊断：名字含「琴奏」的卡是否真的带了 Playpiano 关键词 + Strike tag（用户第 3 条）。
            var pianoCards = definitions
                .Where(d => (d.Card.Name?.Chinese ?? string.Empty).Contains("琴奏", StringComparison.Ordinal))
                .ToArray();
            if (pianoCards.Length > 0)
            {
                var pianoKeyword = CuteSakikoMod.CuteSakikoModCode.Others.CutesakiKeywords.Playpiano;
                Log.Info($"[关键词统计] 名字含「琴奏」{pianoCards.Length} 张；带 Playpiano "
                         + $"{pianoCards.Count(d => (d.Card.CustomKeywords ?? []).Contains(pianoKeyword))} 张；"
                         + $"带 Strike tag {pianoCards.Count(d => d.Card.Tags.Contains(CardTag.Strike))} 张");
                // 核对：全池实际带 Playpiano 的数量必须等于「名字含琴奏」的数量。
                var allPiano = definitions.Count(d => (d.Card.CustomKeywords ?? []).Contains(pianoKeyword));
                Log.Info($"[关键词核对] Playpiano 总数={allPiano} 名字含琴奏={pianoCards.Length}");
            }

            // 每个角色都打一次：出鞘/特殊演奏/莱芜 只能出现在本角色池子里。
            var swordCount = definitions.Count(d => (d.Card.CustomKeywords ?? [])
                .Contains(CuteSakikoMod.CuteSakikoModCode.Others.CutesakiKeywords.Sword));
            var noNoteCount = definitions.Count(d => (d.Card.CustomKeywords ?? [])
                .Contains(CuteSakikoMod.CuteSakikoModCode.Others.CutesakiKeywords.NoNote));
            var ranaLiveCount = definitions.Count(d => (d.Card.CustomKeywords ?? [])
                .Contains(CuteSakikoMod.CuteSakikoModCode.Others.CutesakiKeywords.RanaLive));
            Log.Info($"[关键词核对] {profileId} 出鞘={swordCount} 特殊演奏={noNoteCount} 莱芜={ranaLiveCount}");

            var noteCards = definitions.Count(d => d.Card.Operations.Any(operation =>
            {
                try
                {
                    return OperationRuntimeSpecCompiler.GetOrCompile(operation).Opcode
                           == SakiComponents.NoteOpcode;
                }
                catch { return false; }
            }));
            Log.Info($"[机制核对] {profileId} 音符卡={noteCards}");
            var duplicateGroups = definitions
                .GroupBy(d => string.Join("|", d.Card.Operations.Select(operation => operation.Template)))
                .Where(group => group.Count() > 1)
                .OrderByDescending(group => group.Count())
                .Take(5)
                .ToArray();
            Log.Info($"[模板重复] {profileId} "
                     + string.Join("；", duplicateGroups.Select(group => $"{group.Key}×{group.Count()}")));
        }
        catch (Exception exception)
        {
            Log.Warn($"[CuteSakikoModRandom][预算统计] 失败：{exception.Message}");
        }

        // P1 只读：按稀有度×费用汇总「实际 V vs 本体目标带」，用来判断偏差方向。
        try
        {
            var rows = definitions
                .Select(d => new
                {
                    Profile = RandomProfiles.DisplayNameFor(profileId),
                    d.Card.Rarity,
                    d.Card.Cost,
                    Value = CardValuator.Evaluate(d.Card),
                    Band = CardValueProfile.For(d.Card.Rarity, d.Card.Cost)
                })
                .Where(row => row.Band is not null)
                .ToArray();

            foreach (var group in rows
                         .GroupBy(row => (row.Profile, row.Rarity, row.Cost))
                         .OrderBy(g => g.Key.Profile, StringComparer.Ordinal)
                         .ThenBy(g => g.Key.Rarity.ToString(), StringComparer.Ordinal)
                         .ThenBy(g => g.Key.Cost))
            {
                var band = group.First().Band!.Value;
                var count = group.Count();
                var average = group.Average(row => row.Value);
                var inBand = group.Count(row => band.Contains(row.Value));
                var low = group.Count(row => row.Value < band.Low);
                var high = group.Count(row => row.Value > band.High);
                Log.Info($"[价值对齐] [{group.Key.Profile}] {group.Key.Rarity} {group.Key.Cost}c {count}张 "
                         + $"均值V={average:F1} 目标={band.Low:F1}~{band.High:F1} "
                         + $"带内={inBand} 低={low} 高={high}");
            }
        }
        catch { }

        return definitions;
    }

    /// <summary>
    /// 项目口径的软上限：AA 的 OrdinaryUpperBound 是为原版五角色校准的，
    /// 对「白卡打 31 点」这种还是太宽松。按 卡牌数值基准表 再收一档：
    ///   Basic 1.00 / Common 0.65 / Uncommon 0.80 / Rare 1.00 / Ancient 1.00
    /// </summary>
    private static double UpperBoundFactor(GeneratedRarity rarity) => rarity switch
    {
        GeneratedRarity.Common => 0.65,
        GeneratedRarity.Uncommon => 0.80,
        _ => 1.0
    };

    /// <summary>
    /// AA `ComponentAssemblyGenerator.WholeCardBudgetBounds` 的「下限 / 上限」比例
    /// （balanced 模式；上限已经乘过 BalancedUpperBoundMultiplier = 1.15）。
    ///
    /// 来源（AA 源码 ComponentAssemblyGenerator.cs:1982-2006）：
    ///   Basic (0.80,1.13) / Common (0.84,1.15) / Uncommon (0.88,1.18)
    ///   Rare (0.85,1.20) / Ancient (0.84,1.26)
    /// ⇒ lower = OrdinaryUpperBound × lowerPercent / (upperPercent × 1.15)。
    ///
    /// 为什么用比例而不是反射调 internal 方法：`CardTinkeringApi.Analyze` 给的上限
    /// 已经包含了 AA 的全部修正（powerFactor / 费用 / 零费复制混合），
    /// 同一个 center 乘上下限比例即可，口径与 AA 一致。
    /// </summary>
    private static double LowerBoundRatio(GeneratedRarity rarity) => rarity switch
    {
        GeneratedRarity.Basic => 0.80d / (1.13d * 1.15d),
        GeneratedRarity.Common => 0.84d / (1.15d * 1.15d),
        GeneratedRarity.Uncommon => 0.88d / (1.18d * 1.15d),
        GeneratedRarity.Rare => 0.85d / (1.20d * 1.15d),
        GeneratedRarity.Ancient => 0.84d / (1.26d * 1.15d),
        _ => 0.84d / (1.15d * 1.15d)
    };

    /// <summary>合法预算区间：净价值要落在 [下限, 上限] 内（下限只对 Rare/Ancient 生效）。</summary>
    private readonly record struct BudgetBand(double Net, double Lower, double Upper)
    {
        public bool IsInBand => Net >= Lower && Net <= Upper;
    }

    private static BudgetBand ReadBudget(GeneratedCard card)
    {
        var analysis = ChaosCardGenerator.CardTinkeringApi.Analyze(
            card, new ChaosCardGenerator.CardTinkeringEvaluationOptions(true));
        var upper = analysis.Budget.OrdinaryUpperBound * UpperBoundFactor(card.Rarity);
        var lower = card.Rarity is GeneratedRarity.Rare or GeneratedRarity.Ancient
            ? analysis.Budget.OrdinaryUpperBound * LowerBoundRatio(card.Rarity)
            : 0d;
        return new BudgetBand(analysis.Budget.NetValue, lower, upper);
    }

    /// <summary>
    /// 预算贴合分数：在区间内 = 1；低于下限或高于上限 = 负的「相对越界幅度」。
    /// 用相对幅度是为了让「差 100/上限 1600」和「超 100/上限 1600」可比。
    /// </summary>
    private static double BudgetFitScore(GeneratedCard card)
    {
        try
        {
            var band = ReadBudget(card);
            if (band.IsInBand) return 1d;
            if (band.Net < band.Lower)
                return -(band.Lower - band.Net) / Math.Max(1d, band.Lower);
            return -(band.Net - band.Upper) / Math.Max(1d, band.Upper);
        }
        catch { return 0d; }
    }

    /// <summary>
    /// 重抽后仍低于稀有度下限时的**抬升**兜底（用户 2026-11-11 第 2 条：「不达标就重抽或抬」）。
    ///
    /// 只抬「安全槽」——伤害 / 格挡 / 金币 / 抽牌 / 治疗 / 能量；
    /// **绝不抬** csk_* 的机制数值（芭菲杯数、猫咪张数、能力层数、倍率），
    /// 否则就会把刚压下去的 11/22 又抬回来。
    /// 每轮挑第一个还有余量的槽 +20%，重新 Analyze，直到进区间或没有可抬的槽。
    /// </summary>
    private static GeneratedCard LiftToBudgetFloor(GeneratedCard card, string profileId)
    {
        try
        {
            return LiftToBudgetFloorCore(card, profileId);
        }
        catch
        {
            // 抬升是兜底增强；Analyze/改值任何一步失败都不能拖垮整局生成。
            return card;
        }
    }

    private static GeneratedCard LiftToBudgetFloorCore(GeneratedCard card, string profileId)
    {
        if (card.Rarity is not (GeneratedRarity.Rare or GeneratedRarity.Ancient)) return card;

        var band = ReadBudget(card);
        if (band.Net >= band.Lower) return card;

        var operations = card.Operations.ToArray();
        for (var step = 0; step < 12; step++)
        {
            var progressed = false;
            for (var i = 0; i < operations.Length && !progressed; i++)
            {
                var spec = OperationRuntimeSpecCompiler.GetOrCompile(operations[i]);
                foreach (var slot in OperationRuntimeSpecCompiler.ExplicitFixedValueSlots(operations[i]))
                {
                    if (!IsSafeLiftSlot(spec.Opcode, slot.Id)) continue;
                    var current = slot.BaseValue + slot.Offset;
                    var cap = NumericCapFor(spec.Opcode, slot.Id);
                    if (current >= cap) continue;
                    var next = Math.Min(cap, current + Math.Max(1, current / 5));
                    if (!OperationRuntimeSpecCompiler.TryReplaceFixedValue(operations[i], slot.Id, next,
                            out var updated)) continue;
                    operations[i] = updated;
                    progressed = true;
                    break;
                }
            }

            if (!progressed) break;
            var candidate = card with { Operations = operations };
            if (ReadBudget(candidate).Net >= band.Lower)
            {
                // 抬到区间内 → 重建描述（卡面必须跟着新数值走）。
                return SanitizeEnergyText(RebuildDescriptions(candidate), profileId);
            }
        }

        // 抬不满也把已抬的部分写回去（总比一点不抬好），并重建描述。
        return operations.SequenceEqual(card.Operations)
            ? card
            : SanitizeEnergyText(RebuildDescriptions(card with { Operations = operations }), profileId);
    }

    /// <summary>
    /// 可以安全抬升的槽位：只限「原生收益型」。
    /// 自定义机制（csk_*）的数值一律不抬 —— 那是 11/22 问题的来源。
    /// </summary>
    private static bool IsSafeLiftSlot(string opcode, string slotId)
    {
        // 金币允许抬（本体「客服工作」10/15），但仍然受 NumericCapFor 的荒谬值上限约束。
        if (opcode == SakiComponents.GoldOpcode) return slotId is "count" or "gold";

        // 伤害/格挡/治疗/抽牌/能量是**具体收益**，即使是复合原子里的也可以抬
        // （例：演奏和弦 + 16 格挡 → 抬格挡，不动「演奏几次」的计数）。
        // 计数类（芭菲杯数 / 猫咪张数 / 能力层数 / 倍率）一律不抬。
        return slotId is "damage" or "block" or "heal" or "draw" or "energy";
    }

    /// <summary>
    /// P2：卡的价值高于本体目标带时，按**我们的 VP** 等比缩小安全槽。
    ///
    /// 只缩 damage / block / heal / draw / energy / hits：
    ///   · 不缩 count / amount（杯数/猫咪/层数/倍率）—— 那会回到 11/22 的老路；
    ///   · 不增删 operation —— 升级计划和描述索引不会错位；
    ///   · 每步缩当前估算 VP 最大的槽 15%，最多 24 步，降到上限内就停。
    /// 缩完必须重建描述（卡面是冻结字符串）。
    /// </summary>
    private static GeneratedCard TrimToValueBand(GeneratedCard card, string profileId, int cardSlot)
    {
        try { return TrimToValueBandCore(card, profileId, cardSlot); }
        catch { return card; }
    }

    private static GeneratedCard TrimToValueBandCore(GeneratedCard card, string profileId, int cardSlot)
    {
        var band = CardValueProfile.For(card.Rarity, card.Cost);
        if (band is null) return card;

        var before = CardValuator.Evaluate(card);
        if (before <= band.Value.High) return card;

        var operations = card.Operations.ToArray();
        var steps = 0;
        var shrunk = 0;
        var raised = 0;
        while (steps++ < 24)
        {
            var current = CardValuator.Evaluate(card with { Operations = operations });
            if (current <= band.Value.High) break;

            // 用户 2026-11-11：优先缩收益，副升代价，随机来（25% 概率尝试升代价）。
            // 升代价只动「纯代价槽」（压力消耗/芭菲杯数/金币价/状态张数），
            // 且必须验证升完 V 真的下降，否则回滚走缩收益。
            if (GenerationRng.NextDouble() < 0.25
                && TryRaiseCostSlot(operations, out var costOperation, out var costSlot, out var costNext))
            {
                var original = operations[costOperation];
                if (OperationRuntimeSpecCompiler.TryReplaceFixedValue(
                        original, costSlot, costNext, out var costUpdated))
                {
                    operations[costOperation] = costUpdated;
                    if (CardValuator.Evaluate(card with { Operations = operations }) < current)
                    {
                        raised++;
                        continue;
                    }
                    operations[costOperation] = original;
                }
            }

            var bestOperation = -1;
            string? bestSlot = null;
            var bestValue = 0;
            var bestWeight = 0d;
            for (var i = 0; i < operations.Length; i++)
            {
                var spec = OperationRuntimeSpecCompiler.GetOrCompile(operations[i]);
                foreach (var slot in OperationRuntimeSpecCompiler.ExplicitFixedValueSlots(operations[i]))
                {
                    if (!IsSafeTrimSlot(spec.Opcode, spec.Variant, slot.Id)) continue;
                    var value = slot.BaseValue + slot.Offset;
                    if (value <= 1) continue;
                    var weight = SlotValueFor(slot.Id, value);
                    if (weight <= bestWeight) continue;
                    bestOperation = i;
                    bestSlot = slot.Id;
                    bestValue = value;
                    bestWeight = weight;
                }
            }

            if (bestOperation < 0 || bestSlot is null) break;
            var delta = bestSlot == "hits" ? 1 : Math.Max(1, (int)Math.Round(bestValue * 0.15));
            var next = Math.Max(1, bestValue - delta);
            if (next >= bestValue) break;
            if (!OperationRuntimeSpecCompiler.TryReplaceFixedValue(
                    operations[bestOperation], bestSlot, next, out var updated)) break;
            operations[bestOperation] = updated;
            shrunk++;
        }

        var trimmed = card with { Operations = operations };
        var after = CardValuator.Evaluate(trimmed);

        // P5：数值缩到不能再缩（例如 growth_block_parfait 最小 1 层也有 19 VP）时，
        // 换掉/移除价值最高的那个非触发 operation，再重新生成升级计划。
        if (after > band.Value.High)
        {
            var structural = ReplaceStructuralOutlier(trimmed, profileId, cardSlot);
            var structuralValue = CardValuator.Evaluate(structural);
            if (structuralValue < after)
            {
                Log.Info($"[价值对齐][收缩] 「{card.Name?.Chinese}」{card.Rarity} {card.Cost}c "
                         + $"V {before:F1}→{structuralValue:F1} 目标={band.Value.Low:F1}~{band.Value.High:F1} 结构替换");
                return structural;
            }
        }

        if (Math.Abs(after - before) < 0.01) return card;

        Log.Info($"[价值对齐][收缩] 「{card.Name?.Chinese}」{card.Rarity} {card.Cost}c "
                 + $"V {before:F1}→{after:F1} 目标={band.Value.Low:F1}~{band.Value.High:F1} "
                 + $"步={steps - 1} 缩={shrunk} 升={raised}");
        return SanitizeEnergyText(RebuildDescriptions(trimmed), profileId);
    }

    /// <summary>
    /// P5：结构型高值兜底。数值已经缩到最小仍高于目标带时，
    /// 移除价值最高的非触发 operation；如果只剩一个 operation，就换成
    /// 本卡型的通用低价值效果（攻击→6 伤、技能→5 挡、能力→抽 1）。
    /// 改完重新生成升级计划，避免升级索引指向旧 operation。
    /// </summary>
    private static GeneratedCard ReplaceStructuralOutlier(GeneratedCard card, string profileId, int cardSlot)
    {
        try
        {
            var band = CardValueProfile.For(card.Rarity, card.Cost);
            if (band is null) return card;

            var operations = card.Operations.ToList();
            var changed = false;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var current = CardValuator.Evaluate(card with { Operations = operations });
                if (current <= band.Value.High) break;

                var bestIndex = -1;
                var bestVp = 0d;
                for (var i = 0; i < operations.Count; i++)
                {
                    var operation = operations[i];
                    if (operation.Template.StartsWith("A:", StringComparison.Ordinal)
                        || operation.Scope is OperationScope.AbilityTrigger
                            or OperationScope.ConditionalTrigger or OperationScope.AbilityRule)
                        continue;

                    double vp;
                    try { vp = AtomVpTable.Vp(OperationRuntimeSpecCompiler.GetOrCompile(operation)); }
                    catch { continue; }
                    if (vp <= bestVp) continue;
                    bestIndex = i;
                    bestVp = vp;
                }

                if (bestIndex < 0) break;
                if (operations.Count == 1)
                {
                    operations[0] = ToOperation(card.Type switch
                    {
                        GeneratedCardType.Attack => SakiAtomLibrary.Damage(6),
                        GeneratedCardType.Power => SakiAtomLibrary.Draw(1),
                        _ => SakiAtomLibrary.Block(5)
                    });
                }
                else
                {
                    operations.RemoveAt(bestIndex);
                }

                changed = true;
            }

            if (!changed) return card;

            var result = card with { Operations = operations };
            result = RegenerateUpgrade(result, profileId, cardSlot);
            var value = CardValuator.Evaluate(result);
            Log.Info($"[价值对齐][结构替换] 「{card.Name?.Chinese}」{card.Rarity} {card.Cost}c "
                     + $"V {CardValuator.Evaluate(card):F1}→{value:F1} "
                     + $"ops {card.Operations.Count}→{operations.Count}");
            return SanitizeEnergyText(RebuildDescriptions(result), profileId);
        }
        catch { return card; }
    }

    /// <summary>挑一个还有空间的纯代价槽，升 1 点（压力升 2 点）。</summary>
    private static bool TryRaiseCostSlot(GeneratorOperation[] operations,
        out int operationIndex, out string slotId, out int nextValue)
    {
        for (var i = 0; i < operations.Length; i++)
        {
            var spec = OperationRuntimeSpecCompiler.GetOrCompile(operations[i]);
            foreach (var slot in OperationRuntimeSpecCompiler.ExplicitFixedValueSlots(operations[i]))
            {
                if (!IsCostSlot(spec.Opcode, spec.Variant, slot.Id)) continue;
                var value = slot.BaseValue + slot.Offset;
                var cap = CostCapFor(spec.Opcode, spec.Variant, slot.Id);
                if (value >= cap) continue;
                operationIndex = i;
                slotId = slot.Id;
                nextValue = Math.Min(cap, value + (slot.Id == "pressure" ? 2 : 1));
                return true;
            }
        }

        operationIndex = -1;
        slotId = string.Empty;
        nextValue = 0;
        return false;
    }

    /// <summary>
    /// 纯代价槽：升它只会让卡更弱，不会同时加收益。
    /// 注意 `csk_pay_pressure|weak_all` 的 cost 同时是虚弱层数的基数，不能升。
    /// </summary>
    private static bool IsCostSlot(string opcode, string variant, string slotId) => (opcode, variant, slotId) switch
    {
        ("csk_pay_pressure", "weak_all", _) => false,
        ("csk_pay_pressure", _, "cost") => true,
        ("csk_parfait_combo", _, "cups") => true,
        ("csk_parfait", "buy", "count") => true,
        ("csk_status", _, "count") => true,
        ("csk_deck", "pressure_max_hp", "pressure") => true,
        ("csk_deck", "return_to_hand", "count") => true,
        ("csk_deck", "relieve_draw", "pressure") => true,
        ("csk_parfait", "eat", "amount") => true,
        _ => false
    };

    /// <summary>代价槽的上限：再高就变成不可用的卡。</summary>
    private static int CostCapFor(string opcode, string variant, string slotId) => (opcode, variant, slotId) switch
    {
        ("csk_pay_pressure", _, "cost") => 6,
        ("csk_parfait_combo", _, "cups") => 4,
        ("csk_parfait", "buy", "count") => 20,
        ("csk_status", _, "count") => 3,
        ("csk_deck", _, "pressure") => 10,
        ("csk_deck", "return_to_hand", "count") => 10,
        ("csk_parfait", "eat", "amount") => 4,
        _ => 3
    };

    private sealed record Rider(string Id, GeneratedCardType Type, ComponentAtom Atom, double Vp,
        bool AoeOnly = false, bool NeedsSingleTarget = false);

    /// <summary>
    /// P3 rider 池：固定值、固定 VP 的原生/通用效果。
    /// 只用原生效果（伤害/格挡/抽牌/能量/虚弱/易伤/治疗），不涉及机制关键词，
    /// 避免追加后卡面/关键词/运行时对不上。
    /// </summary>
    private static readonly Rider[] Riders =
    [
        // P6：小 rider 用来补「结构替换后只差 0.6~1.4 VP」的 Ancient/Rare 卡，
        // 避免因为最小 rider 5.75 VP 太大而卡在带外。
        new("Damage2", GeneratedCardType.Attack, SakiAtomLibrary.Damage(2), 1.0,
            NeedsSingleTarget: true),
        new("Damage3", GeneratedCardType.Attack, SakiAtomLibrary.Damage(3), 1.5,
            NeedsSingleTarget: true),
        new("Damage4", GeneratedCardType.Attack, SakiAtomLibrary.Damage(4), 2.0,
            NeedsSingleTarget: true),
        new("Damage6", GeneratedCardType.Attack, SakiAtomLibrary.Damage(6), 3.0,
            NeedsSingleTarget: true),
        new("Damage8", GeneratedCardType.Attack, SakiAtomLibrary.Damage(8), 4.0,
            NeedsSingleTarget: true),
        new("DamageAll4", GeneratedCardType.Attack, SakiAtomLibrary.DamageAll(4), 2.6, AoeOnly: true),
        new("Weak1", GeneratedCardType.Attack, SakiAtomLibrary.ApplyWeak(1), 2.0,
            NeedsSingleTarget: true),
        new("Vulnerable1", GeneratedCardType.Attack, SakiAtomLibrary.ApplyVulnerable(1), 2.0,
            NeedsSingleTarget: true),
        new("AttackDraw1", GeneratedCardType.Attack, SakiAtomLibrary.Draw(1), 2.75),

        new("Block2", GeneratedCardType.Skill, SakiAtomLibrary.Block(2), 2.0),
        new("Block3", GeneratedCardType.Skill, SakiAtomLibrary.Block(3), 3.0),
        new("Block4", GeneratedCardType.Skill, SakiAtomLibrary.Block(4), 4.0),
        new("Block6", GeneratedCardType.Skill, SakiAtomLibrary.Block(6), 6.0),
        new("Block8", GeneratedCardType.Skill, SakiAtomLibrary.Block(8), 8.0),
        new("SkillDraw1", GeneratedCardType.Skill, SakiAtomLibrary.Draw(1), 2.75),
        new("Heal1", GeneratedCardType.Skill, SakiAtomLibrary.Heal(1), 0.6),
        new("Heal2", GeneratedCardType.Skill, SakiAtomLibrary.Heal(2), 1.2),
        new("Heal3", GeneratedCardType.Skill, SakiAtomLibrary.Heal(3), 1.8),
        new("SkillEnergy1", GeneratedCardType.Skill, SakiAtomLibrary.Energy(1), 5.75),

        new("PowerHeal1", GeneratedCardType.Power, SakiAtomLibrary.Heal(1), 0.6),
        new("PowerHeal2", GeneratedCardType.Power, SakiAtomLibrary.Heal(2), 1.2),
        new("PowerDraw1", GeneratedCardType.Power, SakiAtomLibrary.Draw(1), 2.75),
        new("PowerEnergy1", GeneratedCardType.Power, SakiAtomLibrary.Energy(1), 5.75)
    ];

    /// <summary>
    /// P3：卡的价值低于本体目标带时，追加 1~2 个固定值 rider（复合效果），
    /// 而不是把原数值放大。
    ///
    /// 限制：
    ///   · 触发型 **Power** 允许补「打出时立即生效」的 rider（抽牌/能量），不改触发器本身；
    ///     触发型 Attack/Skill 暂跳过，避免语义混乱；
    ///   · 最多 5 个 operation（P4 从 4 放宽，给低价值 4-op 卡留空间），最多追加 2 个 rider；
    ///   · 只用原生效果，不动机制关键词；
    ///   · 同类 template 已有就不重复加（例如已经有 T:D 就不再加 Damage）。
    /// </summary>
    private static GeneratedCard AppendRidersToBand(GeneratedCard card, string profileId)
    {
        try { return AppendRidersToBandCore(card, profileId); }
        catch { return card; }
    }

    private static GeneratedCard AppendRidersToBandCore(GeneratedCard card, string profileId)
    {
        var band = CardValueProfile.For(card.Rarity, card.Cost);
        if (band is null) return card;
        const int maxOperations = 5;
        if (card.Operations.Count >= maxOperations) return card;

        var hasTrigger = card.Operations.Any(operation =>
            operation.Template.StartsWith("A:", StringComparison.Ordinal)
            || operation.Scope is OperationScope.AbilityTrigger
                or OperationScope.ConditionalTrigger or OperationScope.AbilityRule);
        // ★ 2026-11-12 P7：触发型 Attack/Skill 也允许补「立即生效」的 rider；
        //   之前跳过导致乐奈一堆 V=0 的触发卡。只补伤害/格挡/抽牌/能量，不改触发器。

        var before = CardValuator.Evaluate(card);
        if (before >= band.Value.Low) return card;

        var operations = card.Operations.ToList();
        var added = new List<string>();
        for (var attempt = 0; attempt < 2 && operations.Count < maxOperations; attempt++)
        {
            var current = CardValuator.Evaluate(card with { Operations = operations });
            var gap = band.Value.Low - current;
            if (gap <= 0.5) break;

            var rider = Riders
                .Where(candidate => RiderAllowed(candidate, card, operations))
                .Where(candidate => candidate.Vp <= gap * 1.5 + 0.5)
                .OrderByDescending(candidate => candidate.Vp)
                .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (rider is null) break;

            operations.Add(ToOperation(rider.Atom));
            added.Add(rider.Id);
        }

        if (added.Count == 0) return card;

        var result = card with { Operations = operations };
        var after = CardValuator.Evaluate(result);
        Log.Info($"[价值对齐][补效果] 「{card.Name?.Chinese}」{card.Rarity} {card.Cost}c "
                 + $"V {before:F1}→{after:F1} 目标={band.Value.Low:F1}~{band.Value.High:F1} "
                 + $"加={string.Join('+', added)}{(hasTrigger ? " 触发=是" : string.Empty)}");
        return SanitizeEnergyText(RebuildDescriptions(result), profileId);
    }

    private static bool RiderAllowed(Rider rider, GeneratedCard card, IReadOnlyList<GeneratorOperation> operations)
    {
        if (rider.Type != card.Type) return false;
        if (rider.AoeOnly && card.Target == TargetMode.SingleEnemy) return false;
        if (rider.NeedsSingleTarget && card.Target != TargetMode.SingleEnemy) return false;
        if (operations.Any(operation => operation.Template == rider.Atom.Template)) return false;
        return true;
    }

    private static GeneratorOperation ToOperation(ComponentAtom atom) =>
        new(atom.Template, atom.Scope, atom.ChineseText, new Dictionary<string, int>(),
            RequiresSingleTarget: atom.RequiresSingleTarget,
            RuntimeSpec: atom.RuntimeSpec,
            LocalizedText: atom.LocalizedText);

    /// <summary>安全槽的估算 VP，用来决定先缩哪个（只用于排序，不参与最终判定）。</summary>
    private static double SlotValueFor(string slotId, int value) => slotId switch
    {
        "damage" => value * 0.5,
        "block" => value * 1.0,
        "heal" => value * 0.6,
        "draw" => value * 2.75,
        "energy" => value * 5.75,
        "hits" => value * 2.0,
        // 计数/层数/成长：没法定价，按 1 VP/点排序；真正的停止条件仍是 CardValuator。
        _ => value * 1.0
    };

    /// <summary>
    /// P2 允许缩小的槽位。核心原则：**只缩「收益」，不缩「代价」**。
    ///   · 不缩 cost（费用/压力代价）—— 缩了反而更强；
    ///   · 不缩 csk_status 的 count（状态牌是负面，缩了更强）；
    ///   · 不缩 parfait_combo 的 cups / parfait.buy 的金币（食用杯数/购买价是代价）；
    ///   · 其余 payoff 槽（伤害/格挡/成长/能力层/猫咪/音符/演奏次数/压力/减益层）都允许缩。
    /// </summary>
    private static bool IsSafeTrimSlot(string opcode, string variant, string slotId)
    {
        // 芭菲组合的 payoff 是「杯数成本」的回报，缩它只会让净价值更负。
        if (opcode == SakiComponents.ParfaitComboOpcode) return false;
        if (slotId is "damage" or "block" or "heal" or "draw" or "energy" or "hits") return true;
        return (opcode, variant) switch
        {
            ("csk_growth", _) => slotId == "growth",
            ("csk_power", _) => slotId == "amount",
            ("csk_token", _) => slotId is "count" or "amount",
            ("csk_parfait", "charge" or "consume_as_parfait") => slotId is "amount" or "count",
            ("csk_parfait_combo", _) => slotId is "damage" or "block" or "heal",
            ("csk_play_chord", _) => slotId == "count",
            ("csk_pressure", _) => slotId == "amount",
            ("csk_condition", _) => slotId is "count" or "amount" or "shrink",
            ("csk_pay_pressure", _) => slotId is "count" or "extra" or "hits" or "multiplier",
            ("csk_note", _) => slotId == "count",
            ("csk_recall", _) => slotId == "count",
            ("csk_forget", _) => slotId == "count",
            ("csk_scale", _) => slotId == "count",
            ("csk_gold", _) => slotId == "count",
            _ => false
        };
    }

    /// <summary>这张卡含「获得能力」操作（csk_power）。</summary>
    /// <summary>
    /// 最后一道数值兜底：把每个显式数值槽压到上限内。
    /// 用途：AA 的 RaiseNumericRewardsToTopRarityFloor 会为「够稀有度」反复抬高数值，
    /// 自定义原子（芭菲/和声/猫咪/倍率）会被抬到两位数。
    /// ⚠️ 只改 operation 不会改卡面（ChineseDescription 是冻结字符串），
    ///    调用方必须在之后走 <see cref="RebuildDescriptions"/> 重建描述。
    /// </summary>
    private static GeneratedCard ClampCardOperations(GeneratedCard card)
    {
        try
        {
            var operations = card.Operations.ToArray();
            var changed = false;
            for (var i = 0; i < operations.Length; i++)
            {
                var spec = OperationRuntimeSpecCompiler.GetOrCompile(operations[i]);
                foreach (var slot in OperationRuntimeSpecCompiler.ExplicitFixedValueSlots(operations[i]))
                {
                    var current = slot.BaseValue + slot.Offset;
                    if (current <= 0) continue;
                    var cap = NumericCapFor(spec.Opcode, slot.Id);
                    if (current <= cap) continue;
                    if (OperationRuntimeSpecCompiler.TryReplaceFixedValue(operations[i], slot.Id, cap, out var updated))
                    {
                        operations[i] = updated;
                        changed = true;
                    }
                }
            }

            return changed ? card with { Operations = operations } : card;
        }
        catch { return card; }
    }

    /// <summary>
    /// 按 operations 重建卡面描述（含升级描述）。
    ///
    /// 为什么必须做：<see cref="GeneratedCard.ChineseDescription"/> 是**冻结字符串**，
    /// 而 <see cref="ClampCardOperations"/> 只改 operation 的数值槽。
    /// AA 的 `CardDescriptionRenderer.Render` / `EnglishCardDescriptionRenderer.Render`
    /// 在 0.3.156 里都是 public，且 AA 自己也是这么重建的
    /// （ComponentAssemblyGenerator.cs:1045-1063）。
    /// </summary>
    private static GeneratedCard RebuildDescriptions(GeneratedCard card)
    {
        try
        {
            var chinese = AppendConditionDescriptions(
                CardDescriptionRenderer.Render(card.Operations), card.Operations);
            // ★ P8 性能：英文描述不在中文卡面显示，重建英文占每张卡一次全量渲染；
            //   这里保留生成时的英文，只重建中文（英文数值可能略滞后，但可接受）。
            var english = card.EnglishDescription;

            CardUpgradePlan? upgrade = null;
            if (card.Upgrade is { } plan)
            {
                var upgradedOperations = CardUpgradeGenerator.ApplyEffectsToOperations(
                    card.Operations, plan.Effects);
                var upgradedChinese = AppendConditionDescriptions(
                    CardDescriptionRenderer.Render(upgradedOperations), upgradedOperations);
                var upgradedEnglish = plan.UpgradedEnglishDescription;
                upgrade = plan with
                {
                    UpgradedChineseDescription = upgradedChinese,
                    UpgradedEnglishDescription = upgradedEnglish
                };
            }

            return card with
            {
                ChineseDescription = chinese,
                EnglishDescription = english,
                Upgrade = upgrade
            };
        }
        catch { return card; }
    }

    /// <summary>
    /// AA 的 CardDescriptionRenderer 不认识 csk:condition_*，会只渲染前半段。
    /// 这里把「若…」/「斩杀时…」条件从句补回卡面，避免生成卡描述漏掉机制。
    /// </summary>
    private static string AppendConditionDescriptions(string rendered,
        IReadOnlyList<GeneratorOperation> operations)
    {
        foreach (var operation in operations)
        {
            if (!operation.Template.StartsWith("csk:condition", StringComparison.Ordinal)) continue;
            var text = operation.ChineseText;
            if (string.IsNullOrWhiteSpace(text)) continue;
            var marker = text.IndexOf("若", StringComparison.Ordinal);
            if (marker < 0) marker = text.IndexOf("斩杀", StringComparison.Ordinal);
            if (marker < 0) continue;
            var clause = text[marker..].Trim();
            if (clause.Length == 0) continue;
            // AA 有时已经把条件渲染成「如果…」；把两种写法归一后再判重，
            // 否则会在卡面上出现「如果…；若…」两份条件。
            var normalizedRendered = rendered.Replace("如果", "若", StringComparison.Ordinal);
            var normalizedClause = clause.Replace("如果", "若", StringComparison.Ordinal);
            if (normalizedRendered.Contains(normalizedClause, StringComparison.Ordinal)) continue;
            rendered = rendered.TrimEnd('。', '；') + "；" + clause;
        }

        return rendered;
    }

    private static int NumericCapFor(string opcode, string slotId)
    {
        var id = slotId ?? string.Empty;
        if (id == "damage") return 45;
        if (id == "block") return 40;
        if (id == "hits") return 6;
        if (id == "draw") return 4;
        if (id == "energy") return 3;
        // ★ 2026-11-11：金币上限从 6 抬到 18 —— 本体「客服工作」就是 10(15) 金币；
        //   更关键的是 AA 的 Rare 1 费下限约 1615 单位、1 金币≈100 单位，
        //   夹到 15 会让「1 费金卡只有 15 金币」仍低于下限（实测 net=1500 < 1615）。
        if (opcode == SakiComponents.GoldOpcode) return 18;
        if (opcode != null && opcode.Contains("power", StringComparison.Ordinal)) return 4;
        if (opcode != null && opcode.Contains("scale", StringComparison.Ordinal)) return 3;
        return 6;   // 层数 / 张数 / 杯数 等一律个位数
    }

    private static bool HasPowerOperation(GeneratedCard card)
    {
        try
        {
            foreach (var operation in card.Operations)
                if (OperationRuntimeSpecCompiler.GetOrCompile(operation).Opcode == SakiPowerCatalog.Opcode)
                    return true;
        }
        catch { }
        return false;
    }

    /// <summary>只有素效果（单操作且不是 mod 机制）。</summary>
    private static bool IsBareSingleEffect(GeneratedCard card)
    {
        try
        {
            if (card.Operations.Count != 1) return false;
            var opcode = OperationRuntimeSpecCompiler.GetOrCompile(card.Operations[0]).Opcode;
            if (string.IsNullOrEmpty(opcode)) return false;
            if (opcode.StartsWith("csk_", StringComparison.Ordinal)) return false;
            return opcode is "damage" or "block" or "draw" or "gold" or "energy" or "heal" or "power";
        }
        catch { return false; }
    }

    /// <summary>只有「食用芭菲」这一个负面前置、没有收益。</summary>
    private static bool IsParfaitEatOnly(GeneratedCard card)
    {
        try
        {
            if (card.Operations.Count != 1) return false;
            var spec = OperationRuntimeSpecCompiler.GetOrCompile(card.Operations[0]);
            return spec.Opcode == SakiComponents.ParfaitOpcode
                   && spec.Variant == SakiComponents.ParfaitEatVariant;
        }
        catch { return false; }
    }

    private static bool HasCustomNumberedBeyondFirst(GeneratedCard card)
    {
        for (var index = 1; index < card.Operations.Count; index++)
        {
            var spec = OperationRuntimeSpecCompiler.GetOrCompile(card.Operations[index]);
            if (spec.Opcode.StartsWith("csk_", StringComparison.Ordinal) && spec.Values.Count > 0)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 这张卡是不是「触发型 + 结束回合」的坏组合
    /// （例如「在你的回合开始时，结束你的回合」—— 会逻辑死掉）。
    /// </summary>
    private static bool HasTriggerWithEndTurn(GeneratedCard card)
    {
        var hasTrigger = false;
        var hasEndTurn = false;
        foreach (var operation in card.Operations)
        {
            var spec = OperationRuntimeSpecCompiler.GetOrCompile(operation);
            if (operation.Scope == OperationScope.AbilityTrigger) hasTrigger = true;
            if (spec.Opcode == SakiComponents.ExtremeOpcode) hasEndTurn = true;
        }
        return hasTrigger && hasEndTurn;
    }

    /// <summary>
    /// 类型与效果错配：技能/能力牌却带「造成X点伤害」，或攻击牌完全没有伤害。
    /// 依据：本体卡池里这种卡数量为 0（2026-11-10 实测）。
    /// </summary>
    private static bool IsTypeMismatched(GeneratedCard card)
    {
        var desc = card.ChineseDescription ?? string.Empty;
        var hasDamage = desc.Contains("造成", StringComparison.Ordinal)
            && desc.Contains("伤害", StringComparison.Ordinal);
        return card.Type == GeneratedCardType.Skill && hasDamage;
    }

    /// <summary>
    /// 生成 → 结构化 → 抹辉星费 → 费用校准。
    /// ★ 必须把 StripStarCost 放进这条链里：重抽（机制守卫等）发生在原 Strips 之后，
    ///   否则重抽出来的卡会重新带回「1费1星」（2026-11-10 实机「别再舞会」）。
    /// </summary>
    /// <summary>
    /// 2026-11-11 用户第 5 条：基础效果（伤害/格挡/抽牌）出现得太少，
    /// 卡经常只有一条 mod 机制。这里在生成后按概率补一个基础效果：
    ///   · 只有 1 个 operation 的机制卡：70% 补；
    ///   · 已经有 2+ operation 但仍没有基础效果的：45% 补。
    /// 纯基础卡、已经有基础效果的复合卡不补；最多 5 个 operation。
    /// </summary>
    /// <summary>
    /// P8：纯原版卡不再重抽，直接补一条本角色机制，保证「不会只有纯原版效果」。
    /// </summary>
    private static GeneratedCard EnsureMechanic(GeneratedCard card, string profileId)
    {
        try
        {
            if (HasMechanicOperation(card)) return card;

            ComponentAtom? atom = profileId switch
            {
                RandomProfiles.Saki => PressureFiller(card),
                RandomProfiles.Anon => SakiAtomLibrary.AddNote(1, NoteKind.Skill),
                RandomProfiles.Rana => SakiAtomLibrary.NekoRetain(1),
                _ => null
            };
            if (atom is null) return card;

            var operation = ToOperation(atom);
            if (card.Operations.Any(existing => existing.Template == operation.Template)) return card;

            // 满编卡（8 op）不再跳过：替换最后一个安全 op，保证不会留纯原版。
            if (card.Operations.Count >= 8)
            {
                var operations = card.Operations.ToList();
                var replaceIndex = operations.FindLastIndex(op =>
                    !op.Template.StartsWith("A:", StringComparison.Ordinal)
                    && !op.Template.StartsWith("csk:condition", StringComparison.Ordinal));
                if (replaceIndex < 0) replaceIndex = operations.Count - 1;
                operations[replaceIndex] = operation;
                Log.Info($"[补机制] 「{card.Name?.Chinese}」{card.Rarity} {card.Cost}c "
                         + $"满编替换 op#{replaceIndex} 加={atom.Template}");
                return card with { Operations = operations.ToArray() };
            }

            Log.Info($"[补机制] 「{card.Name?.Chinese}」{card.Rarity} {card.Cost}c "
                     + $"ops {card.Operations.Count}→{card.Operations.Count + 1} 加={atom.Template}");
            return card with { Operations = card.Operations.Append(operation).ToArray() };
        }
        catch { return card; }
    }

    /// <summary>
    /// P1：AA 会把芭菲组合的 cups 抬到 6，6 杯成本 54 VP 会把整卡估成 0。
    /// 超过 2 杯的组合直接换成 1 杯格挡原子，保证卡可玩、不为 0 价值。
    /// </summary>
    private static GeneratedCard SanitizeParfaitCosts(
        GeneratedCard card, string profileId, out bool changed)
    {
        changed = false;
        try
        {
            var operations = card.Operations.ToList();
            var replaced = false;
            for (var i = operations.Count - 1; i >= 0; i--)
            {
                var spec = OperationRuntimeSpecCompiler.GetOrCompile(operations[i]);
                if (spec.Opcode != SakiComponents.ParfaitComboOpcode) continue;
                var cups = spec.Values.FirstOrDefault(slot => slot.Id == "cups")?.BaseValue ?? 0;
                if (cups <= 2 && AtomVpTable.Vp(spec) >= 0) continue;
                operations.RemoveAt(i);
                changed = true;
                if (!replaced)
                {
                    // 保留角色机制，再补一条基础效果；不能只补原生效果（会变成纯原版卡）。
                    if (MechanicAtomFor(profileId, card) is { } mechanic)
                        operations.Add(ToOperation(mechanic));
                    operations.Add(ToOperation(card.Type == GeneratedCardType.Attack
                        ? SakiAtomLibrary.Damage(4)
                        : SakiAtomLibrary.Block(4)));
                    replaced = true;
                }
            }

            if (!changed) return card;
            if (operations.Count == 0)
                operations.Add(ToOperation(SakiAtomLibrary.Block(4)));
            Log.Info($"[芭菲降费] 「{card.Name?.Chinese}」{card.Rarity} {card.Cost}c "
                     + $"ops {card.Operations.Count}→{operations.Count}");
            return card with { Operations = operations.ToArray() };
        }
        catch { return card; }
    }

    private static ComponentAtom? MechanicAtomFor(string profileId, GeneratedCard? card = null) => profileId switch
    {
        RandomProfiles.Saki => PressureFiller(card),
        RandomProfiles.Anon => SakiAtomLibrary.AddNote(1, NoteKind.Skill),
        RandomProfiles.Rana => SakiAtomLibrary.NekoRetain(1),
        _ => null
    };

    /// <summary>
    /// 「补机制」兜底的压力原子：**获得 / 给予单体 / 给予全体** 三种形态 + 1~6 层随机，
    /// 用生成期 RNG（同 seed 确定）选，所以不会永远是「获得1层压力」。
    /// 2026-11-13 用户口径：获得压力/给予压力的数值要随机化。
    /// </summary>
    private static ComponentAtom PressureFiller(GeneratedCard? card)
    {
        var amount = FillerPressureAmount(card);
        if (card is null) return SakiAtomLibrary.PressureSelf(amount);

        var roll = GenerationRng.Next(100);
        if (card.Target == TargetMode.SingleEnemy)
        {
            // 单体卡：55% 获得（代价） / 45% 给予目标（收益）。
            return roll < 55
                ? SakiAtomLibrary.PressureSelf(amount)
                : SakiAtomLibrary.Pressure(amount);
        }

        // 非单体卡：60% 获得（代价） / 40% 给予全体。
        return roll < 60
            ? SakiAtomLibrary.PressureSelf(amount)
            : SakiAtomLibrary.PressureAll(amount);
    }

    /// <summary>
    /// 兜底压力的层数：按稀有度/费用给基准，再 ±1 波动（1~6）。
    /// 2026-11-13 用户口径：获得/给予压力的数值要随机化。
    /// 这里用确定性分档 + 生成期 RNG，保证同 seed 的生成/联机一致。
    /// </summary>
    private static int FillerPressureAmount(GeneratedCard? card)
    {
        if (card is null) return 1;
        var tier = card.Rarity switch
        {
            GeneratedRarity.Basic => 1,
            GeneratedRarity.Common => 2,
            GeneratedRarity.Uncommon => 3,
            GeneratedRarity.Rare => 4,
            _ => 5
        };
        if (card.Cost >= 2) tier++;
        return Math.Clamp(tier + GenerationRng.Next(3) - 1, 1, 6);
    }

    private static GeneratedCard EnsureKeywordIdentity(GeneratedCard card, string profileId)
    {
        try
        {
            if (HasMechanicOperation(card)) return card;
            var keyword = profileId switch
            {
                RandomProfiles.Anon => card.Type == GeneratedCardType.Attack
                    ? CuteSakikoMod.CuteSakikoModCode.Others.CutesakiKeywords.Playguitar
                    : CuteSakikoMod.CuteSakikoModCode.Others.CutesakiKeywords.NoNote,
                RandomProfiles.Rana => CuteSakikoMod.CuteSakikoModCode.Others.CutesakiKeywords.RanaLive,
                _ => null
            };
            if (keyword is null) return card;
            var keywords = card.CustomKeywords ?? [];
            if (keywords.Contains(keyword)) return card;
            Log.Info($"[补关键词] 「{card.Name?.Chinese}」{card.Rarity} {card.Cost}c 加={keyword}");
            return card with { CustomKeywords = [.. keywords, keyword] };
        }
        catch { return card; }
    }

    private static GeneratedCard AppendBasicEffect(GeneratedCard card)    {
        try
        {
            if (card.Operations.Count >= 5) return card;

            var hasMechanic = false;
            var hasBase = false;
            foreach (var operation in card.Operations)
            {
                OperationRuntimeSpec spec;
                try { spec = OperationRuntimeSpecCompiler.GetOrCompile(operation); }
                catch { continue; }

                if (spec.Opcode.StartsWith("csk_", StringComparison.Ordinal)
                    || spec.Opcode.StartsWith("csk:", StringComparison.Ordinal))
                    hasMechanic = true;
                if (spec.Opcode is "N:D" or "N:B" or "N:Draw" or "N:E" or "N:Heal"
                    or "N:NextTurnDraw" or "N:NextTurnEnergy")
                    hasBase = true;
            }

            if (!hasMechanic || hasBase) return card;

            var chance = card.Operations.Count == 1 ? 0.70 : 0.45;
            if (GenerationRng.NextDouble() > chance) return card;

            ComponentAtom? atom = card.Type switch
            {
                GeneratedCardType.Attack => card.Target == TargetMode.SingleEnemy
                    ? SakiAtomLibrary.Damage(6)
                    : SakiAtomLibrary.DamageAll(4),
                GeneratedCardType.Skill => SakiAtomLibrary.Block(5),
                GeneratedCardType.Power => SakiAtomLibrary.Draw(1),
                _ => null
            };
            if (atom is null) return card;

            // ★ 2026-11-12 用户第 4 条：攻击/技能牌几乎拿不到「获得能量」。
            //   基础复合时给小概率直接补 1 点能量，让三类型都能出现能量词条。
            if (card.Type is GeneratedCardType.Attack or GeneratedCardType.Skill
                && GenerationRng.NextDouble() < 0.15)
                atom = SakiAtomLibrary.Energy(1);

            var basicOperation = ToOperation(atom);
            if (card.Operations.Any(existing => existing.Template == basicOperation.Template)) return card;

            Log.Info($"[基础复合] 「{card.Name?.Chinese}」{card.Rarity} {card.Cost}c "
                     + $"ops {card.Operations.Count}→{card.Operations.Count + 1} 加={atom.Template}");
            return card with { Operations = card.Operations.Append(basicOperation).ToArray() };
        }
        catch { return card; }
    }

    private static GeneratedCard Prepare(GeneratedCard raw) =>
        CardCostCalibration.Apply(StripStarCost(OperationRuntimeSpecCompiler.Attach(raw)));

    /// <summary>
    /// 把描述里残留的能量 formatter 换成纯文本。
    ///
    /// 2026-11-11 实测：AA 在**采样过的**数值槽上会把「获得N点能量。」改写成
    /// `获得{energyPrefix:energyIcons(N)}。`，而外部包的卡面不参与该 formatter 求值 → 直接漏字。
    /// 我们注册的命名本地化模板只覆盖「未采样」的那份原子，覆盖不到采样变体，
    /// 所以在**卡面文本**这一层兜底替换：图标拿不到，就老老实实显示「N点能量」。
    /// </summary>
    private static GeneratedCard SanitizeEnergyText(GeneratedCard card, string profileId)
    {
        // ★ 2026-11-12 用户反馈：自定义 [img] 能量图标在卡面上会被显示层丢掉，
        //   只剩「获得2。」。现在**保留游戏自己的 {energyPrefix:energyIcons(N)} formatter**，
        //   交给 ChaosCardModel 的卡面渲染按当前角色取本体/自定义能量图标；
        //   这比在生成期硬塞 [img] 路径可靠（Power 牌本来就是这么显示正确的）。
        var pressureCost = System.Text.RegularExpressions.Regex
            .Match(card.ChineseDescription ?? string.Empty, @"消耗(\d+)层")
            .Groups[1].Value;
        if (string.IsNullOrEmpty(pressureCost)) pressureCost = "1";

        string Fix(string? text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
            // ★ 2026-11-11 用户要求：不要硬写「N点能量」，要原版能量图标。
            //   AA 的 CardTextStyle 会把「N点能量」规范成 {energyPrefix:energyIcons(N)}；
            //   这里再把它换成游戏自己的能量图标资源（和 AA 训练房同一路径）。
            var normalized = ChaosCardGenerator.CardTextStyle.NormalizeRenderedChineseEnergyNotation(text);
            // 保留 formatter，让卡面渲染自己求值；不要再替换成 [img]，否则会被丢标签。
            var fixedText = normalized;
            // ★ 2026-11-11 用户第 4 条：csk_pay_pressure 的运行期固定扣 1 层压力，
            //   但 AA 的数值放大/升级会把卡面文字里的「消耗N层」改成 6/7，造成负升级假象。
            //   这里把卡面代价钉回 1，和运行时一致。
            fixedText = System.Text.RegularExpressions.Regex.Replace(
                fixedText, @"消耗\d+层(?=\[gold\]压力)", $"消耗{pressureCost}层");
            fixedText = System.Text.RegularExpressions.Regex.Replace(
                fixedText, @"spend \d+", $"spend {pressureCost}",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // ★ 2026-11-11 用户第 6 条：卡牌本地化染色规范。
            //   · 手牌/弃牌堆/抽牌堆/消耗堆/遗忘堆 → [gold]
            //   · 回复生命 → [green]
            //   · 数值不要 [blue] 包裹
            fixedText = System.Text.RegularExpressions.Regex.Replace(
                fixedText, @"(?<!\[gold\])(手牌|弃牌堆|抽牌堆|消耗堆|遗忘堆)(?!\[/gold\])",
                "[gold]$1[/gold]");
            fixedText = System.Text.RegularExpressions.Regex.Replace(
                fixedText, @"(?<!\[green\])回复(\d+)点生命(?!\[/green\])",
                "[green]回复$1点生命[/green]");
            fixedText = System.Text.RegularExpressions.Regex.Replace(
                fixedText, @"\[blue\]([^\[\]]*\d+[^\[\]]*)\[/blue\]", "$1");

            return fixedText;
        }

        // ★ 2026-11-11 用户第 2 条：获得回忆/欺诈/逃避/猫咪的卡，升级描述要显示「回忆+」等。
        //   这一步必须放在 RebuildDescriptions 之后（SanitizeEnergyText 是流水线最后一步），
        //   否则会被 RebuildDescriptions 用 operations 重算覆盖。
        string GrantPlus(string? text)
        {
            var result = text ?? string.Empty;
            bool Repl(string name)
            {
                if (result.Contains(name + "+", StringComparison.Ordinal)) return false;
                if (!result.Contains(name, StringComparison.Ordinal)) return false;
                result = result.Replace(name, name + "+", StringComparison.Ordinal);
                return true;
            }

            foreach (var operation in card.Operations)
            {
                OperationRuntimeSpec spec;
                try { spec = OperationRuntimeSpecCompiler.GetOrCompile(operation); }
                catch { continue; }
                if (spec.Opcode == SakiComponents.RecallOpcode) Repl("回忆");
                else if (spec.Opcode == SakiComponents.StatusOpcode)
                {
                    Repl("欺诈");
                    Repl("逃避");
                }
                else if (spec.Opcode == SakiComponents.TokenOpcode
                         && spec.Variant == SakiComponents.TokenNekoVariant)
                    Repl("猫咪");
            }

            if (!string.Equals(result, text, StringComparison.Ordinal))
                Log.Info($"[升级文本] 「{card.Name?.Chinese}」{result}");
            return result;
        }

        var chinese = Fix(card.ChineseDescription);
        var english = Fix(card.EnglishDescription);
        var upgrade = card.Upgrade;
        if (upgrade is not null)
        {
            var upgradedChinese = GrantPlus(Fix(upgrade.UpgradedChineseDescription));
            var upgradedEnglish = Fix(upgrade.UpgradedEnglishDescription);
            if (!ReferenceEquals(upgradedChinese, upgrade.UpgradedChineseDescription)
                || !ReferenceEquals(upgradedEnglish, upgrade.UpgradedEnglishDescription))
            {
                upgrade = upgrade with
                {
                    UpgradedChineseDescription = upgradedChinese,
                    UpgradedEnglishDescription = upgradedEnglish
                };
            }
        }

        if (ReferenceEquals(chinese, card.ChineseDescription)
            && ReferenceEquals(english, card.EnglishDescription)
            && ReferenceEquals(upgrade, card.Upgrade))
            return card;

        return card with { ChineseDescription = chinese, EnglishDescription = english, Upgrade = upgrade };
    }

    /// <summary>抽牌数额是否离谱（2026-11-11 实测出现「抽 41 张牌」）。</summary>
    private static bool HasAbsurdDraw(GeneratedCard card)
    {
        foreach (var operation in card.Operations)
        {
            var spec = OperationRuntimeSpecCompiler.GetOrCompile(operation);
            var looksLikeDraw = spec.Opcode.Contains("draw", StringComparison.Ordinal)
                                || spec.Variant.Contains("draw", StringComparison.Ordinal)
                                || spec.Values.Any(v => v.Id.Contains("draw", StringComparison.Ordinal)
                                                        || v.Id.Contains("card", StringComparison.Ordinal));
            if (!looksLikeDraw) continue;
            foreach (var value in spec.Values)
                if (value.BaseValue > 6) return true;
        }
        return false;
    }

    /// <summary>
    /// 能量数额是否离谱（2026-11-11 实测：出现「获得27点能量」，但运行时处理器只给 2 点 → 文案与效果不符）。
    /// 能量原子应当固定为 2；一旦 AA 把它采样放大就重抽。
    /// </summary>
    private static bool HasAbsurdEnergy(GeneratedCard card)
    {
        foreach (var operation in card.Operations)
        {
            var spec = OperationRuntimeSpecCompiler.GetOrCompile(operation);
            if (!spec.Opcode.StartsWith("csk_energy", StringComparison.Ordinal)) continue;
            foreach (var value in spec.Values)
                if (value.BaseValue > 3) return true;
        }
        return false;
    }

    /// <summary>这张卡是否包含至少一个机制（csk_*）操作。</summary>
    private static bool HasMechanicOperation(GeneratedCard card)
    {
        foreach (var operation in card.Operations)
        {
            var spec = OperationRuntimeSpecCompiler.GetOrCompile(operation);
            if (spec.Opcode.StartsWith("csk_", StringComparison.Ordinal)) return true;
        }
        return false;
    }

    /// <summary>这张卡里有没有明显离谱的数值（> 60 或 &lt; -1）。</summary>
    private static bool HasAbsurdValue(GeneratedCard card)
    {
        foreach (var operation in card.Operations)
        {
            var spec = OperationRuntimeSpecCompiler.GetOrCompile(operation);
            foreach (var value in spec.Values)
                if (value.BaseValue > 60 || value.BaseValue < -1) return true;
        }
        return false;
    }

    /// <summary>
    /// 这张卡是不是「只有辅助/纯负面效果」——这种卡不该存在（用户 2026-11-10 要求）。
    /// </summary>
    private static bool IsSupportOnlyCard(GeneratedCard card)
    {
        if (card.Operations.Count == 0) return true;
        if (card.Operations.Count > 1) return false;

        var spec = OperationRuntimeSpecCompiler.GetOrCompile(card.Operations[0]);
        return spec.Opcode switch
        {
            // 只把这张牌换个位置 / 只塞状态牌 / 只结束回合 / 只弃牌 → 没有独立价值
            "csk_deck" => spec.Variant is SakiComponents.ReturnToHandVariant
                or SakiComponents.DiscardAllVariant
                or SakiComponents.DiscardNonParfaitVariant,
            "csk_status" => true,
            "csk_extreme" => true,
            // 纯减益能力打在自己身上没有任何意义（伤害/格挡都没有）
            "csk_power" => false,
            _ => false
        };
    }

    /// <summary>
    /// 卡名里出现机制词时，补上对应关键词（用户 2026-11-10 要求：
    /// 「名字叫琴奏xx 就必须能触发琴奏的效果」）。
    ///
    /// ★★ 2026-11-11 用户第 3 条：**名字含琴奏 → 自动加「琴奏」关键词 + 打击 tag**。
    ///   本体 `PianoStrike` 就是 `CanonicalTags { CardTag.Strike }` + 关键词 Playpiano；
    ///   `AnonStrike`（弦奏）同理，关键词是 **Playguitar**（不是 Chord）。
    ///   ⚠️ 关键词要在游戏里真的生效，还必须注册 IComponentKeywordRuntimeAdapter
    ///   （见 SakiKeywordRuntime.cs）——只写 CustomKeywords 不会进 CanonicalKeywords。
    /// </summary>
    private static GeneratedCard ApplyNameKeywords(GeneratedCard card, string profileId)
    {
        var name = card.Name?.Chinese ?? string.Empty;
        var keywords = new List<string>(card.CustomKeywords ?? []);
        var before = keywords.Count;
        var tags = new List<CardTag>(card.Tags);
        var tagsBefore = tags.Count;

        void Add(string keyword)
        {
            if (!keywords.Contains(keyword)) keywords.Add(keyword);
        }

        void AddStrike()
        {
            if (!tags.Contains(CardTag.Strike)) tags.Add(CardTag.Strike);
        }

        // ★ 2026-11-11 用户口径：**只要卡名含琴奏/弦奏就加关键词 + Strike**，
        //   不限制卡型（技能/能力也可以）。卡名不含就绝不能有 —— 之前 AA 会按
        //   `AllowedCustomBaseKeywords` 从目录里随机采样，造成「我来麻木」也带琴奏；
        //   现在 shell recipe 不再写 CustomKeywords，AA 没有可采样的计数，
        //   这里只按名字补。
        if (profileId == RandomProfiles.Saki && name.Contains("琴奏", StringComparison.Ordinal))
        {
            Add(CuteSakikoMod.CuteSakikoModCode.Others.CutesakiKeywords.Playpiano);
            AddStrike();
        }

        if (profileId == RandomProfiles.Anon && name.Contains("弦奏", StringComparison.Ordinal))
        {
            Add(CuteSakikoMod.CuteSakikoModCode.Others.CutesakiKeywords.Playguitar);
            AddStrike();
        }

        if (profileId == RandomProfiles.Saki && name.Contains("回忆", StringComparison.Ordinal)) Add(CuteSakikoMod.CuteSakikoModCode.Others.CutesakiKeywords.Memory);
        if (profileId == RandomProfiles.Saki && name.Contains("骑士之剑", StringComparison.Ordinal)) Add(CuteSakikoMod.CuteSakikoModCode.Others.CutesakiKeywords.KnightSword);

        // ★ 2026-11-11 用户要求：把骑士之剑入手要走本体「出鞘」关键词的机制
        //   （CutesakiKeywords.Sword → SwordManager.BeforeCardPlayed 自动 EnsureSwordExists），
        //   而不是卡面文字里写「手动加入手牌」。
        //   这里按操作变体判断：只要这张卡有「把骑士之剑加入手牌」的操作，就挂上出鞘关键词。
        var grantsSword = card.Operations.Any(operation =>
        {
            try
            {
                var spec = OperationRuntimeSpecCompiler.GetOrCompile(operation);
                return spec.Opcode == SakiComponents.TokenOpcode
                       && spec.Variant is SakiComponents.SwordFromAnywhereVariant
                           or SakiComponents.DamageGrowthSwordVariant
                           or SakiComponents.BlockGrowthSwordVariant
                           or SakiComponents.DoubleCollapseSwordVariant
                           or SakiComponents.DamageSwordOnKillVariant;
            }
            catch { return false; }
        });
        // ★ 2026-11-11 用户口径（第 3 条）：本体没有「直接获得骑士之剑」，只有「出鞘」关键词。
        //   所以删掉所有加剑原子，改成：祥子的一部分卡（≥2 个操作 = 复合卡）按稳定哈希带「出鞘」。
        if (profileId == RandomProfiles.Saki && !grantsSword && card.Operations.Count >= 2)
        {
            var hash = 0;
            foreach (var ch in name) hash = (hash * 31 + ch) & 0x7fffffff;
            if (hash % 5 == 0) Add(CuteSakikoMod.CuteSakikoModCode.Others.CutesakiKeywords.Sword);
        }
        if (profileId == RandomProfiles.Rana && name.Contains("猫咪", StringComparison.Ordinal)) Add(CuteSakikoMod.CuteSakikoModCode.Others.CutesakiKeywords.Neko);
        if (profileId == RandomProfiles.Rana && name.Contains("芭菲", StringComparison.Ordinal)) Add(CuteSakikoMod.CuteSakikoModCode.Others.CutesakiKeywords.Parfait);

        // ★ 2026-11-11 用户第 1 条：特殊演奏/莱芜也要能在**基础卡池**随机到，
        //   不只升级才给。这里按 15% 概率给本角色基础卡挂上。
        if (profileId == RandomProfiles.Anon && GenerationRng.NextDouble() < 0.15)
            Add(CuteSakikoMod.CuteSakikoModCode.Others.CutesakiKeywords.NoNote);
        if (profileId == RandomProfiles.Rana && GenerationRng.NextDouble() < 0.15)
            Add(CuteSakikoMod.CuteSakikoModCode.Others.CutesakiKeywords.RanaLive);

        if (keywords.Count == before && tags.Count == tagsBefore) return card;
        return card with { CustomKeywords = keywords, Tags = tags };
    }

    /// <summary>
    /// 兜底：给这张卡合成一条**数值提升**的升级（用于升级轴落在空操作上的情况）。
    ///
    /// 取第一个「有显式数值槽」的操作，按当前值的 ~20% 给增量（至少 +1），
    /// 并写回一个 IncreaseNumber 效果。这样卡至少「升级真的有用」。
    /// </summary>
    private static CardUpgradePlan SyntheticNumericUpgrade(GeneratedCard card, CardUpgradePlan original)
    {
        for (var index = 0; index < card.Operations.Count; index++)
        {
            var spec = OperationRuntimeSpecCompiler.GetOrCompile(card.Operations[index]);
            var slot = spec.Values.FirstOrDefault(value => value.Explicit);
            if (slot is null) continue;

            var baseValue = slot.BaseValue + slot.Offset;
            if (baseValue <= 0) continue;

            var delta = Math.Max(1, baseValue / 5);
            // 复用原计划的其它字段（UpgradedCost / 标签 / 关键词 …），只换效果列表。
            return original with
            {
                Effects = [new CardUpgradeEffect(CardUpgradeKind.IncreaseNumber, index, delta,
                    ValueSlotId: slot.Id)]
            };
        }

        // ★ B9（2026-11-10）：没有可升级的数值槽（我们大量原子是「无槽版」，因为
        //   文案里不能写阿拉伯数字，§3.92）——这时升级必须**换个轴**，否则玩家升级后
        //   什么都看不到（实测 10 张里有 6~7 张升级前后一字不变）。
        //   优先级：降费（最直观）→ 固有 → 保留。
        if (card.Cost > 0)
            return original with { UpgradedCost = Math.Max(0, card.Cost - 1) };

        var effects = new List<CardUpgradeEffect>();
        if (!card.Tags.Contains(CardTag.Innate))
            effects.Add(new CardUpgradeEffect(CardUpgradeKind.GrantInnate, 0, 0));
        else if (!card.Tags.Contains(CardTag.Retain))
            effects.Add(new CardUpgradeEffect(CardUpgradeKind.GrantRetain, 0, 0));

        return original with { Effects = effects };
    }

    /// <summary>
    /// 2026-11-11 用户口径：升级池除了数值，还可以随机获得：
    ///   · 去除「消耗 / 虚无」；
    ///   · 固有 / 保留；
    ///   · 角色关键词：祥子「出鞘」、爱音「特殊演奏」、乐奈「莱芜」。
    /// 每次升级最多追加 1 个，避免一张卡升级后同时多出三四个关键词。
    /// </summary>
    private static CardUpgradePlan AugmentUpgradeWithKeywords(GeneratedCard card, string profileId,
        CardUpgradePlan plan)
    {
        try
        {
            var effects = plan.Effects.ToList();
            // AA 已经选了关键词类升级时不再追加，避免「移除消耗 ×2 / 固有+保留」这种重复。
            if (effects.Any(effect => effect.Kind is CardUpgradeKind.RemoveExhaust
                    or CardUpgradeKind.RemoveEthereal
                    or CardUpgradeKind.GrantInnate
                    or CardUpgradeKind.GrantRetain
                    or CardUpgradeKind.AddCustomKeyword))
                return plan;

            var added = false;
            CardUpgradeEffect? extra = null;

            if (card.Tags.Contains(CardTag.Exhaust) && GenerationRng.NextDouble() < 0.5)
                extra = new CardUpgradeEffect(CardUpgradeKind.RemoveExhaust, 0, 0);
            else if (card.Tags.Contains(CardTag.Ethereal) && GenerationRng.NextDouble() < 0.5)
                extra = new CardUpgradeEffect(CardUpgradeKind.RemoveEthereal, 0, 0);
            else if (!card.Tags.Contains(CardTag.Innate) && GenerationRng.NextDouble() < 0.20)
                extra = new CardUpgradeEffect(CardUpgradeKind.GrantInnate, 0, 0);
            else if (!card.Tags.Contains(CardTag.Retain) && !card.Tags.Contains(CardTag.Ethereal)
                     && GenerationRng.NextDouble() < 0.20)
                extra = new CardUpgradeEffect(CardUpgradeKind.GrantRetain, 0, 0);
            else
            {
                var keyword = profileId switch
                {
                    RandomProfiles.Saki => CuteSakikoMod.CuteSakikoModCode.Others.CutesakiKeywords.Sword,
                    RandomProfiles.Anon => CuteSakikoMod.CuteSakikoModCode.Others.CutesakiKeywords.NoNote,
                    RandomProfiles.Rana => CuteSakikoMod.CuteSakikoModCode.Others.CutesakiKeywords.RanaLive,
                    _ => null
                };
                if (keyword is not null
                    && !(card.CustomKeywords ?? []).Contains(keyword)
                    && GenerationRng.NextDouble() < 0.40)
                {
                    extra = new CardUpgradeEffect(CardUpgradeKind.AddCustomKeyword, 0, 0,
                        KeywordId: keyword);
                }
            }

            if (extra is null) return plan;
            effects.Add(extra);
            added = true;
            if (!added) return plan;

            try
            {
                var upgraded = CardUpgradeGenerator.ApplyEffectsToOperations(card.Operations, effects);
                var result = plan with
                {
                    Effects = effects,
                    UpgradedChineseDescription = CardDescriptionRenderer.Render(upgraded),
                    UpgradedEnglishDescription = EnglishCardDescriptionRenderer.Render(upgraded)
                };
                Log.Info($"[升级追加] [{profileId}] 「{card.Name?.Chinese}」 +{extra.Kind}"
                         + (extra.KeywordId is null ? string.Empty : $"({extra.KeywordId})"));
                return result;
            }
            catch (Exception exception)
            {
                Log.Warn($"[升级追加] 「{card.Name?.Chinese}」失败：{exception.Message}");
                return plan;
            }
        }
        catch { return plan; }
    }

    /// <summary>
    /// 获得「回忆 / 欺诈 / 逃避 / 猫咪」的卡升级后，运行期本来就按 IsUpgraded 给升级版；
    /// 这里把升级描述里的卡名补上「+」，让卡面和行为一致。
    /// </summary>
    private static CardUpgradePlan ApplyGrantedCardUpgradeText(GeneratedCard card, CardUpgradePlan plan)
    {
        try
        {
            if (plan.Effects.Count == 0) return plan;
            var text = plan.UpgradedChineseDescription;
            var changed = false;

            bool Repl(string from, string to)
            {
                if (!text.Contains(from, StringComparison.Ordinal)
                    || text.Contains(to, StringComparison.Ordinal)) return false;
                text = text.Replace(from, to, StringComparison.Ordinal);
                return true;
            }

            foreach (var operation in card.Operations)
            {
                OperationRuntimeSpec spec;
                try { spec = OperationRuntimeSpecCompiler.GetOrCompile(operation); }
                catch { continue; }

                if (spec.Opcode == SakiComponents.RecallOpcode)
                    changed |= Repl("回忆", "回忆+");
                else if (spec.Opcode == SakiComponents.StatusOpcode)
                    changed |= Repl("欺诈", "欺诈+") | Repl("逃避", "逃避+");
                else if (spec.Opcode == SakiComponents.TokenOpcode
                         && spec.Variant == SakiComponents.TokenNekoVariant)
                    changed |= Repl("猫咪", "猫咪+");
            }

            if (!changed) return plan;
            Log.Info($"[升级文本] 「{card.Name?.Chinese}」获得卡改为升级版");
            return plan with { UpgradedChineseDescription = text };
        }
        catch { return plan; }
    }

    /// <summary>
    /// 纠正「反着升级」：AA 的 IncreaseNumber 会挑数值最大的槽，
    /// 可能挑中压力代价 / 芭菲杯数 / 金币价 / 状态张数 —— 升级后代价更高，是负升级。
    /// 这里把这些槽的 IncreaseNumber 改成 ReduceNegativeNumber（代价 -1），并重算升级描述。
    /// </summary>
    private static CardUpgradePlan NormalizeUpgradeDirection(GeneratedCard card, CardUpgradePlan plan)
    {
        try
        {
            var effects = plan.Effects.ToArray();
            var changed = false;
            for (var i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];
                if (effect.Kind != CardUpgradeKind.IncreaseNumber) continue;
                if (effect.OperationIndex is not { } index
                    || index < 0 || index >= card.Operations.Count) continue;

                OperationRuntimeSpec spec;
                string? slotId;
                try
                {
                    spec = OperationRuntimeSpecCompiler.GetOrCompile(card.Operations[index]);
                    slotId = effect.ValueSlotId
                             ?? OperationRuntimeSpecCompiler.UpgradeValueSlot(card.Operations[index]);
                }
                catch { continue; }

                if (slotId is null || !IsCostSlot(spec.Opcode, spec.Variant, slotId)) continue;

                var current = spec.Values.FirstOrDefault(value => value.Id == slotId)?.BaseValue ?? 0;
                if (current <= 1)
                {
                    // 代价只剩 1 层时不能再减到 0（会写出「消耗0层压力」）。
                    // 改成升收益；没有可升的收益就给固有。
                    var payoff = spec.Values.FirstOrDefault(value =>
                        value.Id != slotId
                        && value.Id is "damage" or "block" or "draw" or "energy" or "count"
                            or "amount" or "extra");
                    effects[i] = payoff is null
                        ? effect with
                        {
                            Kind = CardUpgradeKind.GrantInnate,
                            Delta = 0,
                            ValueSlotId = null
                        }
                        : effect with
                        {
                            Kind = CardUpgradeKind.IncreaseNumber,
                            Delta = 1,
                            ValueSlotId = payoff.Id
                        };
                }
                else
                {
                    effects[i] = effect with
                    {
                        Kind = CardUpgradeKind.ReduceNegativeNumber,
                        Delta = -1
                    };
                }

                changed = true;
                Log.Info($"[升级修正] 「{card.Name?.Chinese}」{spec.Opcode}/{spec.Variant} {slotId}："
                         + $"IncreaseNumber → {effects[i].Kind}");
            }

            if (!changed) return plan;
            try
            {
                var upgraded = CardUpgradeGenerator.ApplyEffectsToOperations(card.Operations, effects);
                return plan with
                {
                    Effects = effects,
                    UpgradedChineseDescription = CardDescriptionRenderer.Render(upgraded),
                    UpgradedEnglishDescription = EnglishCardDescriptionRenderer.Render(upgraded)
                };
            }
            catch { return plan with { Effects = effects }; }
        }
        catch { return plan; }
    }

    /// <summary>
    /// 在「已抹掉辉星费」的卡上重新生成升级计划。
    ///
    /// 为什么必须重来：原计划里若含 <c>ReduceStarCost</c>，抹掉辉星后它就是空操作，
    /// 玩家会看到「升级了但什么都没变」。重算后升级轴会落到数值/费用/关键词上。
    ///
    /// 种子用 slot 派生，保证同一 seed 下每次生成结果一致（联机/读档安全）。
    /// </summary>
    private static GeneratedCard RegenerateUpgrade(GeneratedCard card, string profileId, int slot)
    {
        try
        {
            var upgrade = CardUpgradeGenerator.Generate(
                card,
                new Random(unchecked(slot * 397 ^ profileId.Length * 7919)),
                unifiedChaos: false,
                profileId: profileId,
                keywordPolicy: RandomCatalogs.KeywordPolicy(profileId));

            // ★ 2026-11-11 用户反馈：「绝对工作+」升级后压力代价 6→7，升级反而变弱。
            //   AA 的 IncreaseNumber 会挑「数值最大」的槽，可能挑中代价槽
            //   （压力/芭菲杯/金币/状态张数）。这里统一纠正：
            //   代价槽的 IncreaseNumber 改成 ReduceNegativeNumber（代价 -1），并重算升级描述。
            upgrade = NormalizeUpgradeDirection(card, upgrade);
            // 用户 2026-11-11：升级池要包含固有/保留/角色关键词，以及去除消耗/虚无。
            upgrade = AugmentUpgradeWithKeywords(card, profileId, upgrade);
            // 获得回忆/欺诈/逃避/猫咪时，升级文本显示「回忆+」等（运行期本来就按 IsUpgraded 给升级版）。
            upgrade = ApplyGrantedCardUpgradeText(card, upgrade);

            // ★★ 2026-11-10 实测根因（「唯有练习」升级无提升）：
            //   重算之后**仍然**可能得到纯 `ReduceStarCost`（辉星费已被我们抹成 -1，
            //   这条升级就是空操作），当时小祥只是把 Effects 清空 ⇒ 等于这张卡没有升级。
            //   正确做法：把空操作换成**真正的数值提升**（这条 op 的第一个显式数值槽）。
            // 诊断（B9/T-034）：把每张卡的升级计划打出来，查明为什么「升级后没变化」。
            Log.Info($"[CuteSakikoModRandom][升级诊断] 「{card.Name?.Chinese}」 baseCost={card.Cost} "
                     + $"effects={string.Join("|", upgrade.Effects.Select(e => e.Kind + "@" + e.OperationIndex + ":" + (e.ValueSlotId ?? "-")))} "
                     + $"upCost={upgrade.UpgradedCost} descSame={upgrade.UpgradedChineseDescription == card.ChineseDescription}");

            if (upgrade.Effects.Count == 0
                || upgrade.Effects.All(effect => effect.Kind == CardUpgradeKind.ReduceStarCost))
            {
                return card with { Upgrade = SyntheticNumericUpgrade(card, upgrade) };
            }

            return card with { Upgrade = upgrade };
        }
        catch (Exception)
        {
            // 重算失败就保留原计划，至少不会比之前更差。
            return card;
        }
    }

    /// <summary>
    /// 去掉辉星费用。
    /// AA 把「辉星费」拆成两个属性：<c>StarCost</c>（固定值）与 <c>HasStarCostX</c>（X 费）。
    /// 两者都要归零，否则 UI 仍会画辉星图标。
    /// </summary>
    private static GeneratedCard StripStarCost(GeneratedCard card)
    {
        if (card.StarCost <= 0 && !card.HasStarCostX) return card;

        // 升级计划里可能也带了星费降低，一并抹掉。
        var upgrade = card.Upgrade is { } plan
            ? plan with { UpgradedStarCost = null }
            : null;

        return card with
        {
            StarCost = -1,
            HasStarCostX = false,
            Upgrade = upgrade
        };
    }

    private static GeneratedCard FallbackCard(GeneratedRarity rarity, GeneratedCharacter archetype)
    {
        var amount = rarity switch
        {
            GeneratedRarity.Basic => 6,
            GeneratedRarity.Common => 7,
            GeneratedRarity.Uncommon => 9,
            GeneratedRarity.Rare => 12,
            _ => 14
        };
        var atom = SakiAtomLibrary.Damage(amount);
        var operation = new GeneratorOperation(
            atom.Template, atom.Scope, atom.ChineseText,
            new Dictionary<string, int>(),
            RequiresSingleTarget: true,
            RuntimeSpec: atom.RuntimeSpec,
            LocalizedText: atom.LocalizedText);
        return new GeneratedCard(
            Cost: 1,
            Type: GeneratedCardType.Attack,
            Target: TargetMode.SingleEnemy,
            Rarity: rarity,
            ChineseDescription: atom.ChineseText,
            Tags: [],
            Operations: [operation],
            Character: archetype,
            StarCost: -1,
            HasStarCostX: false);
    }

    /// <summary>稳定种子：同一 (profileId, seed) 一定得到同一个 int，跨端一致。</summary>
    private static int StableSeed(string profileId, string seed)
    {
        var payload = System.Text.Encoding.UTF8.GetBytes($"{profileId}|{seed}");
        var hash = System.Security.Cryptography.SHA256.HashData(payload);
        return BitConverter.ToInt32(hash, 0) & int.MaxValue;
    }
}













































































































