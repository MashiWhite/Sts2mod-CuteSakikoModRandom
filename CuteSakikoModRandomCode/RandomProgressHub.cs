using System.Collections.Generic;
using System.Threading;
using AutoAnthony;
using MegaCrit.Sts2.Core.Logging;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 生成进度的「汇总器」：把三个角色的本地进度映射成全局进度，转发给 AA 原生进度层。
///
/// 官方依据：
///   · AutoAnthony.wiki / 外部角色接入.md:108 —— 「生成界面……使用 ComponentGenerationProgressApi」
///   · AA 内部 ChaosRunDefinitions.cs:652-666 —— 生成放 Task.Run，主线程
///     `while (!task.Wait(8)) { progress.Report(n); DisplayServer.ProcessEvents(); }`
/// 只有主线程真正出帧，Label 才会被排版绘制（我们自己硬画的 Label 就是因为这一条才看不见）。
/// </summary>
internal static class RandomProgressHub
{
    private static IComponentGenerationProgress? _progress;
    private static int _offset;

    internal static bool Active => _progress is not null;

    /// <summary>
    /// 在「属性 getter 里当场安装」这种场景必须抑制 Pump：
    /// DisplayServer.ProcessEvents() 会重入游戏逻辑（实测把初始牌组换回本体卡），
    /// 只在 RunStartedEvent 那种安全时机出帧。
    /// </summary>
    internal static bool SuppressPumping { get; set; }

    internal static int TotalFor(string profileId) => RandomCardSlots.CountFor(profileId);

    internal static int OffsetBefore(string profileId)
    {
        var offset = 0;
        foreach (var id in RandomProfiles.All)
        {
            if (id == profileId) return offset;
            offset += RandomCardSlots.CountFor(id);
        }

        return offset;
    }

    internal static void Begin(IComponentGenerationProgress progress)
    {
        _progress = progress;
        _offset = 0;
    }

    internal static void StartProfile(string profileId)
    {
        _offset = OffsetBefore(profileId);
    }

    private static int _local;

    /// <summary>本地进度（0..N）→ 全局进度。</summary>
    internal static void ReportLocal(int local)
    {
        Volatile.Write(ref _local, local);
        if (SuppressPumping) return;     // 后台生成时由外层主线程统一 Report
        try { _progress?.Report(Math.Max(1, _offset + local)); }
        catch { }
    }

    internal static int ReadLocal() => Volatile.Read(ref _local);

    /// <summary>让主线程出一帧（必须在主线程调用）。</summary>
    internal static void Pump()
    {
        try { Godot.DisplayServer.ProcessEvents(); }
        catch { }
    }

    internal static void End()
    {
        try { _progress?.ShowEnteringRun(); }
        catch (Exception exception)
        {
            Log.Warn($"[CuteSakikoModRandom] 进度层收尾失败：{exception.Message}");
        }

        _progress = null;
    }
}


