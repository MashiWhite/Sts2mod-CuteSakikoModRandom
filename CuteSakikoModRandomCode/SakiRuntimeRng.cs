using AutoAnthony;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Random;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 随机卡「运行时效果」的确定性随机源。
///
/// ★★ 2026-11-12 联机实测：以前这些效果（随机遗忘、随机演奏、随机敌人、随机 debuff 等）
///   直接用 <c>Random.Shared</c>（进程级随机）。多人里同一张牌会在两端各掷一次不同的随机数，
///   下一次 checksum 就直接 StateDivergence。
///
/// ⇒ 全部改用游戏自带的**同步 RNG 计数器**（RunState.Rng，随存档/联机一起同步、也进 checksum）：
///   · 选牌 / 选效果 / 选和弦 / 选 debuff → <c>CombatCardSelection</c>
///   · 随机敌人目标                    → <c>CombatTargets</c>
///   两端执行同一串动作、消耗同一顺序的随机数 → 结果一致。
///
/// 注意：只有真正在「对局内」才有 RunState；拿不到时退回 Rng.Chaotic（单机调试用，不进联机）。
/// </summary>
internal static class SakiRuntimeRng
{
    internal static Rng CardSelection(ComponentRuntimeContext context) =>
        context.Card?.Owner?.RunState?.Rng?.CombatCardSelection ?? Rng.Chaotic;

    internal static Rng Targets(ComponentRuntimeContext context) =>
        context.Card?.Owner?.RunState?.Rng?.CombatTargets ?? Rng.Chaotic;

    /// <summary>洗牌后取前 count 张（等价于原来的 OrderBy(random).Take(count)，但走同步 RNG）。</summary>
    internal static List<T> PickRandom<T>(IReadOnlyList<T> source, int count, Rng rng)
    {
        var pool = source.ToList();
        // 0.113 删除了 Rng.Shuffle<T>(IList<T>)（0.111 的 Rng.cs:209），
        // 改用 MegaCrit.Sts2.Core.Extensions.ListExtensions.UnstableShuffle。
        // 注意：它先取一次 rng.NextUnsignedLong() 造子 Rng，再 Fisher-Yates；
        // 与 0.111 的「每次交换直接取 NextInt」消耗的随机数序列不同，但两端同版本仍一致。
        pool.UnstableShuffle(rng);
        if (count < pool.Count)
            pool.RemoveRange(count, pool.Count - count);
        return pool;
    }
}
