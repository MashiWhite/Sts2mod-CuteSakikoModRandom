using AutoAnthony;
using ChaosCardGenerator;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 本体专属能力的运行时执行器。
///
/// 为什么不用 AA 的 <c>apply_power</c>：
///   那个 opcode 在 ChaosOperationExecutor 里是**硬编码 variant 分派**
///   （retain_hand_this_turn / vigor / weak …），只认原版能力，
///   加载不了任意 mod 的 Power 类型。
///   ⇒ 我们自己开 <c>csk_power</c>，variant = 能力短名，按 <see cref="SakiPowerCatalog"/>
///     的清单反射出 Type 再 Apply。
/// </summary>
internal sealed class ModPowerHandler : IComponentRuntimeHandler
{
    private readonly string _profileId;

    public ModPowerHandler(string profileId) => _profileId = profileId;

    public async Task<bool> ExecuteAsync(ComponentRuntimeContext context)
    {
        var powerType = Resolve(_profileId, context.RuntimeSpec.Variant);
        if (powerType is null) return true;

        var amount = Math.Max(1, context.Amount);
        var owner = context.Card.Owner;

        // ★ 减益类能力（Status）必须打在**敌人**身上；打在自己身上就是纯负面卡（2026-11-10 用户实测
        //   「心必得：获得2层破防」——既无意义，又因为类型名写错命名空间而完全没生效）。
        var entry = SakiPowerCatalog.For(_profileId)
            .FirstOrDefault(power => string.Equals(power.Variant, context.RuntimeSpec.Variant, StringComparison.Ordinal));
        var applyTarget = owner.Creature;
        if (entry.TargetsEnemy)
        {
            var foes = owner.Creature?.CombatState?.HittableEnemies?.ToArray() ?? [];
            if (foes.Length > 0)
                applyTarget = foes[context.CardPlay.Target is { } t && !t.IsDead ? Array.IndexOf(foes, t) is var i && i >= 0 ? i : 0 : 0];
        }

        // ★ 必须精确挑重载（2026-11-09 实测：能力打不出去就是这里错了）
        //   PowerCmd.Apply<T> 有**两个**泛型重载，参数个数都是 6：
        //     Apply<T>(PlayerChoiceContext, IEnumerable<Creature>, decimal, Creature, CardModel, bool)
        //     Apply<T>(PlayerChoiceContext, Creature,             decimal, Creature, CardModel, bool)
        //   原来按「参数 >= 5」取第一个，会命中 IEnumerable 版本，
        //   把单个 Creature 传进 IEnumerable 参数 → 反射抛异常 → 被 catch 吞掉
        //   → 表现成「能力图标显示了但根本不生效」。
        //   ⇒ 这里按第 2 个参数**精确匹配 Creature**。
        var method = typeof(PowerCmd)
            .GetMethods()
            .FirstOrDefault(m => m.Name == nameof(PowerCmd.Apply)
                                 && m.IsGenericMethodDefinition
                                 && m.GetParameters() is { Length: 6 } p
                                 && p[0].ParameterType == typeof(PlayerChoiceContext)
                                 && p[1].ParameterType == typeof(Creature)
                                 && p[2].ParameterType == typeof(decimal)
                                 && p[3].ParameterType == typeof(Creature));

        if (method is null)
        {
            Log.Warn($"[CuteSakikoModRandom] 找不到 PowerCmd.Apply<T> 的 Creature 重载，能力 {context.RuntimeSpec.Variant} 未生效。");
            return true;
        }

        try
        {
            var generic = method.MakeGenericMethod(powerType);
            // 尾随 bool 参数：本体卡调用时都省略（有默认值），这里显式传 false。
            var args = new object?[]
            {
                context.ChoiceContext,
                applyTarget,
                (decimal)amount,
                owner.Creature,
                context.Card,
                false
            };

            if (generic.Invoke(null, args) is Task task)
                await task;
        }
        catch (Exception exception)
        {
            // 不再静默：能力没生效是最难查的一类问题，必须在日志里留痕。
            Log.Warn($"[CuteSakikoModRandom] 施加本体能力失败 {powerType.Name}"
                     + $"(variant={context.RuntimeSpec.Variant})：{exception.GetBaseException().Message}");
        }

        return true;
    }

    /// <summary>按 variant 找到能力类型（清单里的完整类名 → Type）。</summary>
    /// <summary>
    /// 按已解析的 Type 施放能力（给「条件成立 → 获得随机能力」这类调用方复用）。
    /// 复用同一段「精确挑 PowerCmd.Apply&lt;T&gt; 的 Creature 重载」逻辑。
    /// </summary>
    internal static async Task ApplyByTypeAsync(Type powerType, PlayerChoiceContext choiceContext,
        Creature target, CardModel? source, int amount)
    {
        var method = typeof(PowerCmd)
            .GetMethods()
            .FirstOrDefault(m => m.Name == nameof(PowerCmd.Apply)
                                 && m.IsGenericMethodDefinition
                                 && m.GetParameters() is { Length: 6 } p
                                 && p[0].ParameterType == typeof(PlayerChoiceContext)
                                 && p[1].ParameterType == typeof(Creature)
                                 && p[2].ParameterType == typeof(decimal)
                                 && p[3].ParameterType == typeof(Creature));
        if (method is null) return;

        try
        {
            var generic = method.MakeGenericMethod(powerType);
            var args = new object?[] { choiceContext, target, (decimal)Math.Max(1, amount), target, source, false };
            if (generic.Invoke(null, args) is Task task) await task;
        }
        catch (Exception exception)
        {
            Log.Warn($"[CuteSakikoModRandom] 施放能力 {powerType.Name} 失败：{exception.Message}");
        }
    }

    internal static Type? Resolve(string profileId, string variant)
    {
        foreach (var power in SakiPowerCatalog.For(profileId))
        {
            if (!string.Equals(power.Variant, variant, StringComparison.Ordinal)) continue;
            var type = Type.GetType(power.TypeName)
                       ?? AppDomain.CurrentDomain.GetAssemblies()
                           .Select(assembly => assembly.GetType(power.TypeName))
                           .FirstOrDefault(found => found is not null);
            return type;
        }

        return null;
    }

    /// <summary>生成调用本执行器的路由（每个角色一套 variant）。</summary>
    internal static IReadOnlyList<ComponentRuntimeRoute> RoutesFor(string profileId)
    {
        var handler = new ModPowerHandler(profileId);
        return SakiPowerCatalog.For(profileId)
            .Select(power => new ComponentRuntimeRoute(SakiPowerCatalog.Opcode, power.Variant, handler))
            .ToArray();
    }
}




