using ChaosCardGenerator;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 本体专属能力的**纯数据清单**（不依赖 sts2，可离线验证）。
///
/// 为什么单独拆出来：
///   真正的执行器需要 `PowerCmd` 与具体 Power 类型（都在 CuteSakikoMod.dll 里，
///   而那个 dll 依赖 sts2），离线自检加载不了。
///   把「清单」与「执行」拆开之后，清单可以离线校验
///   （variant 唯一、名称非空、每角色数量合理），执行部分交给实机。
///
/// 能力按角色划分（同「机制不串池」原则）：
///   祥子 → 压力/记忆/骑士之剑系
///   爱音 → 和弦/音符/吉他系
///   乐奈 → 芭菲/猫咪/莱芜系
/// </summary>
internal static class SakiPowerCatalog
{
    public const string Template = "csk:power";
    public const string Opcode = "csk_power";

    /// <summary>一个可随机化的本体能力。<c>TypeName</c> 是完整类名（运行时再反射成 Type）。</summary>
    internal readonly record struct ModPower(
        string Variant,
        string ChineseName,
        string EnglishName,
        string TypeName,
        ComponentCategory Category,
        bool TargetsEnemy = false);

        /// <summary>
    /// 这个能力能不能叠层（决定「获得 N 层 X」里的 N 能不能 &gt; 1）。
    /// ⚠️ 目前是**白名单**：只有明确可以叠层的能力才写进来；不在名单里的一律只给 1 层，
    ///    避免「只能单层的能力被写成获得 11 层」。
    /// </summary>
    /// <summary>
    /// 这个能力能不能叠层 —— 直接读本体 `PowerModel.StackType`：
    ///     PowerStackType.Counter = 可叠层（可以「获得 X 层」）
    ///     PowerStackType.Single  = 只能单层（只能「获得 X」）
    /// 实现：反射拿到能力类型 → Activator 造实例 → 读 StackType（拿不到就按单层，安全优先）。
    /// </summary>
    internal static bool IsStackable(string variant)
    {
        if (string.IsNullOrEmpty(variant)) return false;
        lock (StackCache)
        {
            if (StackCache.TryGetValue(variant, out var cached)) return cached;
        }

        var stackable = false;
        try
        {
            var entry = AllPowers().FirstOrDefault(p => string.Equals(p.Variant, variant, StringComparison.Ordinal));
            var type = entry.TypeName is { Length: > 0 } name ? ResolveType(name) : null;
            if (type is not null)
            {
                var property = type.GetProperty("StackType",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (property is not null)
                {
                    object? instance = null;
                    try { instance = Activator.CreateInstance(type); } catch { }
                    var value = instance is null ? null : property.GetValue(instance);
                    stackable = string.Equals(value?.ToString(), "Counter", StringComparison.Ordinal);
                }
            }
        }
        catch { }

        lock (StackCache) StackCache[variant] = stackable;
        return stackable;
    }

    private static readonly Dictionary<string, bool> StackCache = new(StringComparer.Ordinal);

    private static IEnumerable<ModPower> AllPowers() =>
        For(RandomProfiles.Saki).Concat(For(RandomProfiles.Anon)).Concat(For(RandomProfiles.Rana));

    private static Type? ResolveType(string fullName) =>
        Type.GetType(fullName) ?? AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => { try { return a.GetType(fullName); } catch { return null; } })
            .FirstOrDefault(t => t is not null);


    internal static IReadOnlyList<ModPower> For(string profileId) => profileId switch
    {
        RandomProfiles.Saki => SakiPowers,
        RandomProfiles.Anon => AnonPowers,
        RandomProfiles.Rana => RanaPowers,
        _ => []
    };

    /// <summary>生成一个「获得 N 层某能力」的组件。</summary>
    internal static ComponentAtom Atom(ModPower power, int amount)
    {
        var chinese = power.TargetsEnemy
            ? $"给予{amount}层[gold]{power.ChineseName}[/gold]。"
            : $"获得{amount}层[gold]{power.ChineseName}[/gold]。";
        var english = power.TargetsEnemy
            ? $"Apply {amount} [gold]{power.EnglishName}[/gold]."
            : $"Gain {amount} [gold]{power.EnglishName}[/gold].";

        // ★★ 每个能力必须用**自己的 template**（2026-11-09 实测，见文档 §3.39）：
        //   同一个 template 配不同中文文本会被 AA 的模板编译器判成结构冲突，
        //   该 atom **静默落选、不报错**。
        //   之前 31 个能力全用 "csk:power"，结果只有一个能出现（莱芜实测 150 张 0 次）。
        //   运行时路由仍走 opcode + variant，不受 template 影响。
        var template = $"{Template}_{power.Variant}";
        return SakiComponents.AtomWithSpec(template, OperationScope.NonTargeted, chinese, english,
            SakiComponents.NumericSpec(Opcode, power.Variant, "self", "amount", amount, []),
            category: power.Category);
    }

    private const string Namespace = "CuteSakikoMod.CuteSakikoModCode.Powers.Buff.";

    // 祥子（Mujica）：压力 / 记忆 / 骑士之剑系
    private static readonly ModPower[] SakiPowers =
    [
        new("pressure_dmg", "压力化动力", "Pressure Drive", Namespace + "PressureDmgPower", ComponentCategory.Growth),
        new("oblivionis", "遗忘之力", "Oblivionis", Namespace + "OblivionisPower", ComponentCategory.Damage),
        new("memory_coming", "回忆涌来", "Flood of Memories", Namespace + "MemoryComingPower", ComponentCategory.CardManipulation),
        new("meet_performance", "迎接舞台", "Meet Performance", Namespace + "MeetPerformancePower", ComponentCategory.Defense),
        new("saki_parry", "招架", "Parry", Namespace + "SakiParryPower", ComponentCategory.Defense),
                // 祥036 直击内心：破防（本体 BreakDownPower）
new("half_body", "半身", "Other Half", Namespace + "OtherHalfPower", ComponentCategory.Utility),
        new("bow_to_me", "向我俯首", "Bow to Me", Namespace + "BowToMePower", ComponentCategory.Utility),
                // 祥080 麻木：人工制品（本体 ArtifactPower）
        new("artifact", "人工制品", "Artifact", "MegaCrit.Sts2.Core.Models.Powers.ArtifactPower", ComponentCategory.Defense),
new("dont_associate", "别和我扯上关系", "Stay Away", Namespace + "DontAssociateMePower", ComponentCategory.Defense),
        new("ether", "以太", "Ether", Namespace + "EtherPower", ComponentCategory.Utility),
        new("masquerade", "假面舞会", "Masquerade", Namespace + "MasqueradePower", ComponentCategory.Utility),
        new("black_rebirth", "黑色重生", "Black Rebirth", Namespace + "BlackRebirthPower", ComponentCategory.Utility),
        new("patience", "忍耐", "Patience", Namespace + "PatiencePower", ComponentCategory.Utility),
        new("weak_me_dead", "软糯的我已经死了", "Weak Me", Namespace + "WeakMeDeadPower", ComponentCategory.Utility),
        new("musica", "天球のMúsica", "Musica", Namespace + "MusicaCaelestisPower", ComponentCategory.Utility),
        new("saki_sweep", "横扫", "Sweep", Namespace + "SakiSweepPower", ComponentCategory.Damage),
        new("daggers_drawn", "剑拔弩张的气氛", "Daggers Drawn", Namespace + "DaggersDrawnPower", ComponentCategory.Status),
        new("calmness_lady", "大小姐的从容", "Calmness Lady", Namespace + "CalmnessLadyPower", ComponentCategory.Defense),
        new("be_god", "我来成神", "Become God", Namespace + "BeGodPower", ComponentCategory.Utility),
        new("ob_card", "遗忘女神", "Oblivion Goddess", Namespace + "ObCardPower", ComponentCategory.Utility),
    ];

    // 爱音（MyGO）：和弦 / 音符 / 吉他系
    private static readonly ModPower[] AnonPowers =
    [
        new("chord_bonus", "和弦增幅", "Chord Bonus", Namespace + "ChordBonusPower", ComponentCategory.Resource),
        new("guitar_vocal", "吉他主唱", "Guitar Vocal", Namespace + "GuitarVocalPower", ComponentCategory.Damage),
        new("full_assault", "全力攻哈！", "Full Assault", Namespace + "FullAssaultPower", ComponentCategory.CardManipulation),
        new("lingering_taste", "意犹未尽", "Lingering Taste", Namespace + "LingeringTastePower", ComponentCategory.Utility),
        new("messy_play", "弹得乱七八糟", "Messy Play", Namespace + "MessyPlayPower", ComponentCategory.Resource),
        new("marks_of_practice", "练习的证明", "Marks of Practice", Namespace + "MarksOfPracticePower", ComponentCategory.Resource),
                // 爱016 好可爱：缩小（本体原生 power，用全名解析）
        new("shrink", "缩小", "Shrink", "MegaCrit.Sts2.Core.Models.Powers.ShrinkPower", ComponentCategory.Status, TargetsEnemy: true),
new("stage_nerves", "怯场", "Stage Nerves", Namespace + "StageNervesPower", ComponentCategory.Defense),
        new("unforgettable", "难忘的演奏", "Unforgettable", Namespace + "UnforgettablePerformancePower", ComponentCategory.Resource),
        new("escapism", "习惯逃避", "Escapism", Namespace + "EscapismTendencyPower", ComponentCategory.Defense),
        new("when_perform", "什么时候演奏？", "When Perform", Namespace + "WhenPerformPower", ComponentCategory.Utility),
        new("steady_practice", "坚持练习", "Steady Practice", Namespace + "SteadyPracticePower", ComponentCategory.Resource),
        new("near_sight", "近视眼", "Nearsighted", Namespace + "NearSightPower", ComponentCategory.CardManipulation),
        new("nai_vitality", "毫无活力", "No Vitality", Namespace + "NaiVitalityPower", ComponentCategory.Utility),
        new("play_immediately", "即刻演奏", "Play Immediately", Namespace + "PlayImmediatelyPower", ComponentCategory.Utility),
    ];

    // 乐奈（Rana）：芭菲 / 猫咪 / 莱芜系
    private static readonly ModPower[] RanaPowers =
    [
        new("neko_scratch", "挠！挠！", "Cat Scratch", Namespace + "NekoScratchPower", ComponentCategory.Damage),
        new("neko_leader", "猫咪领袖", "Cat Leader", Namespace + "NekoLeaderPower", ComponentCategory.CardManipulation),
                // 乐073 边吃边看：无实体（本体 IntangiblePower）
        new("intangible", "无实体", "Intangible", "MegaCrit.Sts2.Core.Models.Powers.IntangiblePower", ComponentCategory.Defense),
new("vocal_harmony", "和声", "Harmony", Namespace + "VocalHarmonyPower", ComponentCategory.Damage),
        new("rain_omen", "雨前征兆", "Rain Omen", Namespace + "RainfallOmenPower", ComponentCategory.Defense),
        new("parfait_treat", "有人请客", "Treat", Namespace + "ParfaitTreatPower", ComponentCategory.Utility),
        new("neko_trance", "和猫发呆", "Spacing Out with Cats", Namespace + "NekoTrancePower", ComponentCategory.CardManipulation),
        new("cats_more_cats", "猫咪！还有猫咪！", "Cats! More Cats!", Namespace + "CatsMoreCatsPower", ComponentCategory.CardManipulation),
        new("want_more_parfait", "我还要吃", "I Want More", Namespace + "WantMoreParfaitPower", ComponentCategory.Resource),
        // ★ 2026-11-11：两者皆要 是**抹茶芭菲**系的能力，属于乐奈；
        //   之前它被误放进 AnonPowers，导致爱音卡上出现「获得 N 层两者皆要」（用户实测）。
        new("want_both", "两者皆要", "Want Both", Namespace + "WantBothPower", ComponentCategory.Resource),
        new("parfait_delivery", "送芭菲的猫", "Parfait Delivery Cat", Namespace + "ParfaitDeliveryNekoPower", ComponentCategory.Resource),
        new("rana_live", "莱芜", "Live", Namespace + "RanaLivePower", ComponentCategory.Utility),
        // ★ 2026-11-10 修正：安可（EncoreNextTurnPower）在本体是**乐奈**的
        //   （由 Cards\Rana\Rare\EncoreWish 施加，效果是「下回合获得莱芜爽」），
        //   之前错放进爱音池，导致爱音抽到乐奈的能力（机制串池）。
        new("encore_next", "下回合莱芜爽", "Encore", Namespace + "EncoreNextTurnPower", ComponentCategory.Resource),
        new("finally_here", "终于来了", "Finally Here", Namespace + "FinallyHerePower", ComponentCategory.Resource),
        new("eve_live", "莱芜前夕", "Eve of Live", Namespace + "EveLivePower", ComponentCategory.Resource),
        new("live_sweet", "莱芜爽", "Live Sweet", Namespace + "LiveSweetPower", ComponentCategory.Utility),
    ];
}





















