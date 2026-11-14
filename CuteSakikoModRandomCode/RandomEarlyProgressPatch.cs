using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 开局时先「生成随机卡池」再真正开局，并在黑屏期间显示 AA 原生进度层。
///
/// ★ 这是**照抄 AA 自己的写法**（ChaosMode/Patches/RunAndPoolPatches.cs:2649 起，类 SeedBeforeSingleplayerPatch）：
///     Prefix 里 `__result = StartAfterGeneration(...); return false;`   ← 返回**未完成的 Task**，
///     引擎主循环继续跑 → 画面能刷新；生成在后台线程，主线程 while(!task.Wait(8)) 里
///     progress.Report + DisplayServer.ProcessEvents()；生成完再 `await game.StartNewSingleplayerRun(...)`。
///
/// 参考实现（小睦）也是这个套路（MzmAnthonyology/Patches/StartSingleplayerRunPatch.cs，
/// 带 `[HarmonyBefore("autoanthony.v111")]`，内部 StartAfterGeneration 先 await 生成再调原方法）。
///
/// 之前我们只在 Prefix 里**同步**生成 → 主循环被占住，进度层永远等不到一帧。
/// </summary>
[HarmonyPatch(typeof(NGame), nameof(NGame.StartNewSingleplayerRun))]
internal static class RandomEarlyProgressPatch
{
    private static int _callingOriginal;

    private static bool Prefix(
        NGame __instance,
#if STS2_AT_LEAST_0_113_0
        Player player,
#else
        CharacterModel character,
#endif
        bool shouldSave,
        IReadOnlyList<ActModel> acts,
        IReadOnlyList<ModifierModel> modifiers,
        string seed,
        GameMode gameMode,
#if STS2_AT_LEAST_0_113_0
        bool isCustomGame,
#endif
        int ascensionLevel,
        DateTimeOffset? dailyTime,
        ref Task<RunState> __result)
    {
        // 我们自己再调一次原方法时要放行，否则无限递归。
        if (Volatile.Read(ref _callingOriginal) > 0) return true;

        try
        {
            // 0.113 起首个参数是 Player（0.111 是 CharacterModel）。
#if STS2_AT_LEAST_0_113_0
            var profileId = player?.Character is null
                ? null
                : RandomProfiles.ProfileOfCharacterTypeName(player.Character.GetType().Name);
#else
            var profileId = character is null
                ? null
                : RandomProfiles.ProfileOfCharacterTypeName(character.GetType().Name);
#endif
            if (profileId is null) return true;
            if (!RandomConfig.RandomStartingDeck) return true;

#if STS2_AT_LEAST_0_113_0
            __result = StartAfterGeneration(__instance, player, shouldSave, acts, modifiers, seed,
                gameMode, isCustomGame, ascensionLevel, dailyTime);
#else
            __result = StartAfterGeneration(__instance, character, shouldSave, acts, modifiers, seed,
                gameMode, ascensionLevel, dailyTime);
#endif
            return false;
        }
        catch
        {
            return true;
        }
    }

    private static async Task<RunState> StartAfterGeneration(
        NGame game,
#if STS2_AT_LEAST_0_113_0
        Player player,
#else
        CharacterModel character,
#endif
        bool shouldSave,
        IReadOnlyList<ActModel> acts,
        IReadOnlyList<ModifierModel> modifiers,
        string seed,
        GameMode gameMode,
#if STS2_AT_LEAST_0_113_0
        bool isCustomGame,
#endif
        int ascensionLevel,
        DateTimeOffset? dailyTime)
    {
        try { Entry.PrecacheEnergyIcons(); } catch { }
        // ★ 2026-11-12 用户设置：只随机本局选中的角色。
        //   单人：只生成本地角色；多人：生成全部 profile（包含队友角色），
        //   避免队友卡池没生成导致异步错误。设置关闭时永远生成三个。
#if STS2_AT_LEAST_0_113_0
        var character = player.Character;
#endif
        var localProfile = RandomProfiles.ProfileOfCharacterTypeName(character.GetType().Name);
        var multiplayer = RunManager.Instance?.NetService?.Type is NetGameType.Host or NetGameType.Client;
        IReadOnlyList<string> profiles = RandomConfig.OnlySelectedCharacters
                                          && !multiplayer
                                          && localProfile is not null
            ? new[] { localProfile }
            : RandomProfiles.All;
        var total = profiles.Sum(RandomCardSlots.CountFor);

        AutoAnthony.IComponentGenerationProgress? progress = null;
        SimpleProgressOverlay? simpleProgress = null;
        try
        {
            // ★ 2026-11-12 用户实测：AA 的进度层挂在 GameTransitionRect 上，
            //   但生成阶段始终被 transition 的黑色 ColorRect 盖住，加载完才露出白字。
            //   这里再加一个**独立高 CanvasLayer** 的简单进度层，保证黑屏上也能看到文字。
            simpleProgress = new SimpleProgressOverlay(total);
            // ★ 2026-11-12 用户反馈：进度层黑屏、看不到「生成 x/x」。
            //   根因：进程在 NTransition 变黑**之前**就 Create 了 overlay，
            //   AA 会退回 root CanvasLayer，被黑屏 transition 盖住。
            //   先等 transition 可见（最多约 1 秒），再创建。
            for (var wait = 0; wait < 60; wait++)
            {
                var transition = NGame.Instance?.Transition;
                if (transition?.IsInsideTree() == true && transition.IsVisibleInTree()) break;
                await Task.Delay(16);
            }

            progress = AutoAnthony.ComponentGenerationProgressApi.Create(Math.Max(1, total));
            RandomProgressHub.Begin(progress);
            // 先写一行，避免首帧标签为空（只看到黑屏背景）。
            progress.Report(1);

            // ★ 2026-11-12 用户截图：生成阶段只有黑屏，加载完才出现「正在进入游戏」。
            //   说明 overlay 被 NTransition 后来添加的黑色 ColorRect 盖住了；
            //   每次 Report 后把 AA 的 host 重新提到 transition 最后一个子节点。
            static void RaiseProgressHost()
            {
                var transition = NGame.Instance?.Transition;
                var host = transition?.GetNodeOrNull<Godot.Control>("AutoAnthonyGenerationProgress");
                if (transition is not null && host is not null && host.GetParent() == transition)
                    transition.MoveChild(host, transition.GetChildCount() - 1);
            }

            RaiseProgressHost();

            // 生成放后台线程；主线程一边报进度一边泵事件（AA 同款）。
            var offset = 0;
            var task = Task.Run(() =>
            {
                RandomProgressHub.SuppressPumping = true;
                try
                {
                    foreach (var selectedProfile in profiles)
                    {
                        RandomProgressHub.StartProfile(selectedProfile);
                        RandomRunState.EnsureInstalled(selectedProfile, seed);
                        // 一个 profile 生成完，把已完成数量加到 offset；
                        // 否则多个 profile 的 local 进度会互相覆盖，永远到不了 total。
                        Interlocked.Add(ref offset, RandomCardSlots.CountFor(selectedProfile));
                    }
                }
                finally
                {
                    RandomProgressHub.SuppressPumping = false;
                }
            });

            // ★ 根因（对照 AA ChaosRunDefinitions.ActivateAsync / 小睦）：
            //   之前用 task.Wait(8) + DisplayServer.ProcessEvents() **阻塞**主线程；
            //   ProcessEvents 只处理系统事件，不推进 Godot 的渲染帧 → overlay 永远画不出来，
            //   生成完回到主循环才看到「正在进入游戏」。
            //   AA 的做法是 await NextProcessFrame()，让主循环真正跑一帧。
            while (!task.IsCompleted)
            {
                try
                {
                    var local = RandomProgressHub.ReadLocal();
                    var reported = Math.Clamp(
                        System.Threading.Volatile.Read(ref offset) + local, 1, Math.Max(1, total));
                    progress.Report(reported);
                    simpleProgress?.Report(reported);
                    RaiseProgressHost();
                }
                catch { }
                await NextProcessFrame();
            }

            await task;

            task.GetAwaiter().GetResult();
            // 生成完、进游戏前，让 AA 的进度层显示「进入游戏」而不是停在 255/255。
            progress.ShowEnteringRun();
            simpleProgress?.Entering();
            RaiseProgressHost();
            await NextProcessFrame();
        }
        catch (Exception exception)
        {
            MegaCrit.Sts2.Core.Logging.Log.Warn(
                $"[CuteSakikoModRandom] 开局前生成失败（仍继续开局）：{exception.Message}");
        }
        finally
        {
            RandomProgressHub.End();
            simpleProgress?.Dispose();
        }

        try
        {
            Interlocked.Increment(ref _callingOriginal);
#if STS2_AT_LEAST_0_113_0
            return await game.StartNewSingleplayerRun(player, shouldSave, acts, modifiers, seed,
                gameMode, isCustomGame, ascensionLevel, dailyTime);
#else
            return await game.StartNewSingleplayerRun(character, shouldSave, acts, modifiers, seed,
                gameMode, ascensionLevel, dailyTime);
#endif
        }
        finally
        {
            Interlocked.Decrement(ref _callingOriginal);
            progress?.Dispose();
        }
    }

    /// <summary>
    /// 真正让 Godot 主循环跑一帧（AA 的 NextProcessFrame 同款）。
    /// 不能用 DisplayServer.ProcessEvents：它只处理系统事件，不推进渲染。
    /// </summary>
    private static async Task NextProcessFrame()
    {
        if (Godot.Engine.GetMainLoop() is Godot.SceneTree tree)
            await tree.ToSignal(tree, Godot.SceneTree.SignalName.ProcessFrame);
        else
            await Task.Delay(16);
    }

}


