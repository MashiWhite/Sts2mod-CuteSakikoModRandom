using System.Reflection;
using AutoAnthony;
using ChaosCardGenerator;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 2026-11-12 用户第 5 条：升级后的「获得回忆」卡面必须显示「回忆+」。
///
/// 为什么不能只改 Generated.Upgrade.UpgradedChineseDescription：
///   AA 的 ChaosCardModel 卡面走 ChaosRuntimeDescriptionRenderer.Render(card)，
///   是**从 operations 动态重算**的，不读 Upgrade.UpgradedChineseDescription。
///   AA 没有公开「按卡覆盖 operation 文本」的 API（ExternalOperationTextRegistry 是模板级全局），
///   所以这里用 Harmony 在渲染结果上做一次按卡替换。
/// </summary>
[HarmonyPatch]
internal static class MemoryPlusTextPatch
{
    [HarmonyPrepare]
    private static bool Prepare() => TargetMethod() is not null;

    private static MethodBase? TargetMethod() =>
        AccessTools.TypeByName("AutoAnthony.ChaosRuntimeDescriptionRenderer")
            ?.GetMethod("Render", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

    [HarmonyPostfix]
    private static void Postfix(ChaosCardModel card, bool chinese, ref string __result)
    {
        if (!chinese || !card.IsUpgraded || string.IsNullOrEmpty(__result)) return;

        // ★ 2026-11-12 用户口径：升级预览里「欺诈+」要是**绿色**，游戏局内才是金色。
        var preview = card.UpgradePreviewType.IsPreview();
        var text = __result;
        foreach (var operation in card.Generated.Operations)
        {
            OperationRuntimeSpec spec;
            try { spec = OperationRuntimeSpecCompiler.GetOrCompile(operation); }
            catch { continue; }

            if (spec.Opcode == SakiComponents.RecallOpcode)
                text = ReplacePlus(text, "回忆", preview);
            else if (spec.Opcode == SakiComponents.StatusOpcode)
            {
                text = ReplacePlus(text, "欺诈", preview);
                text = ReplacePlus(text, "逃避", preview);
            }
            else if (spec.Opcode == SakiComponents.TokenOpcode
                     && spec.Variant == SakiComponents.TokenNekoVariant)
                text = ReplacePlus(text, "猫咪", preview);
        }

        __result = text;
    }

    private static string ReplacePlus(string text, string name, bool preview)
    {
        if (text.Contains(name + "+", StringComparison.Ordinal)) return text;

        var gold = $"[gold]{name}[/gold]";
        if (text.Contains(gold, StringComparison.Ordinal))
        {
            return preview
                ? text.Replace(gold, $"{gold}[green]+[/green]", StringComparison.Ordinal)
                : text.Replace(gold, $"[gold]{name}+[/gold]", StringComparison.Ordinal);
        }

        return text.Replace(name, preview ? $"{name}[green]+[/green]" : $"{name}+",
            StringComparison.Ordinal);
    }
}
