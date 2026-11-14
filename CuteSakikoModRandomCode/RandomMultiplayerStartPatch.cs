using System.Threading;
using System.Threading.Tasks;
using AutoAnthony;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 联机开局：在 **RunState 建立之前** 把随机卡定义生成并装好，并且生成期间让主循环真正出帧。
///
/// ★★ 2026-11-12 两个问题的来源（对照 AA 源码 / Wiki 定论）：
///
/// ① 定义装得太晚：AA wiki《外部角色接入》要求「新一局建立或读档时」InstallDefinitions。
///    以前联机只在 <c>RunStartedEvent</c>（RunManager.InitializeNewRun 内、牌组已建好）才装，
///    StartingDeck 前缀永远看到「定义还没装」→ 原版牌组 → 只能靠 RunStartedEvent 兜底换牌组。
///    ⇒ patch <c>NGame.StartNewMultiplayerRun</c>，在原方法（RunState.CreateForNewRun）之前装定义。
///    用 <c>[HarmonyBefore("autoanthony.v111")]</c> 排在 AA 的 SeedBeforeMultiplayerPatch 前面
///    （照小睦 MzmAnthonyology/Patches/StartMultiplayerRunPatch.cs 的写法）。
///
/// ② 进度层全黑：AA 的 <c>ChaosGenerationProgressOverlay</c> 只有被 <c>Report()</c> 时才会
///    <c>RenderingServer.ForceDraw()</c>；如果主线程同步阻塞着等生成，就没有任何 Report，
///    黑屏上只剩一个透明 backdrop（AA 源码 ChaosGenerationProgressOverlay.cs:105-128）。
///    ⇒ 照抄单人补丁（RandomEarlyProgressPatch）的写法：
///      生成放后台 Task，主线程 <c>await NextProcessFrame()</c> 出帧并 Report；
///      另外再挂一个最高层 CanvasLayer 的 SimpleProgressOverlay，保证黑屏上也看得见字。
///
/// Prefix 返回 false 并把 <c>__result</c> 换成我们自己的 Task——和 AA/小睦完全相同的接管方式；
/// 生成完再调用 <c>game.StartNewMultiplayerRun</c>（重入，本前缀的 _callingOriginal 守卫放行），
/// 让 AA/小睦的联机链路照常执行。
/// </summary>
[HarmonyPatch(typeof(NGame), nameof(NGame.StartNewMultiplayerRun))]
[HarmonyBefore("autoanthony.v111")]
internal static class RandomMultiplayerStartPatch
{
    private static int _callingOriginal;

    private static bool Prefix(
        NGame __instance,
#if STS2_AT_LEAST_0_113_0
        List<Player> players,
#endif
        StartRunLobby lobby,
        bool shouldSave,
        IReadOnlyList<ActModel> acts,
        IReadOnlyList<ModifierModel> modifiers,
        string seed,
        int ascensionLevel,
        DateTimeOffset? dailyTime,
        ref Task<RunState> __result)
    {
        // 生成完重新调用原方法时放行，否则会无限递归。
        if (Volatile.Read(ref _callingOriginal) > 0) return true;

        try
        {
            if (lobby?.Players is null) return true;

            var profiles = ProfilesFor(lobby);
            if (profiles.Count == 0) return true;

            __result = StartAfterGeneration(__instance,
#if STS2_AT_LEAST_0_113_0
                players,
#endif
                lobby, profiles, shouldSave, acts,
                modifiers, seed, ascensionLevel, dailyTime);
            return false;
        }
        catch (Exception exception)
        {
            // 接管失败就退回原版链路；RunStartedEvent 的兜底仍会尝试一次。
            Log.Warn($"[CuteSakikoModRandom] 联机开局生成接管失败（退回原链路）：{exception}");
            return true;
        }
    }

    private static async Task<RunState> StartAfterGeneration(
        NGame game,
#if STS2_AT_LEAST_0_113_0
        List<Player> players,
#endif
        StartRunLobby lobby,
        IReadOnlyList<string> profiles,
        bool shouldSave,
        IReadOnlyList<ActModel> acts,
        IReadOnlyList<ModifierModel> modifiers,
        string seed,
        int ascensionLevel,
        DateTimeOffset? dailyTime)
    {
        var total = Math.Max(1, profiles.Sum(RandomCardSlots.CountFor));

        IComponentGenerationProgress? progress = null;
        SimpleProgressOverlay? simpleProgress = null;
        try
        {
            simpleProgress = new SimpleProgressOverlay(total);

            // 先等 NTransition 变黑可见，再创建 AA 的 overlay（和单人补丁相同的原因：
            // 早于 transition 创建会退回 root CanvasLayer，被黑屏盖住）。
            for (var wait = 0; wait < 60; wait++)
            {
                var transition = NGame.Instance?.Transition;
                if (transition?.IsInsideTree() == true && transition.IsVisibleInTree()) break;
                await Task.Delay(16);
            }

            progress = ComponentGenerationProgressApi.Create(total);
            RandomProgressHub.Begin(progress);
            progress.Report(1);
            RaiseProgressHost();

            var offset = 0;
            var task = Task.Run(() =>
            {
                // 后台线程里不要 DisplayServer.ProcessEvents()；由主线程统一 Report/出帧。
                RandomProgressHub.SuppressPumping = true;
                try
                {
                    foreach (var profile in profiles)
                    {
                        RandomProgressHub.StartProfile(profile);
                        RandomRunState.EnsureInstalled(profile, seed);
                        Interlocked.Add(ref offset, RandomCardSlots.CountFor(profile));
                    }
                }
                finally
                {
                    RandomProgressHub.SuppressPumping = false;
                }
            });

            while (!task.IsCompleted)
            {
                try
                {
                    var local = RandomProgressHub.ReadLocal();
                    var reported = Math.Clamp(Volatile.Read(ref offset) + local, 1, total);
                    progress.Report(reported);
                    simpleProgress?.Report(reported);
                    RaiseProgressHost();
                }
                catch
                {
                    // 进度层出错不影响生成。
                }

                await NextProcessFrame();
            }

            await task;
            task.GetAwaiter().GetResult();

            ModelDbCache.Invalidate();
            progress.ShowEnteringRun();
            simpleProgress?.Entering();
            RaiseProgressHost();
            await NextProcessFrame();
        }
        catch (Exception exception)
        {
            // 生成失败也继续开局：RunStartedEvent 的兜底会再试一次，最坏退回原版卡池。
            Log.Warn($"[CuteSakikoModRandom] 联机开局前生成失败（仍继续开局）：{exception}");
        }
        finally
        {
            RandomProgressHub.End();
            // ★ simpleProgress 不在这里 Dispose：加载/预载阶段（原方法里还要跑 7 秒左右）
            //   也保留最高层进度层显示「正在进入游戏……」，否则黑屏 transition 上什么都没有。
            Log.Info($"[CuteSakikoModRandom] 多人开局前已生成并安装随机池："
                     + $"{string.Join(",", profiles)} seed={seed}");
        }

        try
        {
            Interlocked.Increment(ref _callingOriginal);
#if STS2_AT_LEAST_0_113_0
            return await game.StartNewMultiplayerRun(players, lobby, shouldSave, acts, modifiers, seed,
                ascensionLevel, dailyTime);
#else
            return await game.StartNewMultiplayerRun(lobby, shouldSave, acts, modifiers, seed,
                ascensionLevel, dailyTime);
#endif
        }
        finally
        {
            Interlocked.Decrement(ref _callingOriginal);
            // AA 的 overlay 和我们的 SimpleProgressOverlay 都保留到开局链结束（加载期间可见）。
            simpleProgress?.Dispose();
            progress?.Dispose();
        }
    }

    /// <summary>
    /// 本局需要生成的 profile：
    ///   · 本局没有我们三个角色 → 不生成（纯原版联机不该被拖慢十几秒）；
    ///   · 开「只随机选中角色」→ 只生成本局实际用到的；
    ///   · 关掉 → 三个都生成（与其他生成入口保持一致）。
    /// </summary>
    private static IReadOnlyList<string> ProfilesFor(StartRunLobby lobby)
    {
        var selected = new List<string>(3);
        foreach (var player in lobby.Players)
        {
            var profile = RandomProfiles.ProfileOfCharacterTypeName(player.character?.GetType().Name);
            if (profile is not null && !selected.Contains(profile)) selected.Add(profile);
        }

        if (selected.Count == 0) return [];
        return RandomConfig.OnlySelectedCharacters ? selected : RandomProfiles.All;
    }

    /// <summary>把 AA 的 overlay 重新提到 transition 的最后一个子节点，避免被后来添加的黑色 ColorRect 盖住。</summary>
    private static void RaiseProgressHost()
    {
        var transition = NGame.Instance?.Transition;
        var host = transition?.GetNodeOrNull<Godot.Control>("AutoAnthonyGenerationProgress");
        if (transition is not null && host is not null && host.GetParent() == transition)
            transition.MoveChild(host, transition.GetChildCount() - 1);
    }

    /// <summary>让 Godot 主循环真正跑一帧（不能用 DisplayServer.ProcessEvents，它不推进渲染）。</summary>
    private static async Task NextProcessFrame()
    {
        if (Godot.Engine.GetMainLoop() is Godot.SceneTree tree)
            await tree.ToSignal(tree, Godot.SceneTree.SignalName.ProcessFrame);
        else
            await Task.Delay(16);
    }
}
