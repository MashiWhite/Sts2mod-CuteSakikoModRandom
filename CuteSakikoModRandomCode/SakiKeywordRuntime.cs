using AutoAnthony;
using ChaosCardGenerator;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using STS2RitsuLib.Keywords;
using CutesakiKeywords = CuteSakikoMod.CuteSakikoModCode.Others.CutesakiKeywords;
using GameCardTag = MegaCrit.Sts2.Core.Entities.Cards.CardTag;
using GeneratorCardTag = ChaosCardGenerator.CardTag;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 自定义关键词的**运行期投影**。
///
/// ★★★ 为什么必须有这一层（2026-11-11 用户第 3 条根因）：
///   AutoAnthony 的 <c>ChaosCardModel.CanonicalKeywords</c> 只做两件事：
///     ① 把 <c>Generated.Tags</c> 里的原生 tag 映射成 CardKeyword；
///     ② 把 <c>Generated.CustomKeywords</c> 里的 id 交给
///        <c>ComponentKeywordRuntimeApi</c> 的 adapter 去投影。
///   **只往 CustomKeywords 里写字符串是没用的** —— 没有 adapter 就一个关键词都不会进
///   <c>CardModel.Keywords</c>，于是「名字叫琴奏」的卡既不算琴奏、也不触发琴奏计数。
///
/// 这里给「名字/哈希会挂上的关键词」注册 adapter：
///   祥子：Playpiano（琴奏，打击类）+ Sword（出鞘，哈希挂载）
///   爱音：Playguitar（弦奏，打击类）
///   乐奈：无（本体基本攻击没有自定义关键词）
///
/// ⚠️ 注册时机：必须在 <c>ComponentPackageApi.Register</c> 之后（关键词定义先存在）、
///    且在第一次卡牌查询之前。RandomIntegration.RegisterProfile 里同步调用。
/// </summary>
internal static class SakiKeywordRuntime
{
    private static readonly object Sync = new();
    private static readonly HashSet<string> Registered = new(StringComparer.Ordinal);

    internal static void Register(string profileId)
    {
        lock (Sync)
        {
            if (!Registered.Add(profileId)) return;
        }

        try
        {
            var routes = RoutesFor(profileId);
            if (routes.Count == 0) return;

            // Sword（出鞘）不在 StrikeKeywordsFor 里（它不该出现在所有基础攻击上），
            // 但 RandomDefinitionBuilder 会按稳定哈希给部分复合卡挂它 —— 所以定义要补登一次。
            foreach (var route in routes.Where(route => route.EnsureDefinition))
            {
                if (ComponentKeywordApi.IsRegistered(route.Id)) continue;
                ComponentKeywordApi.Register(new ComponentKeywordDefinition(
                    route.Id,
                    new ProfileKeywordRule(profileId),
                    ChineseName: route.ChineseName,
                    EnglishName: route.EnglishName));
            }

            ComponentKeywordRuntimeApi.RegisterPackage($"csk:keywords:{profileId}", routes
                .Select(route => new ComponentKeywordRuntimeRegistration(
                    route.Id, new Adapter(route.Keyword, route.AddsStrikeTag, profileId)))
                .ToArray());

            Log.Info($"[CuteSakikoModRandom] 关键词运行期已注册：{profileId} "
                     + string.Join(", ", routes.Select(route => route.Id)));
        }
        catch (Exception exception)
        {
            // 关键词是增强项：注册失败不应拖垮整局生成，但必须留下日志（否则就是「静默名不副实」）。
            Log.Warn($"[CuteSakikoModRandom] 关键词运行期注册失败（{profileId}）：{exception.Message}");
        }
    }

    private sealed record Route(
        string Id,
        Func<CardKeyword> Keyword,
        bool AddsStrikeTag,
        bool EnsureDefinition,
        string ChineseName,
        string EnglishName);

    private static IReadOnlyList<Route> RoutesFor(string profileId)
    {
        var routes = new List<Route>();
        if (profileId == RandomProfiles.Saki)
        {
            routes.Add(new Route(
                CutesakiKeywords.Playpiano,
                CutesakiKeywords.Playpiano.GetModCardKeyword,
                AddsStrikeTag: true,
                EnsureDefinition: false,
                ChineseName: "琴奏",
                EnglishName: "Piano"));
            routes.Add(new Route(
                CutesakiKeywords.Sword,
                CutesakiKeywords.Sword.GetModCardKeyword,
                AddsStrikeTag: false,
                EnsureDefinition: true,
                ChineseName: "出鞘",
                EnglishName: "Unsheathe"));
        }
        else if (profileId == RandomProfiles.Anon)
        {
            routes.Add(new Route(
                CutesakiKeywords.Playguitar,
                CutesakiKeywords.Playguitar.GetModCardKeyword,
                AddsStrikeTag: true,
                EnsureDefinition: false,
                ChineseName: "弦奏",
                EnglishName: "Guitar"));
            // ★ 2026-11-11 用户口径：爱音升级可以随机获得「特殊演奏」关键词。
            routes.Add(new Route(
                CutesakiKeywords.NoNote,
                CutesakiKeywords.NoNote.GetModCardKeyword,
                AddsStrikeTag: false,
                EnsureDefinition: true,
                ChineseName: "特殊演奏",
                EnglishName: "Special Performance"));
        }
        else if (profileId == RandomProfiles.Rana)
        {
            // ★ 2026-11-11 用户口径：乐奈升级可以随机获得「莱芜」关键词。
            routes.Add(new Route(
                CutesakiKeywords.RanaLive,
                CutesakiKeywords.RanaLive.GetModCardKeyword,
                AddsStrikeTag: false,
                EnsureDefinition: true,
                ChineseName: "莱芜",
                EnglishName: "Live"));
        }

        return routes;
    }

    /// <summary>
    /// 2026-11-11 用户第 1 条：出鞘只属于祥子。
    /// AA 的 ComponentKeywordApi 是全局表，EnsureDefinition 注册的 Sword 默认
    /// CanAttach=true，于是会被采样到爱音/乐奈池子。这里按 profile 挡住。
    /// </summary>
    private sealed class ProfileKeywordRule(string profileId) : IComponentKeywordRule
    {
        public bool CanAttach(ComponentKeywordGenerationContext context) =>
            string.Equals(context.ProfileId, profileId, StringComparison.Ordinal);
    }

    /// <summary>把一个生成期关键词 id 投影成游戏内关键词 / 语义 tag / 悬停提示。</summary>
    private sealed class Adapter(Func<CardKeyword> keyword, bool addsStrikeTag, string profileId)
        : IComponentKeywordRuntimeAdapter
    {
        public IEnumerable<CardKeyword> CanonicalKeywords(ChaosCardModel card)
        {
            // ★ 2026-11-11 用户第 1 条：关键词 adapter 是**全局表**，
            //   必须自己按卡槽类型再挡一道，避免「出鞘」投影到别的角色卡上。
            if (!MatchesProfile(card)) yield break;
            yield return keyword();
        }

        public IEnumerable<GameCardTag> SemanticTags(ChaosCardModel card)
        {
            if (!MatchesProfile(card)) return [];
            // 基础攻击壳本身已经带 CardTag.Strike（recipe 里写死），不要重复 yield。
            if (!addsStrikeTag || card.Generated.Tags.Contains(GeneratorCardTag.Strike)) return [];
            return [GameCardTag.Strike];
        }

        public IEnumerable<IHoverTip> BuildHoverTips(ChaosCardModel card) =>
            MatchesProfile(card) ? [HoverTipFactory.FromKeyword(keyword())] : [];

        private bool MatchesProfile(ChaosCardModel card) => profileId switch
        {
            RandomProfiles.Saki => card is SakiRandomCard,
            RandomProfiles.Anon => card is AnonRandomCard,
            RandomProfiles.Rana => card is RanaRandomCard,
            _ => true
        };
    }
}
