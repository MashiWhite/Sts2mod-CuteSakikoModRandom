using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 2026-11-12 用户实测：进程重启后点「继续游戏」会报
///   External component definition 'cutesakikomod:saki' slot 0 is not installed.
///
/// 原因：定义只在 RunStartedEvent / 开局抢跑时安装；读档走
///   RunManager.InitializeSavedRun，此时 Definitions 还是空的，原逻辑会先校验失败。
/// 修法：在 InitializeSavedRun 之前，从 SerializableRun 里读存档种子和玩家角色，
///   为三个 mod 角色预装同一套定义，再让原逻辑恢复存档。
/// </summary>
[HarmonyPatch(typeof(RunManager), "InitializeSavedRun",
    new[] { typeof(SerializableRun) })]
internal static class RandomResumePatch
{
    [HarmonyPrefix]
    private static void Prefix(SerializableRun? save) => InstallForSave(save, "InitializeSavedRun");

    internal static void InstallForSave(SerializableRun? save, string source)
    {
        try
        {
            if (save is null) return;

            var seed = save.SerializableRng?.Seed;
            if (string.IsNullOrWhiteSpace(seed))
            {
                Log.Warn($"[CuteSakikoModRandom][读档/{source}] 存档没有种子，跳过随机池预装。");
                return;
            }

            foreach (var player in save.Players)
            {
                var profileId = ProfileOf(player.CharacterId?.Entry);
                if (profileId is null) continue;

                RandomRunState.EnsureInstalled(profileId, seed);
                Log.Info($"[CuteSakikoModRandom][读档/{source}] 已预装 {profileId}，seed={seed}");
            }
        }
        catch (Exception exception)
        {
            // 读档失败必须留日志；不能让异常吞掉原逻辑的报错。
            Log.Warn($"[CuteSakikoModRandom][读档/{source}] 预装随机池失败：{exception.Message}");
        }
    }

    private static string? ProfileOf(string? characterEntry)
    {
        if (string.IsNullOrEmpty(characterEntry)) return null;
        if (characterEntry.Contains("CUTE_SAKI", StringComparison.Ordinal))
            return RandomProfiles.Saki;
        if (characterEntry.Contains("CUTE_ANON", StringComparison.Ordinal))
            return RandomProfiles.Anon;
        if (characterEntry.Contains("CUTE_RANA", StringComparison.Ordinal))
            return RandomProfiles.Rana;
        return null;
    }
}

/// <summary>
/// 读档链更早的入口：SaveManager.LoadRunSave 返回 SerializableRun 后、
/// 调用方构造 RunState / 恢复卡牌之前，先把定义装好。
/// 2026-11-12 实测：只挂 InitializeSavedRun 仍会先报 slot 0 not installed，
/// 所以这里必须再挂一道。
/// </summary>
[HarmonyPatch(typeof(SaveManager), nameof(SaveManager.LoadRunSave))]
internal static class RandomResumeLoadSavePatch
{
    [HarmonyPostfix]
    private static void Postfix(ReadSaveResult<SerializableRun> __result)
    {
        if (__result is null || !__result.Success || __result.SaveData is null) return;
        RandomResumePatch.InstallForSave(__result.SaveData, "LoadRunSave");
    }
}

[HarmonyPatch(typeof(SaveManager), nameof(SaveManager.LoadAndCanonicalizeMultiplayerRunSave))]
internal static class RandomResumeMultiplayerSavePatch
{
    [HarmonyPostfix]
    private static void Postfix(ReadSaveResult<SerializableRun> __result)
    {
        if (__result is null || !__result.Success || __result.SaveData is null) return;
        RandomResumePatch.InstallForSave(__result.SaveData, "LoadAndCanonicalizeMultiplayerRunSave");
    }
}
