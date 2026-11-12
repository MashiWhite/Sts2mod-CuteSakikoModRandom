using ChaosCardGenerator;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 卡牌壳的**纯数据表**（不依赖 sts2 / ModelDb，可以离线单元测试）。
///
/// 数据来源：2026-11-09 逐文件统计本体真实卡池。
/// 每行 = (稀有度, 类型, 目标, 费用, 张数)。
/// 本体加卡后必须同步这里 + RandomCardSlots 的卡槽类，两者数量必须相等。
///
/// 为什么不从真实卡池反射读：AutoAnthony 在 ModelDb.InitIds 的 Postfix 里就冻结了
/// 组件包注册表，我们的包必须在模组 Init() 阶段注册完，那时 ModelDb 还没好。
/// 详细原因见 RandomShells.cs 的类注释。
/// </summary>
internal static class RandomShellTable
{
    internal readonly record struct Row(
        GeneratedRarity Rarity,
        GeneratedCardType Type,
        TargetMode Target,
        int Cost);

    internal static IReadOnlyList<Row> For(string profileId) => profileId switch
    {
        RandomProfiles.Saki => SakiRows(),
        RandomProfiles.Anon => AnonRows(),
        RandomProfiles.Rana => RanaRows(),
        _ => throw new ArgumentOutOfRangeException(nameof(profileId), profileId, "Unknown random profile.")
    };

    private static List<Row> SakiRows()
    {
        var rows = new List<Row>();
        void Add(GeneratedRarity rarity, int count, GeneratedCardType type, TargetMode target, int cost)
        {
            for (var i = 0; i < count; i++) rows.Add(new Row(rarity, type, target, cost));
        }

        // Basic 4
        Add(GeneratedRarity.Basic, 2, GeneratedCardType.Attack, TargetMode.SingleEnemy, 1);
        Add(GeneratedRarity.Basic, 1, GeneratedCardType.Skill, TargetMode.Other, 1);
        Add(GeneratedRarity.Basic, 1, GeneratedCardType.Skill, TargetMode.Other, 0);

        // Common 28
        Add(GeneratedRarity.Common, 2, GeneratedCardType.Attack, TargetMode.Other, 1);
        Add(GeneratedRarity.Common, 9, GeneratedCardType.Attack, TargetMode.SingleEnemy, 1);
        Add(GeneratedRarity.Common, 16, GeneratedCardType.Skill, TargetMode.Other, 1);
        Add(GeneratedRarity.Common, 1, GeneratedCardType.Skill, TargetMode.SingleEnemy, 1);

        // Uncommon 35
        Add(GeneratedRarity.Uncommon, 3, GeneratedCardType.Attack, TargetMode.Other, 1);
        Add(GeneratedRarity.Uncommon, 9, GeneratedCardType.Attack, TargetMode.SingleEnemy, 1);
        Add(GeneratedRarity.Uncommon, 7, GeneratedCardType.Power, TargetMode.Other, 2);
        Add(GeneratedRarity.Uncommon, 1, GeneratedCardType.Power, TargetMode.SingleEnemy, 1);
        Add(GeneratedRarity.Uncommon, 12, GeneratedCardType.Skill, TargetMode.Other, 1);
        Add(GeneratedRarity.Uncommon, 2, GeneratedCardType.Skill, TargetMode.SingleEnemy, 1);
        Add(GeneratedRarity.Uncommon, 1, GeneratedCardType.Skill, TargetMode.Other, 0);

        // Rare 26
        Add(GeneratedRarity.Rare, 4, GeneratedCardType.Attack, TargetMode.SingleEnemy, 2);
        Add(GeneratedRarity.Rare, 1, GeneratedCardType.Attack, TargetMode.Other, 0);
        Add(GeneratedRarity.Rare, 7, GeneratedCardType.Power, TargetMode.Other, 2);
        Add(GeneratedRarity.Rare, 14, GeneratedCardType.Skill, TargetMode.Other, 1);

        // Ancient 3
        Add(GeneratedRarity.Ancient, 1, GeneratedCardType.Attack, TargetMode.SingleEnemy, 0);
        Add(GeneratedRarity.Ancient, 1, GeneratedCardType.Power, TargetMode.Other, 3);
        Add(GeneratedRarity.Ancient, 1, GeneratedCardType.Skill, TargetMode.Other, 0);

        return rows;
    }

    private static List<Row> AnonRows()
    {
        var rows = new List<Row>();
        void Add(GeneratedRarity rarity, int count, GeneratedCardType type, TargetMode target, int cost)
        {
            for (var i = 0; i < count; i++) rows.Add(new Row(rarity, type, target, cost));
        }

        // Basic 4
        Add(GeneratedRarity.Basic, 1, GeneratedCardType.Attack, TargetMode.SingleEnemy, 1);
        Add(GeneratedRarity.Basic, 3, GeneratedCardType.Skill, TargetMode.Other, 1);

        // Common 19
        Add(GeneratedRarity.Common, 4, GeneratedCardType.Attack, TargetMode.Other, 1);
        Add(GeneratedRarity.Common, 7, GeneratedCardType.Attack, TargetMode.SingleEnemy, 1);
        Add(GeneratedRarity.Common, 6, GeneratedCardType.Skill, TargetMode.Other, 1);
        Add(GeneratedRarity.Common, 2, GeneratedCardType.Skill, TargetMode.SingleEnemy, 1);

        // Uncommon 35
        Add(GeneratedRarity.Uncommon, 2, GeneratedCardType.Attack, TargetMode.Other, 1);
        Add(GeneratedRarity.Uncommon, 8, GeneratedCardType.Attack, TargetMode.SingleEnemy, 1);
        Add(GeneratedRarity.Uncommon, 8, GeneratedCardType.Power, TargetMode.Other, 2);
        Add(GeneratedRarity.Uncommon, 13, GeneratedCardType.Skill, TargetMode.Other, 1);
        Add(GeneratedRarity.Uncommon, 2, GeneratedCardType.Skill, TargetMode.SingleEnemy, 1);
        Add(GeneratedRarity.Uncommon, 2, GeneratedCardType.Skill, TargetMode.Other, 0);

        // Rare 21
        Add(GeneratedRarity.Rare, 3, GeneratedCardType.Attack, TargetMode.SingleEnemy, 2);
        Add(GeneratedRarity.Rare, 8, GeneratedCardType.Power, TargetMode.Other, 2);
        Add(GeneratedRarity.Rare, 10, GeneratedCardType.Skill, TargetMode.Other, 1);

        // Ancient 2
        Add(GeneratedRarity.Ancient, 1, GeneratedCardType.Power, TargetMode.Other, 2);
        Add(GeneratedRarity.Ancient, 1, GeneratedCardType.Skill, TargetMode.Other, 1);

        return rows;
    }

    private static List<Row> RanaRows()
    {
        var rows = new List<Row>();
        void Add(GeneratedRarity rarity, int count, GeneratedCardType type, TargetMode target, int cost)
        {
            for (var i = 0; i < count; i++) rows.Add(new Row(rarity, type, target, cost));
        }

        // Basic 4
        Add(GeneratedRarity.Basic, 2, GeneratedCardType.Attack, TargetMode.SingleEnemy, 1);
        Add(GeneratedRarity.Basic, 2, GeneratedCardType.Skill, TargetMode.Other, 1);

        // Common 20
        Add(GeneratedRarity.Common, 3, GeneratedCardType.Attack, TargetMode.Other, 1);
        Add(GeneratedRarity.Common, 6, GeneratedCardType.Attack, TargetMode.SingleEnemy, 1);
        Add(GeneratedRarity.Common, 11, GeneratedCardType.Skill, TargetMode.Other, 1);

        // Uncommon 32
        Add(GeneratedRarity.Uncommon, 4, GeneratedCardType.Attack, TargetMode.Other, 1);
        Add(GeneratedRarity.Uncommon, 4, GeneratedCardType.Attack, TargetMode.SingleEnemy, 1);
        Add(GeneratedRarity.Uncommon, 7, GeneratedCardType.Power, TargetMode.Other, 2);
        Add(GeneratedRarity.Uncommon, 14, GeneratedCardType.Skill, TargetMode.Other, 1);
        Add(GeneratedRarity.Uncommon, 2, GeneratedCardType.Skill, TargetMode.SingleEnemy, 1);
        Add(GeneratedRarity.Uncommon, 1, GeneratedCardType.Skill, TargetMode.Other, 0);

        // Rare 20
        Add(GeneratedRarity.Rare, 4, GeneratedCardType.Attack, TargetMode.Other, 2);
        Add(GeneratedRarity.Rare, 3, GeneratedCardType.Attack, TargetMode.SingleEnemy, 2);
        Add(GeneratedRarity.Rare, 1, GeneratedCardType.Attack, TargetMode.SingleEnemy, 1);
        Add(GeneratedRarity.Rare, 6, GeneratedCardType.Power, TargetMode.Other, 2);
        Add(GeneratedRarity.Rare, 5, GeneratedCardType.Skill, TargetMode.Other, 1);
        Add(GeneratedRarity.Rare, 1, GeneratedCardType.Skill, TargetMode.Other, 0);

        // Ancient 2
        Add(GeneratedRarity.Ancient, 1, GeneratedCardType.Power, TargetMode.Other, 3);
        Add(GeneratedRarity.Ancient, 1, GeneratedCardType.Skill, TargetMode.Other, 1);

        return rows;
    }
}
