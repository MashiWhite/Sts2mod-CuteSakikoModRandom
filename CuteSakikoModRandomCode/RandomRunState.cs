using AutoAnthony;
using ChaosCardGenerator;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Keywords;
using GameCardTag = MegaCrit.Sts2.Core.Entities.Cards.CardTag;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 一局游戏内三个角色的随机卡状态。
///
/// 职责边界（AutoAnthony 明确不管这些）：
///   · 决定「什么时候生成」——本模组在开局时生成一次；
///   · 保存完整定义——由本模组自己的 RunSavedData 槽位负责（见 <see cref="RandomRunData"/>）；
///   · 联机时由房主权威同步——客户端安装房主发来的同一份定义。
/// </summary>
internal static class RandomRunState
{
    /// <summary>三个角色里有没有任何池子需要生成（抢跑前先判一下，省得白跑）。</summary>
    internal static bool AnyProfileWanted()
    {
        foreach (var profileId in RandomProfiles.All)
            if (RandomCardSlots.CountFor(profileId) > 0) return true;
        return false;
    }

    private static readonly Dictionary<string, IReadOnlyList<ChaosCardDefinition>> Installed =
        new(StringComparer.Ordinal);

    private static readonly object Gate = new();

    /// <summary>每个 profile 当前安装用的种子（换局换种子时据此重建）。</summary>
    private static readonly Dictionary<string, string> InstalledSeed = new();

    /// <summary>正在做「图鉴用安装」的 profile（重入锁）。</summary>
    private static readonly HashSet<string> LibraryInstalling = new(StringComparer.Ordinal);

    /// <summary>该 profile 的随机卡是否已在本局安装（供 AA 的运行期宿主判断）。</summary>
    internal static bool IsRunActive(string profileId)
    {
        // ★ 2026-11-11 用户实测：放弃对局回主菜单后，图鉴还显示随机卡。
        //   原因是这里只看「有没有装过定义」，而从不清空。
        //   对齐参考实现（小睦回主菜单→图鉴恢复本体卡）：必须同时确认**对局确实在进行**。
        if (MegaCrit.Sts2.Core.Runs.RunManager.Instance is not { IsInProgress: true } manager) return false;
        lock (Gate)
        {
            if (!Installed.ContainsKey(profileId)) return false;
        }

        // ★ 2026-11-12 用户实测：退出祥子再开乐奈，报
        //   "RanaRandomCard000 used in incorrect place"。
        //   根因：三个 profile 都装了定义，IsRunActive 对三个都返回 true，
        //   于是小睦/其他 mod 跨角色查卡池时，Saki 局里也会拿到 Rana 随机卡 → canonical model 崩。
        //   只有**本局玩家实际使用的角色**才算 active，其余 profile 的卡池返回本体卡。
        var state = manager.DebugOnlyGetState();
        if (state is null) return false;
        return state.Players.Any(player =>
            string.Equals(CharacterProfileOf(player), profileId, StringComparison.Ordinal));
    }

    /// <summary>本局是否有任意角色在用随机卡。</summary>
    internal static bool AnyRunActive
    {
        get { lock (Gate) return Installed.Count > 0; }
    }

    /// <summary>某个玩家当前是否在跑该 profile 的随机卡。</summary>
    internal static bool IsPlayerRunning(Player player, string profileId)
    {
        lock (Gate)
        {
            if (!Installed.ContainsKey(profileId)) return false;
        }

        return CharacterProfileOf(player) is { } playerProfile
               && string.Equals(playerProfile, profileId, StringComparison.Ordinal);
    }

    /// <summary>该 profile 的定义是否已经安装好（初始牌随机要靠它判断时机）。</summary>
    /// <summary>三个角色是否都还没装（新局开始时为 true）。</summary>
    internal static bool IsAnyInstalled
    {
        get { lock (Gate) return Installed.Count > 0; }
    }

    internal static bool IsInstalled(string profileId)
    {
        lock (Gate) return Installed.ContainsKey(profileId);
    }

    /// <summary>把玩家角色映射到随机 profile；不是三个小祥角色就返回 null。</summary>
    internal static string? CharacterProfileOf(Player? player)
    {
        var character = player?.Character;
        if (character is null) return null;
        var name = character.GetType().Name;
        return name switch
        {
            "CuteSaki" => RandomProfiles.Saki,
            "CuteAnon" => RandomProfiles.Anon,
            "CuteRana" => RandomProfiles.Rana,
            _ => null
        };
    }

    /// <summary>
    /// 图鉴/局外兜底：没有定义时用固定种子装一套，让卡池视图能显示随机卡。
    /// ⚠️ 必须有**重入锁**：安装过程里 AA 会去查卡池，
    ///    而卡池的 AllCards 补丁又会调回本方法 → 没有锁就是无限递归（2026-11-11 实测游戏直接崩）。
    /// </summary>
    internal static void EnsureLibraryInstalled(string profileId)
    {
        lock (Gate)
        {
            if (Installed.ContainsKey(profileId)) return;
            if (LibraryInstalling.Contains(profileId)) return;
            LibraryInstalling.Add(profileId);
        }

        try
        {
            var defs = EnsureInstalled(profileId, "library");
            try
            {
                var empty = defs.Count(d => string.IsNullOrWhiteSpace(d.PortraitPath) || d.PortraitPath == "res://");
                MegaCrit.Sts2.Core.Logging.Log.Info(
                    $"[CuteSakikoModRandom] 图鉴定义 {profileId}：{defs.Count} 张，空卡图 {empty} 张");
            }
            catch { }
        }
        catch (Exception exception)
        {
            MegaCrit.Sts2.Core.Logging.Log.Warn(
                $"[CuteSakikoModRandom] 图鉴用定义安装失败（{profileId}）：{exception.Message}");
        }
        finally
        {
            lock (Gate) LibraryInstalling.Remove(profileId);
        }
    }


    /// <summary>为一局生成并安装某个 profile 的定义（幂等：已安装就跳过）。</summary>
    internal static IReadOnlyList<ChaosCardDefinition> EnsureInstalled(string profileId, string seed,
        bool reuseExisting = false)
    {
        lock (Gate)
        {
            // ★ 2026-11-12 用户实测「生成好久」：RandomEarlyProgressPatch 已经用开局种子生成过，
            //   RunStartedEvent 又拿 RunState.Rng.Seed 生成一次（种子字符串不同）→ 整池重建两次。
            //   RunStartedEvent 的兜底传 reuseExisting: true，已装就直接复用。
            if (Installed.TryGetValue(profileId, out var existing))
            {
                if (reuseExisting) return existing;
                if (InstalledSeed.TryGetValue(profileId, out var oldSeed)
                    && string.Equals(oldSeed, seed, StringComparison.Ordinal))
                    return existing;
            }

            if (Installed.ContainsKey(profileId))
            {
                // ★ 不要调用 ClearDefinitions：它会把**整个 profile 注册**也清掉，
                //   下一次 Build 就会抛 "No component generation profile is registered"，
                //   整局退回原版卡池（2026-11-10 实机日志实锤）。
                //   只丢掉缓存，让下面的 Build + InstallDefinitions 覆盖即可。
                Installed.Remove(profileId);
                InstalledSeed.Remove(profileId);
                MegaCrit.Sts2.Core.Logging.Log.Info($"[CuteSakikoModRandom] {profileId} 种子变化，重建卡池（新={seed}）。");
            }
        }


        var definitions = RandomDefinitionBuilder.Build(profileId, seed);
        ExternalComponentCharacterApi.InstallDefinitions(profileId, definitions);
        lock (Gate)
        {
            Installed[profileId] = definitions;
            InstalledSeed[profileId] = seed;
        }
        return definitions;
    }

    /// <summary>
    /// 实机验证（用户 2026-11-11 第 3 条）：名字含「琴奏」的随机卡，
    /// 在**真正的 CardModel** 上是否拿到了 Playpiano 关键词 / Strike tag。
    /// 只写 CustomKeywords 不算数 —— 必须看到 <c>Keywords.Contains(...)</c> 为真。
    /// </summary>
    internal static void VerifyKeywords(string profileId)
    {
        try
        {
            var pool = RandomShells.PoolForRegistration(profileId);
            var chaosCards = pool.AllCards.OfType<ChaosCardModel>().ToArray();
            var pianoKeyword = CuteSakikoMod.CuteSakikoModCode.Others.CutesakiKeywords.Playpiano
                .GetModCardKeyword();
            var pianoCards = chaosCards
                .Where(card => (card.Generated.Name?.Chinese ?? string.Empty)
                    .Contains("琴奏", StringComparison.Ordinal))
                .ToArray();
            MegaCrit.Sts2.Core.Logging.Log.Info(
                $"[关键词验证] {profileId} 池内随机卡 {chaosCards.Length} 张；名字含「琴奏」{pianoCards.Length} 张；"
                + $"实际 Keywords 含 Playpiano {pianoCards.Count(c => c.Keywords.Contains(pianoKeyword))} 张；"
                + $"Tags 含 Strike {pianoCards.Count(c => c.Tags.Contains(GameCardTag.Strike))} 张");
        }
        catch (Exception exception)
        {
            MegaCrit.Sts2.Core.Logging.Log.Warn(
                $"[关键词验证] {profileId} 失败：{exception.Message}");
        }
    }

    /// <summary>
    /// 本局结束/放弃时清理（对齐参考实现：小睦局外图鉴恢复本体卡）。
    /// ★ 不调用 ExternalComponentCharacterApi.ClearDefinitions：
    ///   它会把 profile 注册一起清掉，下一局 Build 直接抛
    ///   "No component generation profile is registered"（2026-11-10 实机实锤）。
    ///   只清我们自己的缓存即可 —— 卡池 getter 靠 IsRunActive 判断要不要替换。
    /// </summary>
    internal static void Clear()
    {
        lock (Gate)
        {
            if (Installed.Count == 0) return;
            Installed.Clear();
            InstalledSeed.Clear();
            LibraryInstalling.Clear();
        }

        MegaCrit.Sts2.Core.Logging.Log.Info("[CuteSakikoModRandom] 已清理本局随机状态，卡池恢复本体卡。");

        ModelDbCache.Invalidate();


    }
}


















