using AutoAnthony;
using ChaosCardGenerator;
using CuteSakikoMod.CuteSakikoModCode.Pools.Anon;
using CuteSakikoMod.CuteSakikoModCode.Pools.Rana;
using CuteSakikoMod.CuteSakikoModCode.Pools.Saki;
using MegaCrit.Sts2.Core.Models;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 卡图来源 + 语义匹配（2026-11-09 用户要求「卡图接语义匹配」）。
///
/// 做法：给本角色卡池里每张真实卡建一份「语义标签」，
/// 再给生成出来的随机卡算同样的标签，最后挑**标签最接近**的那张卡面。
///
/// 语义标签从哪来：
///   · 真实卡：直接读它的 CardType / TargetType / 是否有格挡 / 稀有度
///   · 随机卡：读它的 GeneratedCard.Operations（伤害 / 格挡 / 抽牌 / 增益 / 减益）
/// 这样「打伤害的随机卡」会配上「攻击牌卡面」，「给格挡的随机卡」会配上「防御牌卡面」，
/// 观感与本体一致，而不是纯随机乱配。
/// </summary>
internal static class RandomPortraits
{
    /// <summary>一张真实卡面的语义档案。</summary>
    private sealed record Portrait(string Path, SemanticTag Tag);

    /// <summary>语义标签：用位标记，方便算相似度。</summary>
    [Flags]
    private enum SemanticTag
    {
        None = 0,
        Attack = 1 << 0,
        Skill = 1 << 1,
        Power = 1 << 2,
        SingleTarget = 1 << 3,
        AllTargets = 1 << 4,
        GivesBlock = 1 << 5,
        GivesBuff = 1 << 6,
        GivesDebuff = 1 << 7,
        DrawsCards = 1 << 8,
        Rare = 1 << 9,
        Uncommon = 1 << 10,
        Common = 1 << 11
    }

    private static readonly Dictionary<string, IReadOnlyList<Portrait>> Cache = new(StringComparer.Ordinal);

    /// <summary>该角色所有卡面（带语义标签）。</summary>
    /// <summary>
    /// 该角色的卡面列表。★ 2026-11-11 用户要求：**先古卡面只给先古卡，普通卡面只给普通卡**，
    /// 不能互相串（先古卡框是金色横幅，普通卡框布局不同，混用会错位）。
    /// ancient=true 时只返回先古卡面。
    /// </summary>
    private static IReadOnlyList<Portrait> ProfilesOf(string profileId) => ProfilesOf(profileId, false);

    private static IReadOnlyList<Portrait> ProfilesOf(string profileId, bool ancient)
    {
        var key = ancient ? profileId + ":ancient" : profileId;
        lock (Cache)
        {
            if (Cache.TryGetValue(key, out var cached)) return cached;

            var list = new List<Portrait>();
            try
            {
                foreach (var card in RandomPoolBaseIds.CardsFor(profileId))
                {
                    // ★ 排除先古卡（2026-11-09 用户反馈「不要把先古卡图随机到普通卡上，
                    //    尺寸大小不对」）：先古卡的卡框是金色横幅样式，布局与普通卡不同，
                    //    混进普通池会错位。先古卡的 PortraitPath 只给先古槽位用。
                                    // 稀有度分组：先古卡面单独收集（只给先古卡用），其余只收集普通卡面
                var isAncient = card.Rarity == MegaCrit.Sts2.Core.Entities.Cards.CardRarity.Ancient;
                if (isAncient != ancient) continue;
                    // 同理排除状态/诅咒/衍生物——它们的图也不适合当普通卡面。
                    if (card.Rarity is MegaCrit.Sts2.Core.Entities.Cards.CardRarity.Status
                        or MegaCrit.Sts2.Core.Entities.Cards.CardRarity.Curse) continue;

                    var path = card.PortraitPath;
                    if (string.IsNullOrWhiteSpace(path)) continue;
                    if (list.Any(p => string.Equals(p.Path, path, StringComparison.Ordinal))) continue;

                    var tag = SemanticTag.None;
                    tag |= card.Type switch
                    {
                        MegaCrit.Sts2.Core.Entities.Cards.CardType.Attack => SemanticTag.Attack,
                        MegaCrit.Sts2.Core.Entities.Cards.CardType.Power => SemanticTag.Power,
                        _ => SemanticTag.Skill
                    };
                    tag |= card.TargetType switch
                    {
                        MegaCrit.Sts2.Core.Entities.Cards.TargetType.AnyEnemy => SemanticTag.SingleTarget,
                        MegaCrit.Sts2.Core.Entities.Cards.TargetType.AllEnemies => SemanticTag.AllTargets,
                        _ => SemanticTag.None
                    };
                    if (card.GainsBlock) tag |= SemanticTag.GivesBlock;
                    tag |= card.Rarity switch
                    {
                        MegaCrit.Sts2.Core.Entities.Cards.CardRarity.Rare => SemanticTag.Rare,
                        MegaCrit.Sts2.Core.Entities.Cards.CardRarity.Uncommon => SemanticTag.Uncommon,
                        _ => SemanticTag.Common
                    };

                    list.Add(new Portrait(path, tag));
                }
            }
            catch (Exception)
            {
                // 读不到卡面不致命：退回空路径，AA 会用默认图。
            }

            Cache[key] = list;
            return list;
        }
    }

    /// <summary>
    /// 为一张随机卡挑最匹配的卡面。
    /// 先按语义相似度排序，同分时按槽位错开，避免同一池里连续两张一样。
    /// </summary>
    internal static string Pick(string profileId, GeneratedCard card, int slot, ISet<string> used)
    {
        // 设置：随机卡图关闭时不给卡面，AA 会用默认图。
        if (!RandomConfig.RandomPortrait) return string.Empty;
        var isAncientCard = string.Equals(card.Rarity.ToString(), "Ancient", StringComparison.Ordinal);
        var portraits = ProfilesOf(profileId, isAncientCard);
        if (portraits.Count == 0) return string.Empty;

        var wanted = TagOf(card);

        // 相似度 = 相同位的个数；再优先未使用过的。
        var ranked = portraits
            .Select((portrait, index) => new
            {
                portrait.Path,
                Score = Similarity(wanted, portrait.Tag),
                Order = (index + slot) % portraits.Count
            })
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Order)
            .ToArray();

        foreach (var entry in ranked)
        {
            if (used.Add(entry.Path)) return entry.Path;
        }

        // 全用完了允许重复（卡比图多时）：按序号**轮转**，不要永远返回第一张
        // （否则整池卡图会变成同一张，用户实测过「全是一张蓝发图」）。
        return ranked[slot % ranked.Length].Path;
    }

    /// <summary>把随机卡的 operations 转成语义标签。</summary>
    private static SemanticTag TagOf(GeneratedCard card)
    {
        var tag = card.Type switch
        {
            GeneratedCardType.Attack => SemanticTag.Attack,
            GeneratedCardType.Power => SemanticTag.Power,
            _ => SemanticTag.Skill
        };

        tag |= card.Target switch
        {
            TargetMode.SingleEnemy => SemanticTag.SingleTarget,
            _ => SemanticTag.None
        };

        tag |= card.Rarity switch
        {
            GeneratedRarity.Rare or GeneratedRarity.Ancient => SemanticTag.Rare,
            GeneratedRarity.Uncommon => SemanticTag.Uncommon,
            _ => SemanticTag.Common
        };

        foreach (var operation in card.Operations)
        {
            OperationRuntimeSpec? spec = null;
            try
            {
                spec = OperationRuntimeSpecCompiler.GetOrCompile(operation);
            }
            catch (Exception)
            {
                continue;
            }

            if (spec is null) continue;
            switch (spec.Opcode)
            {
                case "deal_damage":
                    tag |= SemanticTag.Attack;
                    break;
                case "gain_block":
                    tag |= SemanticTag.GivesBlock;
                    break;
                case "draw_cards":
                    tag |= SemanticTag.DrawsCards;
                    break;
                case "apply_power":
                    tag |= SemanticTag.GivesBuff;
                    break;
                case "gain_energy":
                case "gain_stars":
                case "heal":
                    tag |= SemanticTag.GivesBuff;
                    break;
            }

            if (spec.Opcode is "apply_power" && spec.Target is "selected_enemy" or "all_enemies")
                tag |= SemanticTag.GivesDebuff;
        }

        return tag;
    }

    private static int Similarity(SemanticTag left, SemanticTag right)
    {
        var value = (int)left & (int)right;
        var count = 0;
        while (value != 0)
        {
            count += value & 1;
            value >>= 1;
        }

        return count;
    }

    private static CardPoolModel PoolOf(string profileId) => profileId switch
    {
        RandomProfiles.Saki => ModelDb.CardPool<CuteSakiCardPool>(),
        RandomProfiles.Anon => ModelDb.CardPool<CuteAnonCardPool>(),
        RandomProfiles.Rana => ModelDb.CardPool<CuteRanaCardPool>(),
        _ => throw new ArgumentOutOfRangeException(nameof(profileId), profileId, "Unknown random profile.")
    };
}




