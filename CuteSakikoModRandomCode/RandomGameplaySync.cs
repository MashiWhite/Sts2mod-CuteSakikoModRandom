using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib;
using STS2RitsuLib.Lifecycle;
using STS2RitsuLib.RunData;
using STS2RitsuLib.Networking.Sidecar;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 本模组设置的**主机权威**快照（存进本局 RunSavedData，随大厅同步）。
///
/// 为什么必须有这个（用户 2026-11-09 明确要求「有关游戏性的 mod 需要主机权威」）：
///   随机卡池是**会改变游戏内容**的设置。如果每个客户端各自用自己的设置，
///   同一局里不同玩家看到的卡池就不一样，联机直接不同步（掉线/卡死）。
///   ⇒ 规则：只有房主能用本地设置决定卡池；客户端一律跟房主的快照。
///
/// 抄的是本体 GameplayConfigSync 的骨架，但**简化**了：
///   本体走 RitsuLibSidecarConfigSyncService 做运行中实时同步；
///   本模组只需要「开局时一致」，所以用 RunSavedData + SyncLobbyOnChange 就够了。
/// </summary>
public class RandomConfigSnapshot
{
    public bool RandomPortrait { get; set; } = true;
    public bool RandomStartingDeck { get; set; } = true;
    public RandomValueMode ValueMode { get; set; } = RandomValueMode.OriginalValue;
    public bool CompoundEffect { get; set; }

    public void CopyFrom(RandomConfigSnapshot other)
    {
        RandomPortrait = other.RandomPortrait;
        RandomStartingDeck = other.RandomStartingDeck;
        ValueMode = other.ValueMode;
        CompoundEffect = other.CompoundEffect;
    }

    public void CopyFrom(RandomConfigData other)
    {
        RandomPortrait = other.RandomPortrait;
        RandomStartingDeck = other.RandomStartingDeck;
        ValueMode = other.ValueMode;
        CompoundEffect = other.CompoundEffect;
    }
}

internal static class RandomGameplaySync
{
    private const string SlotKey = "RandomHostConfig";

    /// <summary>本局快照槽位（房主写，客户端读）。</summary>
    internal static RunSavedData<RandomConfigSnapshot>? Slot { get; private set; }

    /// <summary>自己是不是权威（单人 / 房主 / 本地）。</summary>
    internal static bool IsHostAuthority { get; private set; } = true;

    /// <summary>客户端在本局中不允许改游戏性设置。</summary>
    internal static bool ShouldLockGameplaySettings
    {
        get
        {
            var rm = RunManager.Instance;
            var ns = rm?.NetService;
            if (ns is null) return false;
            if (ns.Type != NetGameType.Client) return false;
            return rm!.DebugOnlyGetState() is not null;
        }
    }

    internal static void Init()
    {
        try
        {
            Slot = RunSavedDataStore.For(RandomConfig.ModId).Register<RandomConfigSnapshot>(
                SlotKey,
                options: new RunSavedDataOptions
                {
                    WritePolicy = RunSavedDataWritePolicy.WhenSet,
                    // 让快照随大厅同步给客户端，这是「主机权威」的关键。
                    SyncLobbyOnChange = true
                });
        }
        catch (Exception exception)
        {
            Log.Warn($"[CuteSakikoModRandom] 注册配置快照槽位失败，联机将退回各客户端本地设置：{exception.Message}");
        }

        UpdateAuthority(RunManager.Instance?.NetService);

        try
        {
            RitsuLibFramework.SubscribeLifecycle<RunStartedEvent>(OnRunStarted);
        }
        catch (Exception exception)
        {
            Log.Warn($"[CuteSakikoModRandom] 订阅 RunStartedEvent 失败：{exception.Message}");
        }

        RitsuLibSidecarEvents.OnSessionBound(evt => UpdateAuthority(evt.NetService));
        RitsuLibSidecarEvents.OnSessionUnbound(_ => UpdateAuthority(null));
    }

    private static void UpdateAuthority(INetGameService? netService) =>
        IsHostAuthority = netService is null || netService.Type != NetGameType.Client;

    private static void OnRunStarted(RunStartedEvent evt)
    {
        var state = evt.RunState;
        if (state is null || Slot is null) return;

        try
        {
            if (IsHostAuthority)
            {
                // 房主：把自己生效中的设置写进本局快照。
                var cfg = RandomConfig.Data;
                var snapshot = new RandomConfigSnapshot();
                snapshot.CopyFrom(cfg);
                Slot.Modify(state, data => data.CopyFrom(snapshot));
                Log.Info($"[CuteSakikoModRandom] 已把房主设置写入本局快照"
                         + $"（卡图={snapshot.RandomPortrait} 初始牌={snapshot.RandomStartingDeck}"
                         + $" 数值={snapshot.ValueMode} 复合={snapshot.CompoundEffect}）。");
            }
            else
            {
                // 客户端：直接用房主快照覆盖本地生效值，保证两边卡池一致。
                var host = Slot.Get(state);
                if (host is null)
                {
                    Log.Warn("[CuteSakikoModRandom] 本局快照里没有房主设置，客户端沿用本地设置（可能不同步）。");
                    return;
                }

                var local = RandomConfig.Data;
                local.RandomPortrait = host.RandomPortrait;
                local.RandomStartingDeck = host.RandomStartingDeck;
                local.ValueMode = host.ValueMode;
                local.CompoundEffect = host.CompoundEffect;
                Log.Info("[CuteSakikoModRandom] 已应用房主的随机设置（客户端以房主为准）。");
            }
        }
        catch (Exception exception)
        {
            Log.Warn($"[CuteSakikoModRandom] 同步房主设置失败：{exception.Message}");
        }
    }
}




