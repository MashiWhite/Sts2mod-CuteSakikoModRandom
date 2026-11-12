using System.Reflection;
using CuteSakikoMod.CuteSakikoModCode.Pools.Anon;
using CuteSakikoMod.CuteSakikoModCode.Pools.Rana;
using CuteSakikoMod.CuteSakikoModCode.Pools.Saki;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 卡池内容整体替换：本局随机模式下，三个小祥角色的卡池**只提供随机卡**
/// （照抄参考实现 MzmAnthonyologyCardPool 的效果：池里没有任何本体卡）。
///
/// 游戏源码依据（sts2.dll 反编译 CardPoolModel.cs:44-52、CardModel.cs:298）：
///     AllCards   => _allCards   ??= GenerateAllCards().Concat(ModHelper.ConcatModelsFromMods(this)).ToArray();
///     AllCardIds => _allCardIds ??= AllCards.Select(c => c.Id).ToHashSet();
///     CardModel.Pool => ModelDb.AllCardPools.FirstOrDefault(p => p.AllCardIds.Contains(Id)) ?? throw ...
///
/// ⇒ 两个 getter 必须分工：
///   · AllCards   ：本局随机激活时**只返回随机卡**（奖励/商店/图鉴都只看它）；
///   · AllCardIds ：**在本体卡都解析过归属之后**也只返回随机卡 id（图鉴不再列本体卡）；
///                  解析完成前必须带上本体卡 id，否则预载会抛 "is not in any card pool!"。
/// </summary>
[HarmonyPatch(typeof(CardPoolModel), nameof(CardPoolModel.AllCards), MethodType.Getter)]
internal static class RandomPoolCardsPatch
{
    private static bool Prefix(CardPoolModel __instance, ref IEnumerable<CardModel> __result)
    {
        var profileId = ProfileOf(__instance);
        if (profileId is null) return true;

        // ★ 第一次访问必须**放行**：我们要靠原逻辑跑一遍、在 Postfix 里抓本体卡快照。
        //   如果这里直接 return false 替换掉，Postfix 永远不会执行 → 快照为空 →
        //   卡图匹配拿不到本体卡（界面全黑图）、AllCardIds 也退化成原样（图鉴还列本体卡）。
        if (!RandomPoolBaseIds.HasSnapshot(profileId))
        {
            try { MegaCrit.Sts2.Core.Logging.Log.Info($"[CuteSakikoModRandom] AllCards 首次放行（抓快照） {profileId}"); } catch { }
            return true;
        }

        // ★ 2026-11-11 对齐参考实现（用户实测小睦）：**局外图鉴显示本体卡，进入对局生成后才换成随机卡**。
        //   所以这里必须用 IsRunActive 门控，不要自作聪明在局外替换（那样图鉴会只剩十来张、还会掉帧）。
        if (!RandomRunState.IsRunActive(profileId)) return true;

        // ⚠️ 不要在 Prefix 里读 pool.AllCards 去抓快照：会重入本 Prefix → 无限递归（实测崩游戏）。
        //    快照在下面的 Postfix 里用 __result 抓。
        var cards = RandomCardSlots.CardsFor(profileId);
        __result = cards;
        return false;
    }

    /// <summary>开局前（还没替换时）用结果快照本体卡 id。</summary>
    private static void Postfix(CardPoolModel __instance, IEnumerable<CardModel> __result)
    {
        var profileId = ProfileOf(__instance);
        if (profileId is null) return;
        RandomPoolBaseIds.Capture(profileId, __result);
    }

    internal static string? ProfileOf(CardPoolModel pool) => pool switch
    {
        CuteSakiCardPool => RandomProfiles.Saki,
        CuteAnonCardPool => RandomProfiles.Anon,
        CuteRanaCardPool => RandomProfiles.Rana,
        _ => null
    };
}

/// <summary>
/// AllCardIds：保证「本体卡能找到归属」（预载不崩），并且一旦本体卡都缓存了 Pool，
/// 就只返回随机卡 id（图鉴/卡池界面里不再出现本体卡）。
/// 性能：每个池只做一次全量检查，之后走缓存，避免图鉴里每帧反射查询导致掉帧。
/// </summary>
[HarmonyPatch(typeof(CardPoolModel), nameof(CardPoolModel.AllCardIds), MethodType.Getter)]
internal static class RandomPoolIdsPatch
{
    private static bool Prefix(CardPoolModel __instance, ref IEnumerable<ModelId> __result)
    {
        var profileId = RandomPoolCardsPatch.ProfileOf(__instance);
        if (profileId is null) return true;

        var randomIds = RandomCardSlots.IdsFor(profileId);

        // ★ 顺序很重要：**先判断在不在对局里**。
        //   之前把 AllResolved 放在前面 → 局外也返回「只有随机卡」，
        //   于是本体卡失去归属 → 图鉴选「祥子」筛出来 0 张（2026-11-11 实测）。
        if (!RandomRunState.IsRunActive(profileId)) return true;

        if (RandomPoolBaseIds.AllResolved(profileId))
        {
            __result = randomIds;
            return false;
        }

        var baseIds = RandomPoolBaseIds.IdsFor(profileId);
        if (baseIds.Count == 0) return true;

        __result = baseIds.Concat(randomIds).Distinct().ToArray();
        return false;
    }
}

/// <summary>
/// 让 ModelDb.AllCards 的缓存失效。
///
/// 读 sts2.dll 源码（ModelDb.cs:114）：
///     AllCards => _allCards ?? (AllCardPools.SelectMany(p => p.AllCards)
///                               .Concat(AllCharacters.SelectMany(c => c.StartingDeck)).Distinct());
/// **它是缓存**，启动时就算好了。所以本局装了 96 张随机卡之后，
/// 如果不清这个缓存，图鉴里只会看到「初始牌组里那 10 张随机卡」（用户实测正是 10 张）。
/// </summary>
internal static class ModelDbCache
{
    private static readonly FieldInfo? AllCardsField =
        typeof(ModelDb).GetField("_allCards", BindingFlags.Static | BindingFlags.NonPublic);

    internal static void Invalidate()
    {
        try { AllCardsField?.SetValue(null, null); }
        catch (Exception exception)
        {
            Log.Warn($"[CuteSakikoModRandom] 清理 ModelDb.AllCards 缓存失败：{exception.Message}");
        }
    }
}

/// <summary>
/// 对局退出/放弃/死亡时清理随机状态。
/// `RunEndedEvent` 在「保存并退出」这条路径上**不触发**（实测），
/// 所以直接打 `RunManager.CleanUp`（离开对局时必走）。
/// </summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp))]
internal static class RunCleanUpPatch
{
    private static void Postfix()
    {
        try { RandomRunState.Clear(); }
        catch (Exception exception)
        {
            Log.Warn($"[CuteSakikoModRandom] CleanUp 清理失败：{exception.Message}");
        }
    }
}

/// <summary>本体卡 id 快照 + 归属解析状态缓存。</summary>
internal static class RandomPoolBaseIds
{
    private static readonly Dictionary<string, ModelId[]> Snapshots = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, bool> Resolved = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, CardModel[]> BaseCards = new(StringComparer.Ordinal);

    internal static bool HasSnapshot(string profileId)
    {
        lock (Snapshots) return Snapshots.ContainsKey(profileId);
    }

    internal static void Capture(string profileId, IEnumerable<CardModel> cards)
    {
        lock (Snapshots)
        {
            if (Snapshots.ContainsKey(profileId)) return;
            try { Snapshots[profileId] = cards.Select(card => card.Id).ToArray(); }
            catch { }
        }
    }

    internal static IReadOnlyList<ModelId> IdsFor(string profileId)
    {
        lock (Snapshots) return Snapshots.TryGetValue(profileId, out var ids) ? ids : [];
    }

    /// <summary>
    /// 该 profile 的**本体卡模型**（卡图语义匹配用）。
    /// ★ 必须用快照，不能读 pool.AllCards——那已经是被替换过的随机卡，
    ///   随机卡又需要卡图 → 空路径/递归（2026-11-11 实测 `No loader found for resource: res://`）。
    /// </summary>
    internal static IReadOnlyList<CardModel> CardsFor(string profileId)
    {
        lock (BaseCards)
        {
            if (BaseCards.TryGetValue(profileId, out var cached)) return cached;

            var list = new List<CardModel>();
            foreach (var id in IdsFor(profileId))
            {
                try { list.Add(ModelDb.GetById<CardModel>(id)); }
                catch { }
            }

            var array = list.ToArray();
            BaseCards[profileId] = array;
            return array;
        }
    }

    /// <summary>本体卡是否都已解析过归属（解析过就会缓存 _pool，之后不再需要出现在 AllCardIds 里）。</summary>
    internal static bool AllResolved(string profileId)
    {
        lock (Resolved)
        {
            if (Resolved.TryGetValue(profileId, out var known)) return known;
        }

        var ids = IdsFor(profileId);
        if (ids.Count == 0) return false;

        foreach (var id in ids)
        {
            try
            {
                var card = ModelDb.GetById<CardModel>(id);
                if (PoolField?.GetValue(card) is null) return false;
            }
            catch
            {
                return false;
            }
        }

        lock (Resolved) Resolved[profileId] = true;
        return true;
    }

    private static readonly FieldInfo? PoolField =
        typeof(CardModel).GetField("_pool", BindingFlags.Instance | BindingFlags.NonPublic);
}

/// <summary>装完定义后让对应卡池丢掉旧的 AllCards 缓存。</summary>
internal static class RandomPoolCache
{
    private static readonly MethodInfo? InvalidateMethod =
        typeof(CardPoolModel).GetMethod("InvalidateCardCache", BindingFlags.Instance | BindingFlags.NonPublic);

    internal static void Refresh(CardPoolModel pool)
    {
        try { InvalidateMethod?.Invoke(pool, null); }
        catch (Exception exception)
        {
            Log.Warn($"[CuteSakikoModRandom] 刷新卡池缓存失败：{exception.Message}");
        }
    }
}

/// <summary>
/// 开局时生成并安装定义。
/// ⚠️ 时机：必须在 RunState 已建好、卡牌开始渲染之前（RunStartedEvent）。
/// </summary>
internal static class RandomRunInstaller
{
    internal static void Init()
    {
        RitsuLibFramework.SubscribeLifecycle<RunStartedEvent>(evt =>
        {
            // ★ 进度层（AA 原生 API，官方 wiki 外部角色接入.md:108 指定）：
            //   三个角色加起来 255 张卡，用**一个**进度层统一报数，生成期间主线程靠
            //   DisplayServer.ProcessEvents() 出帧（与 AA 内部 ChaosRunDefinitions.cs:652-666 同款写法）。
            // 正常情况下 RandomEarlyProgressPatch 已经在过场里生成完了（进度层也放过）；
            // 这里只是兜底：真没生成过才建一个进度层补上。
            var neededProfiles = ProfilesForRun(evt.RunState);
            var needGenerate = neededProfiles.Any(profile => !RandomRunState.IsInstalled(profile));
            AutoAnthony.IComponentGenerationProgress? fallbackProgress = null;
            if (needGenerate)
            {
                var total = neededProfiles.Sum(RandomCardSlots.CountFor);
                fallbackProgress = AutoAnthony.ComponentGenerationProgressApi.Create(total);
                RandomProgressHub.Begin(fallbackProgress);
            }

            try
            {
                InstallFor(evt.RunState);
            }
            catch (Exception exception)
            {
                // 安装失败不能让游戏崩：本局退回原版卡池，只记日志。
                Log.Error($"[CuteSakikoModRandom] 本局安装随机卡失败，退回原版卡池：{exception}");
            }
            finally
            {
                if (fallbackProgress is not null)
                {
                    RandomProgressHub.End();
                    fallbackProgress.Dispose();
                }
            }
        });

        // ★ 对局结束/放弃 → 清掉本局状态，卡池恢复本体卡（对齐参考实现：小睦局外图鉴是本体卡）。
        RitsuLibFramework.SubscribeLifecycle<RunEndedEvent>(_ =>
        {
            try { RandomRunState.Clear(); }
            catch (Exception exception) { Log.Warn($"[CuteSakikoModRandom] 清理本局状态失败：{exception.Message}"); }
        });
    }

    /// <summary>
    /// 本局需要生成的 profile：设置开启时只取本局玩家实际使用的角色（多人包含队友）；
    /// 设置关闭时仍然生成三个。没有任何随机角色时返回三个兜底。
    /// </summary>
    private static IReadOnlyList<string> ProfilesForRun(RunState runState)
    {
        if (!RandomConfig.OnlySelectedCharacters) return RandomProfiles.All;

        var profiles = runState.Players
            .Select(RandomRunState.CharacterProfileOf)
            .Where(profile => profile is not null)
            .Select(profile => profile!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return profiles.Length > 0 ? profiles : RandomProfiles.All;
    }

    private static void InstallFor(RunState runState)
    {
        var profiles = ProfilesForRun(runState);
        if (profiles.Count == 0) return;

        var seed = RunManager.Instance?.DebugOnlyGetState()?.Rng?.Seed ?? (ulong)Environment.TickCount;
        foreach (var profile in profiles)
            RandomRunState.EnsureInstalled(profile,
                seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
                reuseExisting: true);

        // ★ 装完定义后：① 卡池缓存失效 ② **ModelDb.AllCards 缓存失效**（否则图鉴只有初始牌组那 10 张）
        ModelDbCache.Invalidate();

        // ★★ 2026-11-11 兜底（用户反馈「第二次开新局初始牌组变回本体卡」）：
        //   StartingDeck 补丁在个别时序下会被跳过（例如先读档再开新局），
        //   这里在 RunStartedEvent 再做一次最终校验：只要设置开着且牌组里一张随机卡都没有，
        //   就地把牌组换成随机卡（Player.Deck 是 CardPile，可直接改 Cards）。
        try
        {
            if (RandomConfig.RandomStartingDeck)
            {
                foreach (var player in runState.Players)
                {
                    var profileId = RandomRunState.CharacterProfileOf(player);
                    if (profileId is null) continue;
                    var deck = player.Deck;
                    if (deck is null || deck.Cards.Count == 0
                        || deck.Cards.Any(card => card is AutoAnthony.ExternalChaosCardModel)) continue;

                    var slots = RandomCardSlots.For(profileId);
                    var replacements = new List<MegaCrit.Sts2.Core.Models.CardModel>();
                    for (var i = 0; i < 10 && slots.Count > 0; i++)
                    {
                        var model = MegaCrit.Sts2.Core.Models.ModelDb
                            .GetById<MegaCrit.Sts2.Core.Models.CardModel>(
                                MegaCrit.Sts2.Core.Models.ModelDb.GetId(slots[i % slots.Count]));
                        if (model is not null) replacements.Add(model);
                    }

                    if (replacements.Count == 0) continue;
                    // CardPile.Cards 是只读视图，真正的列表是私有字段 _cards
                    var listField = typeof(MegaCrit.Sts2.Core.Entities.Cards.CardPile)
                        .GetField("_cards", System.Reflection.BindingFlags.Instance
                                            | System.Reflection.BindingFlags.NonPublic);
                    if (listField?.GetValue(deck) is List<MegaCrit.Sts2.Core.Models.CardModel> list)
                    {
                        list.Clear();
                        list.AddRange(replacements);
                    }
                    Log.Info($"[CuteSakikoModRandom] 开局兜底：把 {RandomProfiles.DisplayNameFor(profileId)} "
                             + $"的初始牌组强制换成 {replacements.Count} 张随机卡。");
                }
            }
        }
        catch (Exception exception)
        {
            Log.Warn($"[CuteSakikoModRandom] 初始牌组兜底失败：{exception.Message}");
        }

        // 装完定义后让卡池重算 AllCards（内容由我们的 AllCards 补丁决定）
        RefreshPools();

        foreach (var profile in profiles)
            RandomRunState.VerifyKeywords(profile);
    }

    private static void RefreshPools()
    {
        try
        {
            var poolTypes = new[] { typeof(CuteSakiCardPool), typeof(CuteAnonCardPool), typeof(CuteRanaCardPool) };
            foreach (var type in poolTypes)
            {
                var pool = ModelDb.GetById<CardPoolModel>(ModelDb.GetId(type));
                RandomPoolCache.Refresh(pool);
            }
        }
        catch (Exception exception)
        {
            Log.Warn($"[CuteSakikoModRandom] 刷新卡池失败：{exception.Message}");
        }
    }
}









/// <summary>
/// 图鉴可见性 —— 这是「本体卡 ↔ 随机卡」在**卡牌图鉴**里互换的关键。
///
/// 读 sts2.dll 源码（NCardLibraryGrid.cs:159-167）：
///     图鉴列的卡片 = ModelDb.AllCards.Where(c => c.ShouldShowInCardLibrary)
/// 完全不经过 pool.AllCards。所以只要在这个属性上做文章即可：
///   · 本局随机激活时：本体卡 → 不显示；随机卡 → 显示；
///   · 局外 / 对局结束：反过来（恢复本体卡，对齐参考实现的行为）。
/// </summary>
[HarmonyPatch(typeof(CardModel), "get_ShouldShowInCardLibrary")]
internal static class RandomLibraryVisibilityPatch
{
    private static void Postfix(CardModel __instance, ref bool __result)
    {
        try
        {
            var isRandom = __instance is AutoAnthony.ExternalChaosCardModel;
            var pool = __instance.Pool;
            var profileId = RandomPoolCardsPatch.ProfileOf(pool);
            if (profileId is null) return;

            var active = RandomRunState.IsRunActive(profileId);
            __result = isRandom ? active : !active;
        }
        catch
        {
            // Pool 解析失败时保持原样
        }
    }
}



























