using CuteSakikoMod.CuteSakikoModCode.Pools.Anon;
using CuteSakikoMod.CuteSakikoModCode.Pools.Rana;
using CuteSakikoMod.CuteSakikoModCode.Pools.Saki;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 三个角色的固定卡槽类型。
///
/// AutoAnthony 要求把每个槽位声明成**具体的 C# 类型**（不能运行时生成），因为 ModelDb 需要稳定的 ModelId。
/// 槽位号必须从 0 开始连续，数量与真实卡池的可玩牌数一致。
/// </summary>
internal static class SlotCount
{
    // ⚠️ 这三个数字必须与「本体真实卡池的可玩牌数」完全一致，否则 InstallDefinitions 会
    // 因为「定义数量 != 运行期宿主声明的 CardCount」而拒收整个卡池。
    // 统计口径：Cards\<角色>\ 下所有 .cs，排除 Status/Token/Curse，以及 CuteSakikoModCard /
    // CuteAnonCard / CuteRanaCard / SakiMemoryCard / NekoCard 这些基类。
    // 2026-11-09 实测：祥子 96（含 Memory/ 目录 5 张）、爱音 81、乐奈 78。
    // 本体加卡后必须回来改这里并重新生成卡槽类。
    public const int Saki = 96;
    public const int Anon = 81;
    public const int Rana = 78;
}

/// <summary>祥子（CuteSaki）的随机卡槽基类。</summary>
public abstract class SakiRandomCard : AutoAnthony.ExternalChaosCardModel
{
    protected sealed override string ComponentProfileId => RandomProfiles.Saki;
    public sealed override CardPoolModel Pool => ModelDb.CardPool<CuteSakiCardPool>();
}

/// <summary>爱音（CuteAnon）的随机卡槽基类。</summary>
public abstract class AnonRandomCard : AutoAnthony.ExternalChaosCardModel
{
    protected sealed override string ComponentProfileId => RandomProfiles.Anon;
    public sealed override CardPoolModel Pool => ModelDb.CardPool<CuteAnonCardPool>();
}

/// <summary>乐奈（CuteRana）的随机卡槽基类。</summary>
public abstract class RanaRandomCard : AutoAnthony.ExternalChaosCardModel
{
    protected sealed override string ComponentProfileId => RandomProfiles.Rana;
    public sealed override CardPoolModel Pool => ModelDb.CardPool<CuteRanaCardPool>();
}

/// <summary>
/// 按 ProfileId 查槽位类型表（反射收集，避免手写 250 个条目）。
///
/// ⚠️ 这里**只做反射收集**，绝不触碰 ModelDb。AutoAnthony 的 RegisterRuntime 会在
/// 模组初始化阶段就遍历这张表做类型校验（此时 ModelDb 还没就绪），
/// 所以任何 ModelDb.CardPool&lt;T&gt;() 调用都必须留在卡槽类的 <c>Pool</c> 属性里
/// （那是「用到时才取」，不会在注册期触发）。
/// </summary>
internal static class RandomCardSlots
{
    private static readonly Type[] SakiTypes = Build(typeof(SakiRandomCard));
    private static readonly Type[] AnonTypes = Build(typeof(AnonRandomCard));
    private static readonly Type[] RanaTypes = Build(typeof(RanaRandomCard));

    internal static IReadOnlyList<Type> For(string profileId) => profileId switch
    {
        RandomProfiles.Saki => SakiTypes,
        RandomProfiles.Anon => AnonTypes,
        RandomProfiles.Rana => RanaTypes,
        _ => throw new ArgumentOutOfRangeException(nameof(profileId), profileId, "Unknown random profile.")
    };

    internal static int CountFor(string profileId) => For(profileId).Count;

    private static readonly Dictionary<string, CardModel[]> CardCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, ModelId[]> IdCache = new(StringComparer.Ordinal);

    /// <summary>该 profile 的随机卡模型数组（缓存：卡池 getter 每帧可能被问很多次，掉帧就是这里来的）。</summary>
    internal static CardModel[] CardsFor(string profileId)
    {
        lock (CardCache)
        {
            if (CardCache.TryGetValue(profileId, out var cached)) return cached;

            var array = For(profileId)
                .Select(type => ModelDb.GetById<CardModel>(ModelDb.GetId(type)))
                .ToArray();
            CardCache[profileId] = array;
            return array;
        }
    }

    /// <summary>该 profile 的随机卡 id 数组（缓存）。</summary>
    internal static ModelId[] IdsFor(string profileId)
    {
        lock (IdCache)
        {
            if (IdCache.TryGetValue(profileId, out var cached)) return cached;

            var array = For(profileId).Select(ModelDb.GetId).ToArray();
            IdCache[profileId] = array;
            return array;
        }
    }

    internal static Type TypeForSlot(string profileId, int slot)
    {
        var types = For(profileId);
        if ((uint)slot >= (uint)types.Count)
            throw new ArgumentOutOfRangeException(nameof(slot), slot,
                $"{profileId} has no generated card slot {slot}.");
        return types[slot];
    }

    private static Type[] Build(Type baseType) => baseType.Assembly.GetTypes()
        .Where(type => !type.IsAbstract && type.BaseType == baseType)
        .OrderBy(type => type.Name, StringComparer.Ordinal)
        .ToArray();
}

