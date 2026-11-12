using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 初始牌随机（T-002）—— 前缀逻辑 + **手动安装**补丁。
///
/// ★ 做法照抄 AutoAnthony（`RunAndPoolPatches.cs:3447` 起）：
///   它给每个角色挂 `[HarmonyPatch(typeof(Ironclad), nameof(Ironclad.StartingDeck), MethodType.Getter)]`，
///   拦截角色类的 `StartingDeck` getter 来换掉初始牌组。
///
/// ⚠️ 2026-11-09 踩坑：直接照抄属性标注**会崩整个模组**：
///   `ArgumentException: Undefined target method for patch method ... SakiStartingDeckPatch::Prefix`
///   原因：三个小祥角色的 `StartingDeck` 声明在 RitsuLib 的**泛型基类**
///   `ModCharacterTemplate&lt;TCardPool,TRelicPool,TPotionPool&gt;` 上（sealed override），
///   属性标注按具体角色类型解析时找不到。
///
///   而且更要命的是 `Harmony.PatchAll()` 在 `Entry.Init` 的 try 外面，
///   一个补丁失败 → 整个模组初始化抛异常 → 模组完全不生效。
///
/// ⇒ 现在的做法：
///   1. **不用属性标注**，改在 Init 里手动 `harmony.Patch(getter, prefix: ...)`
///   2. 逐个角色 try/catch，某个失败只记 Warn，不影响另外两个
///   3. 找不到 getter 就跳过（初始牌随机降级为「不生效」，而不是让模组崩掉）
/// </summary>
internal static class RandomStartingDeckPatch
{
    /// <summary>本体初始牌组一般是 10 张（5 打击 + 4 防御 + 1 特殊）。</summary>
    private const int DeckSize = 10;

    /// <summary>
    /// 手动安装三个补丁。**永不抛异常**——补丁装不上只是这个功能不生效，
    /// 绝不能因此让整个模组起不来。
    /// </summary>
    internal static void Install(Harmony harmony)
    {
        InstallDispatch(harmony);
    }

    /// <summary>
    /// ★★ 只装**一个**补丁，按实例类型分派（2026-11-10 实机发现的致命 bug）。
    ///
    /// 为什么不能装三个：三个角色的 `StartingDeck` 都来自**同一个泛型基类**
    /// （RitsuLib 的 `ModCharacterTemplate&lt;T…&gt;`），对三个封闭泛型各打一次，
    /// Harmony 视作**同一个泛型方法**，后脸皮盖前面 —— 实际只有最后一次（乐奈）生效。
    /// 症状：玩祥子时日志写「乐奈 的随机卡定义尚未安装」，初始牌永远是原版。
    ///
    /// ⇒ 正确做法：只打一次，在 prefix 里按 `__instance` 的具体类型分派。
    /// </summary>
    private static void InstallDispatch(Harmony harmony)
    {
        try
        {
            var sakiType = typeof(CuteSakikoMod.CuteSakikoModCode.Character.Mujica.CuteSaki);
            Type? declaringType = null;
            for (var type = sakiType; type is not null; type = type.BaseType)
            {
                if (AccessTools.DeclaredProperty(type, "StartingDeck") is null) continue;
                declaringType = type;
                break;
            }

            if (declaringType is null)
            {
                Log.Warn("[CuteSakikoModRandom] 继承链上找不到 StartingDeck，初始牌随机整体不生效。");
                return;
            }

            var getter = AccessTools.DeclaredPropertyGetter(declaringType, "StartingDeck");
            if (getter is null)
            {
                Log.Warn("[CuteSakikoModRandom] 找不到 StartingDeck 的 getter，初始牌随机整体不生效。");
                return;
            }

            harmony.Patch(getter, prefix: new HarmonyMethod(
                typeof(RandomStartingDeckPatch), nameof(PrefixDispatch)));
            Log.Info($"[CuteSakikoModRandom] 初始牌随机补丁已挂到 {declaringType.Name}.StartingDeck（按实例分派）。");
        }
        catch (Exception exception)
        {
            Log.Warn("[CuteSakikoModRandom] 挂初始牌随机补丁失败（不影响其它功能）：" + exception.Message);
        }
    }

    /// <summary>按实例的具体角色类型分派到对应 Profile。</summary>
    private static bool PrefixDispatch(object __instance, ref IEnumerable<CardModel> __result)
    {
        var name = __instance?.GetType().Name ?? string.Empty;
        return name switch
        {
            "CuteSaki" => Apply(RandomProfiles.Saki, ref __result),
            "CuteAnon" => Apply(RandomProfiles.Anon, ref __result),
            "CuteRana" => Apply(RandomProfiles.Rana, ref __result),
            _ => true
        };
    }

    private static void InstallOne(Harmony harmony, Type characterType, string profileId, string prefixName)
    {
        try
        {
            // ★ 2026-11-10 实测：Harmony 不接受「打实现方法」，要求打**声明该 virtual 的基类**。
            //   日志原话：「You can only patch implemented methods/constructors.
            //   Patch the declared method virtual ...::get_StartingDeck() instead.」
            //   ⇒ 沿继承链找到「第一次声明 StartingDeck 的那个类型」，打它。
            // 从角色类往上找「第一个自己声明了 StartingDeck 的类型」。
            // 对三个小祥角色来说，那是各自的**封闭泛型基类**
            // ModCharacterTemplate<CuteSakiCardPool, ...>（sealed override，RitsuLib 第 394 行）。
            Type? declaringType = null;
            for (var type = characterType; type is not null; type = type.BaseType)
            {
                if (AccessTools.DeclaredProperty(type, "StartingDeck") is null) continue;
                declaringType = type;
                break;
            }

            if (declaringType is null)
            {
                Log.Warn($"[CuteSakikoModRandom] {characterType.Name} 的继承链上没有声明 StartingDeck，"
                         + $"初始牌随机对「{RandomProfiles.DisplayNameFor(profileId)}」不生效。");
                return;
            }

            var getter = AccessTools.DeclaredPropertyGetter(declaringType, "StartingDeck");
            if (getter is null)
            {
                Log.Warn($"[CuteSakikoModRandom] 找不到 {characterType.Name}.StartingDeck 的 getter，"
                         + "初始牌随机对「{RandomProfiles.DisplayNameFor(profileId)}」不生效。");
                return;
            }

            var prefix = new HarmonyMethod(typeof(RandomStartingDeckPatch), prefixName);
            harmony.Patch(getter, prefix: prefix);
            Log.Info($"[CuteSakikoModRandom] 初始牌随机补丁已挂到 {characterType.Name}.StartingDeck。");
        }
        catch (Exception exception)
        {
            Log.Warn($"[CuteSakikoModRandom] 给 {characterType.Name} 挂初始牌随机补丁失败（不影响其它功能）："
                     + exception.Message);
        }
    }

    // ── 三个前缀（签名必须与原始 getter 匹配：无参、返回 IEnumerable<CardModel>）──

    private static bool PrefixSaki(ref IEnumerable<CardModel> __result) =>
        Apply(RandomProfiles.Saki, ref __result);

    private static bool PrefixAnon(ref IEnumerable<CardModel> __result) =>
        Apply(RandomProfiles.Anon, ref __result);

    private static bool PrefixRana(ref IEnumerable<CardModel> __result) =>
        Apply(RandomProfiles.Rana, ref __result);

    /// <summary>
    /// 把 <c>__result</c> 换成随机卡并返回 false 跳过原实现。
    /// 任何异常都返回 true（保持原版牌组）—— 初始牌组出错会直接毁掉开局。
    /// </summary>
    private static bool Apply(string profileId, ref IEnumerable<CardModel> __result)
    {
        try
        {
            if (!RandomConfig.RandomStartingDeck) return true;

            // ★ 2026-11-11 用户反馈「角色选择界面点角色会卡一下」：
            //   这里以前会**当场生成** 96 张卡（0.5~3 秒），而角色选择界面会读 StartingDeck 做预览
            //   → 点一下卡几秒。现在这里只「已装好才换牌」，没装就直接返回原版牌组（瞬间返回）；
            //   真正的生成交给 RandomEarlyProgressPatch（开局前缀）和 RunStartedEvent 的兜底替换。
            if (!RandomRunState.IsInstalled(profileId))
            {
                Log.Info($"[CuteSakikoModRandom] 初始牌随机：{RandomProfiles.DisplayNameFor(profileId)} "
                         + "的定义还没装（角色选择阶段不生成），本局开局后由兜底替换。");
                return true;
            }

            if (!RandomRunState.IsInstalled(profileId))
            {
                Log.Warn($"[CuteSakikoModRandom] 初始牌随机：{RandomProfiles.DisplayNameFor(profileId)} "
                         + "的随机卡定义安装失败，本次沿用原版初始牌组。");
                return true;
            }

            var slots = RandomCardSlots.For(profileId);
            if (slots.Count == 0) return true;

            var deck = new List<CardModel>(DeckSize);
            for (var i = 0; i < DeckSize; i++)
            {
                var slotType = slots[i % slots.Count];
                var model = ModelDb.GetById<CardModel>(ModelDb.GetId(slotType));
                if (model is not null) deck.Add(model);
            }

            if (deck.Count == 0) return true;

            __result = deck;
            Log.Info($"[CuteSakikoModRandom] 初始牌随机：{RandomProfiles.DisplayNameFor(profileId)} "
                     + $"的初始牌组已换成 {deck.Count} 张随机卡。");
            return false;
        }
        catch (Exception exception)
        {
            Log.Error($"[CuteSakikoModRandom] 初始牌随机失败，保持原版牌组：{exception}");
            return true;
        }
    }
}












