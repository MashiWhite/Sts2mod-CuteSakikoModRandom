using AutoAnthony;
using ChaosCardGenerator;
using MegaCrit.Sts2.Core.Logging;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 整套接入的注册入口。
///
/// ★ 关键时序（踩过一次坑，务必看完再动）：
///   AutoAnthony 在 <c>ModelDb.InitIds</c> 的 **Postfix 里**就会跑 library preview
///   ——实测日志显示它一次生成 6 套预览池（合计约 4.5 秒），跑完立刻把
///   <c>ComponentApi</c> 的注册表**冻结**。
///
///   所以我们的组件包**绝对不能**挂在 InitIds 的 Postfix 上，否则永远晚一步，抛：
///     "Component packages must be registered before the first generation profile resolves."
///
///   ⇒ 全部注册统一放在模组 Init() 里，此时 ModelDb 还没初始化，
///     但 AutoAnthony 的纯生成器部分本来就不依赖 ModelDb。
///     代偿：壳（稀有度/类型/目标/费用分布）必须硬编码 → 见 RandomShellTable。
///
/// ★ 另一个坑（2026-11-09）：有些注册表是**全局**的，不能按 profile 重复注册：
///     ComponentRuntimeApi.Handlers   —— 全局（按 opcode+variant）
///     ComponentValuationApi.Routes  —— 全局（按 opcode+variant）
///     CardNameGenerator.Parts       —— 全局（按 PartId）
///   前两个用「只注册一次 + 首包传估值」解决；
///   第三个靠给 PartId 加 csSaki_/csAnon_/csRana_ 前缀解决。
/// </summary>
internal static class RandomIntegration
{
    private static readonly HashSet<string> Registered = new(StringComparer.Ordinal);
    private static bool _runtimeRegistered;

    /// <summary>注册单个角色。每个角色独立调用，单点失败不影响其他角色。</summary>
    internal static void RegisterProfile(string profileId)
    {
        EnsureRuntimeRegistered();

        // ① 词片（全局表，PartId 必须唯一 → 我们的 ID 都带角色前缀）
        RandomNames.Register(profileId);

        // ② 组件包（含估值；估值表全局唯一，所以只在第一个成功的角色里传）
        var provideValuations = !AnyValuationRegistered();
        RegisterPackage(profileId, provideValuations);

        // ②.5 关键词的运行期投影（关键！只在 CustomKeywords 里写名字是不生效的）
        //    ChaosCardModel.CanonicalKeywords 只认 ComponentKeywordRuntimeApi 注册的 adapter；
        //    不注册的话「琴奏」关键词不会进 Keywords，钢琴相关计数/触发全部落空。
        SakiKeywordRuntime.Register(profileId);

        // ③ 外部角色身份 + 运行期宿主
        ExternalComponentCharacterApi.Register(new ExternalComponentCharacterRegistration(
            profileId,
            RandomProfiles.ArchetypeFor(profileId),
            RandomProfiles.EnergyIconPrefixFor(profileId)));

        ExternalComponentCharacterApi.RegisterRuntime(new ExternalComponentCharacterRuntimeRegistration(
            profileId,
            RandomCardSlots.CountFor(profileId),
            slot => RandomCardSlots.TypeForSlot(profileId, slot),
            () => RandomRunState.IsRunActive(profileId),
            () => RandomShells.PoolForRegistration(profileId)));

        lock (Registered) Registered.Add(profileId);
    }

    /// <summary>运行时路由与展示提示是全局表，整包只注册一次。</summary>
    private static void EnsureRuntimeRegistered()
    {
        if (_runtimeRegistered) return;
        _runtimeRegistered = true;

        // 基础机制路由（压力/遗忘/回忆/音符/和弦/芭菲）
        var routes = new List<ComponentRuntimeRoute>(SakiRuntimeRouters.Routes());
        // 本体专属能力的路由（每个角色一套 variant，opcode 都是 csk_power）
        foreach (var profileId in RandomProfiles.All)
            routes.AddRange(ModPowerHandler.RoutesFor(profileId));

        ComponentRuntimeApi.RegisterPackage("csk:runtime", routes);

        // 展示提示：整体注册一次。AA 官方 API 是 ComponentPresentationApi，
        // 但 routes 里必须**先去重**（同一个 opcode/variant 注册两次会让整个包抛异常，
        // 之前被 catch 静默吞掉 → 所有自定义能力都没有悬浮提示）。
        try
        {
            var presentationRoutes = SakiPresentation.Routes()
                .GroupBy(route => (route.Opcode, route.Variant))
                .Select(group => group.First())
                .ToArray();
            ComponentPresentationApi.RegisterPackage("csk:presentation", presentationRoutes);
            Log.Info($"[展示提示] 注册 {presentationRoutes.Length} 条（已去重）");
        }
        catch (Exception exception)
        {
            // 提示是可选增强，失败不该拖垮整个模组；但必须留日志，不能再静默。
            Log.Warn($"[展示提示] 注册失败：{exception.Message}");
        }
    }

    /// <summary>估值表是全局的，已经有别的 profile 注册过就不再传。</summary>
    private static bool AnyValuationRegistered()
    {
        lock (Registered)
            return ComponentValuationApi.RegisteredRoutes.Count > 0;
    }

    /// <summary>
    /// 触发 × 效果 的配对（T-015 第一批）。
    /// 只用 AA 原生的「回合开始 / 回合结束」触发 + 本模组机制效果，
    /// 先验证 triggerIndex 绑定能跑通；第二批再写自己的 Power 做
    /// 「当你获得压力时…」这类事件触发。
    /// </summary>
    private static IEnumerable<(ComponentAtom Trigger, ComponentAtom Effect)> TriggerRecipeAtoms(string profileId)
    {
        var triggers = new[] { SakiAtomLibrary.TurnStartTrigger(), SakiAtomLibrary.TurnEndTrigger() };

        // 按角色只给自己的机制效果，保持「机制不串池」
        var effects = profileId switch
        {
            RandomProfiles.Saki => new[]
            {
                SakiAtomLibrary.Pressure(4),
                SakiAtomLibrary.Pressure(6),
                SakiAtomLibrary.Block(5),
                SakiAtomLibrary.Block(8)
            },
            RandomProfiles.Anon => new[]
            {
                SakiAtomLibrary.Block(5),
                SakiAtomLibrary.Block(8),
                SakiAtomLibrary.ClearNotes(1)
            },
            RandomProfiles.Rana => new[]
            {
                SakiAtomLibrary.Block(5),
                SakiAtomLibrary.GainParfait(1),
                SakiAtomLibrary.GainNeko(1)
            },
            _ => []
        };

        foreach (var trigger in triggers)
            foreach (var effect in effects)
                yield return (trigger, effect);
    }

    private static void RegisterPackage(string profileId, bool registerValuations)
    {
        var archetype = RandomProfiles.ArchetypeFor(profileId);
        var shells = RandomShells.For(profileId);
        // ★ 每个角色只抽自己的机制（压力/音符/芭菲各归各家），不共用一份组件库。
        // ★ RecipeAtoms（不去重）：重复次数就是 AA 的出率权重（AtomCounts）。
        //   想让「演奏和弦」更常见，就在 CharacterAtoms 里多写几遍。
        var atoms = SakiAtomUniverse.RecipeAtoms(profileId);
    MegaCrit.Sts2.Core.Logging.Log.Info(
        $"[原子池] {profileId} atoms={atoms.Count} "
        + $"unblocked={atoms.Count(a => a.ChineseText.Contains("未被完全格挡", StringComparison.Ordinal))} "
        + $"blocked={atoms.Count(a => a.ChineseText.Contains("被完全格挡", StringComparison.Ordinal))} "
        + $"kill={atoms.Count(a => a.ChineseText.Contains("若击杀敌人", StringComparison.Ordinal))} "
        + $"notkill={atoms.Count(a => a.ChineseText.Contains("若未击杀敌人", StringComparison.Ordinal))}");

        // 组件目录：一个 atom 一条 recipe（保留重复 = 保留出率权重）。
        var componentRecipes = atoms.Select((atom, index) => new IroncladCardRecipe(
            Id: $"csk_c{index:D3}",
            ChineseTitle: "组件",
            Cost: 0,
            Type: GeneratedCardType.Skill,
            Target: TargetMode.Other,
            OriginalRarity: GeneratedRarity.Common,
            Tags: [],
            Atoms: [atom],
            TriggerOwners: [-1]
        )).ToArray();

        // ★ T-015 第一批：触发型配方（一条 recipe 两个 atom，TriggerOwners [-1,0]）。
        //   触发用 AA 原生 template（A:turnStart / A:turnEnd），效果复用本模组机制原子。
        //   依据：RandomCardGenerator.cs:4647-4669（AA 自测 helix 的写法）。
        // ★★ 触发型配方的原子必须**自己分 SemanticId**（2026-11-10 实测致命）：
        //   TriggerRecipeAtoms 里的原子是直接用 SakiAtomLibrary 造出来的，
        //   没走 RecipeAtoms 的统一编号，SemanticId 为 null
        //   ⇒ AA 的 ComponentProfileValidator 直接拒收整个包：
        //     "cutesakikomod:saki:normal component semantic ID must not be empty"
        //   ⇒ 三个角色全部注册失败，游戏退回原版卡池（用户实机看到的都是原版卡）。
        var triggerPrefix = SakiAtomUniverse.ShortNameFor(profileId);
        var triggerRecipes = TriggerRecipeAtoms(profileId)
            .Select((pair, index) => new IroncladCardRecipe(
                Id: $"csk_t{index:D3}",
                ChineseTitle: "触发组件",
                Cost: 0,
                Type: GeneratedCardType.Power,
                Target: TargetMode.Other,
                OriginalRarity: GeneratedRarity.Common,
                Tags: [],
                Atoms:
                [
                    pair.Trigger with { SemanticId = $"csk/{triggerPrefix}/t{index:D3}a" },
                    pair.Effect with { SemanticId = $"csk/{triggerPrefix}/t{index:D3}b" }
                ],
                TriggerOwners: [-1, 0]
            )).ToArray();

        // 壳目录：形状来自硬编码表。AA 要求壳引用的 atom Key 必须存在于组件目录。
        var placeholder = componentRecipes[0].Atoms[0];
        var shellRecipes = shells.Select((shape, index) => new IroncladCardRecipe(
            Id: $"csk_s{index:D3}",
            ChineseTitle: shape.ChineseTitle,
            Cost: shape.Cost,
            Type: shape.Type,
            Target: shape.Target,
            OriginalRarity: shape.Rarity,
            // ★ 基础攻击牌 = 本角色的「打击」（祥子琴奏 / 爱音弦奏）。
            //   本体这两张卡的 CanonicalTags 都是 { CardTag.Strike }，
            //   没有这个 tag，需要统计「打击牌」的效果就会漏算。
            Tags: shape.Type == GeneratedCardType.Attack && shape.Rarity == GeneratedRarity.Basic
                ? [CardTag.Strike]
                : [],
            Atoms: [placeholder],
            TriggerOwners: [-1],
            StarCost: -1,
            HasStarCostX: false,
            EnglishTitle: shape.EnglishTitle,
            // ★ 2026-11-11 用户口径：琴奏/弦奏关键词**只按卡名**加，不能靠 AA 的目录计数随机采样。
            //   之前这里给基础攻击壳写了 CustomKeywords，AA 的 SampleCustomKeywords 会把
            //   Playpiano 采样到任意卡（连「我来麻木」这种名字不含琴奏的技能牌也会带）。
            //   现在 shell recipe 不再声明 CustomKeywords，AA 的 CustomKeywordCounts 为 0，
            //   关键词只在 RandomDefinitionBuilder.ApplyNameKeywords 里按名字补。
            CustomKeywords: null
        )).ToArray();

        // 词名目录：每条 recipe 用本角色注册过的词片。
        var nameRecipes = RandomNames.For(profileId).Select(entry => new IroncladCardRecipe(
            Id: entry.PartId,
            ChineseTitle: entry.Chinese,
            Cost: 1,
            Type: GeneratedCardType.Skill,
            Target: TargetMode.Other,
            OriginalRarity: GeneratedRarity.Common,
            Tags: [],
            Atoms: [placeholder],
            TriggerOwners: [-1],
            StarCost: -1,
            HasStarCostX: false,
            EnglishTitle: entry.English
        )).ToArray();

        var shellCatalog = new ImmutableComponentCatalog(archetype, shellRecipes);
        // 触发型配方与单原子配方并列进同一个组件目录
        var allComponentRecipes = componentRecipes.Concat(triggerRecipes).ToArray();
        var componentCatalog = new ImmutableComponentCatalog(archetype, allComponentRecipes);
        MegaCrit.Sts2.Core.Logging.Log.Info(
            $"[目录] {profileId} recipes={allComponentRecipes.Length} "
            + $"componentCounts={string.Join("|", componentCatalog.ComponentCounts)} "
            + $"atomsBefore={atoms.Count} catalogAtoms={componentCatalog.Atoms.Count}");
        var nameCatalog = new ImmutableComponentCatalog(archetype, nameRecipes);

        var request = new ComponentProfileRequest(profileId, archetype, false);
        var profile = new ComponentGenerationProfile(
            $"{profileId}:normal",
            archetype,
            false,
            shellCatalog,
            componentCatalog,
            nameCatalog,
                        // ★ 出现几率走官方策略（2026-11-11 用户口径）：估值按 VP 还原，频率靠权重加权。
                () => new SakiOccurrencePolicy(ComponentApi.CreateNativeOccurrencePolicy(componentCatalog)),
            // ★ 自建平衡模型（不用 AA 的原版 DefaultValuePolicy）——原版是按原版五角色校准的，
            // 套在小祥卡池上会让随机卡明显偏弱。系数依据见 SakiValuePolicy 类注释。
            SakiValuePolicy.For(profileId),
            RandomCatalogs.KeywordPolicy(profileId, StrikeKeywordsFor(profileId)));

        ComponentPackageApi.Register(new ComponentPackageRegistration(
            PackageId: $"csk:package:{profileId}",
            Request: request,
            Profile: profile,
            LocalizedTexts: null,
            KeywordUpgrades: null,
            Multiplicities: null,
            Valuations: registerValuations ? SakiValuations.All() : null,
            IncludeInUltimateChaos: false,
            Keywords: StrikeKeywordsFor(profileId)
                .Select(id => new ComponentKeywordDefinition(
                    id,
                    ChineseName: KeywordTitles.GetValueOrDefault(id, string.Empty),
                    EnglishName: KeywordTitlesEn.GetValueOrDefault(id, id)))
                .ToArray(),
            // ★ AA 官方做法（COMPONENT_API.md:293-299）：外部包用**命名本地化模板**，
            //   数值槽写成 [[energy:energy]] / [[amount:cardinal]] 这类 token，
            //   运行期由 AA 渲染成按角色自适应的能量图标（而不是我们手写 [img]）。
            //   这里给「文案里带能量」的原子各注册一条模板。
            Localizations: BuildLocalizations(componentRecipes)

                ));
    }
    /// <summary>
    /// 本角色的「打击」自定义关键词（本体里 琴奏/弦奏/猫咪 这些是 CardKeyword 而非 CardTag）。
    /// 随机卡带上是为了让悬停有说明、需要「打击牌」的效果能正确计数。
    /// </summary>
    /// <summary>
    /// 关键词的显示名（必须**逐字**用本体的标题，否则悬停没字、部分效果也不计数）。
    /// 来源：`CuteSakikoMod\localization\zhs\card_keywords.json` 的 `.title`。
    /// </summary>
    private static readonly Dictionary<string, string> KeywordTitles = new(StringComparer.Ordinal)
    {
        ["CUTE_SAKIKO_MOD_KEYWORD_PLAYPIANO"] = "琴奏",
        ["CUTE_SAKIKO_MOD_KEYWORD_PLAYGUITAR"] = "弦奏",
        ["CUTE_SAKIKO_MOD_KEYWORD_MEMORY"] = "回忆",
        ["CUTE_SAKIKO_MOD_KEYWORD_SWORD"] = "出鞘",
        ["CUTE_SAKIKO_MOD_KEYWORD_KNIGHT_SWORD"] = "骑士之剑",
        ["CUTE_SAKIKO_MOD_KEYWORD_PRESSURE"] = "压力",
        ["CUTE_SAKIKO_MOD_KEYWORD_CHORD"] = "和弦",
        ["CUTE_SAKIKO_MOD_KEYWORD_REMEMBER_CHORD"] = "爱音的和弦",
        ["CUTE_SAKIKO_MOD_KEYWORD_NOTEIFY"] = "音符效果",
        ["CUTE_SAKIKO_MOD_KEYWORD_NEKO"] = "猫咪",
        ["CUTE_SAKIKO_MOD_KEYWORD_PARFAIT"] = "抹茶芭菲",
        ["CUTE_SAKIKO_MOD_KEYWORD_RANA_LIVE"] = "莱芜"
    };

    private static readonly Dictionary<string, string> KeywordTitlesEn = new(StringComparer.Ordinal)
    {
        ["CUTE_SAKIKO_MOD_KEYWORD_PLAYPIANO"] = "Piano",
        ["CUTE_SAKIKO_MOD_KEYWORD_PLAYGUITAR"] = "Guitar",
        ["CUTE_SAKIKO_MOD_KEYWORD_MEMORY"] = "Memory",
        ["CUTE_SAKIKO_MOD_KEYWORD_SWORD"] = "Unsheathe",
        ["CUTE_SAKIKO_MOD_KEYWORD_KNIGHT_SWORD"] = "Knight Sword",
        ["CUTE_SAKIKO_MOD_KEYWORD_PRESSURE"] = "Pressure",
        ["CUTE_SAKIKO_MOD_KEYWORD_CHORD"] = "Chord",
        ["CUTE_SAKIKO_MOD_KEYWORD_REMEMBER_CHORD"] = "Anon's Chords",
        ["CUTE_SAKIKO_MOD_KEYWORD_NOTEIFY"] = "Note",
        ["CUTE_SAKIKO_MOD_KEYWORD_NEKO"] = "Cat",
        ["CUTE_SAKIKO_MOD_KEYWORD_PARFAIT"] = "Matcha Parfait",
        ["CUTE_SAKIKO_MOD_KEYWORD_RANA_LIVE"] = "Rana Live"
    };

    /// <summary>把「N点能量」换成 [[energy:energy]] token，供 AA 渲染成能量图标。</summary>
    private static IReadOnlyList<ComponentLocalizationRegistration> BuildLocalizations(
        IReadOnlyList<IroncladCardRecipe> recipes)
    {
        var result = new List<ComponentLocalizationRegistration>();
        foreach (var recipe in recipes)
        {
            foreach (var atom in recipe.Atoms)
            {
                var chinese = atom.ChineseText;
                if (!chinese.Contains("能量", StringComparison.Ordinal)) continue;
                // ★ 只有真的存在 id=="energy" 数值槽的原子才能用 [[energy:energy]]，
                //   否则 AA 校验直接报 "references missing slot energy"（2026-11-10 实测）。
                if (atom.RuntimeSpec is not { } spec0
                    || !spec0.Values.Any(v => string.Equals(v.Id, "energy", StringComparison.Ordinal))) continue;
                if (atom.SemanticId is not { Length: > 0 } id) continue;

                // ★ 照抄参考实现（MzmAnthonyology/MutsumiBranchKit）：模板用 `[[energy]]` 命名槽。
                //   规则：ChineseText 必须是**渲染后的样子**（获得2点能量。），模板再把它换成 token，
                //   否则注册会报 "localization does not reproduce its Chinese projection"（实测）。
                var energyValue = (int)spec0.Values
                    .First(v => string.Equals(v.Id, "energy", StringComparison.Ordinal)).BaseValue;
                var template = chinese.Replace($"{energyValue}点能量", "[[energy]]点能量", StringComparison.Ordinal);
                if (!template.Contains("[[energy", StringComparison.Ordinal)) continue;

                result.Add(new ComponentLocalizationRegistration(id, template, atom.LocalizedText?.EnglishTemplate ?? template));
            }
        }

        return result;
    }

    /// <summary>
    /// 每个角色的「打击」自定义关键词（只放**等价于本体打击**的那一个）。
    ///
    /// ★ 2026-11-11 用户第 3 条收口：以前这里塞了 5 个关键词，于是随机基础攻击
    ///   会随机带 Memory / Sword / KnightSword / Pressure —— 名字/效果都不对，
    ///   而且 Memory 会让卡进入记忆牌堆（本体机制副作用）。
    ///   现在只保留：
    ///     祥子 → Playpiano（本体 PianoStrike = Strike tag + Playpiano）
    ///     爱音 → Playguitar（本体 AnonStrike = Strike tag + Playguitar）
    ///     乐奈 → 无（本体基本攻击只有 Strike tag）
    /// </summary>
    private static IReadOnlySet<string> StrikeKeywordsFor(string profileId) => profileId switch
    {
        RandomProfiles.Saki => new HashSet<string>(StringComparer.Ordinal)
        {
            CuteSakikoMod.CuteSakikoModCode.Others.CutesakiKeywords.Playpiano
        },
        RandomProfiles.Anon => new HashSet<string>(StringComparer.Ordinal)
        {
            CuteSakikoMod.CuteSakikoModCode.Others.CutesakiKeywords.Playguitar
        },
        _ => new HashSet<string>(StringComparer.Ordinal)
    };
}














