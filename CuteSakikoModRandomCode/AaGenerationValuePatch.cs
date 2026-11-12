using ChaosCardGenerator;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 把「永久性 power 不在 Power 牌上 → 价值 ×1.5」同步进 **AA 自己的整卡预算模型**，
/// 让随机生成阶段就把它当更贵的东西处理，从源头上不超额（2026-11-12 用户要求）。
///
/// 为什么不能直接注册到 <c>ComponentValuationApi</c>：
///   AA 的公开估值接口 <c>IComponentValuation.Estimate</c> 拿到的
///   <c>ComponentValuationContext</c> 只有 Template / Scope / RuntimeSpec，
///   **没有卡牌类型**（ComponentValuationApi.cs:3-6），所以无法表达「只在非 Power 卡上 ×1.5」。
///
/// AA 自己的「原版数值」机制在哪：
///   · <c>EffectBalanceModel.EstimatedNetCardValue(operations, …, cardType, …)</c>
///     —— 整卡价值，**带卡牌类型**（EffectBalanceModel.cs:1442-1453）；
///   · <c>ComponentAssemblyGenerator.ApplyWholeCardBudgetEnvelope(...)</c>
///     —— 按稀有度/费用/卡牌类型算出上下限，超出就缩放数值或重掷
///     （ComponentAssemblyGenerator.cs:1879-1942；<c>_balancedValues</c> 就是这个开关）；
///   · 它内部已经有一条同类规则：<c>ClampReusablePersistentCombatGains</c>
///     （ComponentAssemblyGenerator.cs:1190-1207）—— 非 Power 且非 Exhaust 时，
///     把可叠加的永久战斗增益（力量/敏捷/覆甲等）硬夹到 1 层（费用≥2 时 2 层）。
///
/// ⇒ 我们复现「价值侧」的那一半：在 <c>EstimatedPositiveCardValue(operations, …, cardType, …)</c>
///   的 Postfix 里，把非 Power 卡上的永久 power 原子价值补 +50%，
///   这样整卡预算（含它的缩放/重掷）就会自动把它当更贵，生成结果不会超额。
///   注意：不碰 AA 的公开估值表，避免把 Power 牌自己也一起涨价。
/// </summary>
internal static class AaGenerationValuePatch
{
    /// <summary>AA 原生单位：永久力量/敏捷 = 1200/层；覆甲 = 435/层（EffectBalanceModel.cs:472/483）。</summary>
    private const double NativePermanentStatValue = 1200d;
    private const double NativePlatingValue = 435d;

    private static int _fires;

    /// <summary>取走「本次生成期间 ×1.5 计价触发次数」并清零（给日志验收用）。</summary>
    internal static int TakeFires() => Interlocked.Exchange(ref _fires, 0);

    internal static void Install(Harmony harmony)
    {
        try
        {
            // A/B 验收开关：设 SAKI_AA_POWER_BOOST=0 时禁用本补丁，用来对照同一 seed 的生成结果。
            if (Environment.GetEnvironmentVariable("SAKI_AA_POWER_BOOST") == "0")
            {
                Log.Info("[CuteSakikoModRandom] AA 整卡预算同步已按环境变量禁用（A/B 对照）。");
                return;
            }

            var type = AccessTools.TypeByName("ChaosCardGenerator.EffectBalanceModel");
            if (type is null)
            {
                Log.Warn("[CuteSakikoModRandom] 找不到 AA 的 EffectBalanceModel，跳过整卡预算同步。");
                return;
            }

            var method = AccessTools.Method(type, "EstimatedPositiveCardValue", new[]
            {
                typeof(IReadOnlyList<GeneratorOperation>),
                typeof(bool),
                typeof(GeneratedCardType),
                typeof(IReadOnlyCollection<CardTag>)
            });

            if (method is null)
            {
                Log.Warn("[CuteSakikoModRandom] AA 版本里找不到 "
                         + "EstimatedPositiveCardValue(operations, bool, GeneratedCardType, tags)，"
                         + "整卡预算同步跳过（生成仍可用，只是不提前计价）。");
                return;
            }

            harmony.Patch(method, postfix: new HarmonyMethod(
                typeof(AaGenerationValuePatch), nameof(Postfix)));
            Log.Info("[CuteSakikoModRandom] 已接入 AA 整卡预算：永久 power 在非 Power 卡上按 ×1.5 计价。");
        }
        catch (Exception exception)
        {
            Log.Warn($"[CuteSakikoModRandom] 接入 AA 整卡预算失败（不影响其它功能）：{exception.Message}");
        }
    }

    /// <summary>
    /// 给「非 Power 卡上的永久 power」补 50% 价值（AA 单位）。
    /// 本回合型 power（strength_this_turn / vigor）不加，和用户口径一致。
    /// </summary>
    private static void Postfix(IReadOnlyList<GeneratorOperation> operations, GeneratedCardType cardType,
        ref double __result)
    {
        try
        {
            if (cardType == GeneratedCardType.Power || operations is null || operations.Count == 0) return;

            var extra = 0d;
            foreach (var operation in operations)
            {
                var spec = OperationRuntimeSpecCompiler.GetOrCompile(operation);
                if (spec.Opcode == "apply_power")
                {
                    var amount = spec.Values.FirstOrDefault(value => value.Id == "amount")?.BaseValue ?? 0;
                    if (amount <= 0) continue;
                    var unit = spec.Variant switch
                    {
                        "strength" or "dexterity_gain" => NativePermanentStatValue,
                        "plating" => NativePlatingValue,
                        _ => 0d
                    };
                    if (unit > 0d) extra += amount * unit * 0.5d;
                }
                else if (spec.Opcode == "csk_power")
                {
                    // 本模组永久能力：用我们自己注册给 AA 的估值表反查，再补 50%。
                    extra += SakiValuations.EstimateFor(spec) * 0.5d;
                }
            }

            if (extra > 0d)
            {
                __result += extra;
                Interlocked.Increment(ref _fires);
            }
        }
        catch
        {
            // 预算是生成热路径，补丁自身绝不能抛异常。
        }
    }
}
