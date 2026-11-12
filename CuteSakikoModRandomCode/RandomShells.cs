using ChaosCardGenerator;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 卡牌壳：把 <see cref="RandomShellTable"/> 的纯数据转换成 AA 需要的
/// <see cref="IroncladCardRecipe"/>，并附带运行时需要的卡池取值。
///
/// ★ 时序要点（踩过一次坑，改动前务必读完）：
///   AutoAnthony 在 <c>ModelDb.InitIds</c> 的 **Postfix 里**就跑 library preview
///   （实测日志：生成 6 套预览池，合计约 4.5 秒），跑完立刻把组件包注册表**冻结**。
///   我们的组件包如果也挂在 InitIds 的 Postfix 上，永远晚一步，必然抛
///   「Component packages must be registered before the first generation profile resolves」。
///
///   ⇒ 组件包必须在**模组 Init() 阶段**注册完，那时 ModelDb 还没好，
///     所以形状表只能硬编码（见 RandomShellTable），不能读真实卡池。
///     只有 <see cref="PoolForRegistration"/> 会在运行时被调用（那时 ModelDb 已就绪）。
/// </summary>
internal static class RandomShells
{
    /// <summary>一个卡牌壳。</summary>
    internal readonly record struct Shell(
        GeneratedRarity Rarity,
        GeneratedCardType Type,
        TargetMode Target,
        int Cost,
        string ChineseTitle,
        string EnglishTitle);

    /// <summary>生成某角色的完整壳列表（数量与卡槽数完全一致）。</summary>
    internal static IReadOnlyList<Shell> For(string profileId)
    {
        var rows = RandomShellTable.For(profileId);
        var expected = RandomCardSlots.CountFor(profileId);
        if (rows.Count != expected)
            throw new InvalidOperationException(
                $"{profileId} 的壳数量（{rows.Count}）与卡槽数（{expected}）不一致；"
                + "请同步 RandomShellTable 的形状表和 RandomCardSlots 的卡槽类。");

        var shells = new List<Shell>(rows.Count);
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            shells.Add(new Shell(row.Rarity, row.Type, row.Target, row.Cost,
                $"随机卡{index + 1:D3}", $"Random Card {index + 1:D3}"));
        }

        return shells;
    }

    /// <summary>给 AutoAnthony 的运行期宿主用：返回该角色当前的卡池实例。</summary>
    internal static MegaCrit.Sts2.Core.Models.CardPoolModel PoolForRegistration(string profileId) =>
        profileId switch
        {
            RandomProfiles.Saki => MegaCrit.Sts2.Core.Models.ModelDb.CardPool<
                CuteSakikoMod.CuteSakikoModCode.Pools.Saki.CuteSakiCardPool>(),
            RandomProfiles.Anon => MegaCrit.Sts2.Core.Models.ModelDb.CardPool<
                CuteSakikoMod.CuteSakikoModCode.Pools.Anon.CuteAnonCardPool>(),
            RandomProfiles.Rana => MegaCrit.Sts2.Core.Models.ModelDb.CardPool<
                CuteSakikoMod.CuteSakikoModCode.Pools.Rana.CuteRanaCardPool>(),
            _ => throw new ArgumentOutOfRangeException(nameof(profileId), profileId, "Unknown random profile.")
        };
}
