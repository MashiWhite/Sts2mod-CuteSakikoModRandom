using STS2RitsuLib;
using STS2RitsuLib.Utils.Persistence;
using STS2RitsuLib.Settings;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>数值控制模式。</summary>
public enum RandomValueMode
{
    /// <summary>按本体价值曲线取值（默认）：白卡普通、金卡高数值，与本体平衡一致。</summary>
    OriginalValue = 0,

    /// <summary>完全随机：不做稀有度缩放、不做上下限收敛，纯粹随机。</summary>
    FullyRandom = 1
}

/// <summary>
/// 本模组的设置数据模型。
///
/// 持久化由 RitsuLib 的 data store 负责（SaveScope.Global），
/// 读写都走 <c>RitsuLibFramework.GetDataStore(ModId).Get&lt;RandomConfigData&gt;("config")</c>。
///
/// ⚠️ 必须是 <c>class</c> + 无参构造（RitsuLib 的 <c>where TModel : class, new()</c> 约束）。
/// ⚠️ 属性必须有 public setter，序列化器要写。
/// </summary>
public class RandomConfigData
{
    /// <summary>随机卡图：给随机卡配本体真实卡面（关闭后用 AA 默认图）。</summary>
    public bool RandomPortrait { get; set; } = true;

    /// <summary>初始牌随机：开局把初始牌组换成随机卡。</summary>
    public bool RandomStartingDeck { get; set; } = true;

    /// <summary>禁止重复卡图：同一角色池内一张卡面只用一次（默认开）。</summary>
    public bool NoDuplicateArt { get; set; } = true;

    /// <summary>数值控制：原版价值曲线 / 完全随机。</summary>
    public RandomValueMode ValueMode { get; set; } = RandomValueMode.OriginalValue;

    /// <summary>复合效果开关：开启后不再生成「纯单效果」卡，一律带 mod 机制。</summary>
    public bool CompoundEffect { get; set; } = false;

    /// <summary>只随机本局选中的角色（默认开）。多人联机时包含所有玩家的角色。</summary>
    public bool OnlySelectedCharacters { get; set; } = true;
}

/// <summary>
/// 设置的读写入口 + 设置页注册。
///
/// 为什么要这一层：游戏逻辑（生成器、卡图、初始牌）需要随时读配置，
/// 而 RitsuLib 的 binding 只在设置页渲染时被调用。
/// 所以这里直接走 <c>GetDataStore().Get&lt;T&gt;(key)</c>，任何时刻都能读到最新持久化值。
/// </summary>
internal static class RandomConfig
{
    /// <summary>模组 ID，必须与 CuteSakikoModRandom.json 的 id 一致。</summary>
    internal const string ModId = "CuteSakikoModRandom";

    /// <summary>数据键（与设置页 binding 的 dataKey 一致，改一处即可）。</summary>
    private const string DataKey = "config";

    /// <summary>
    /// 当前配置（实时从 data store 读，永远是最新值）。
    ///
    /// ⚠️ **必须永不抛异常**（2026-11-10 实测踩坑）：
    ///   data store 里的 `config` 键是**设置页 binding 首次渲染时**才注册的，
    ///   而我们在**开局生成卡池时**就要读配置。
    ///   直接 `Get<RandomConfigData>("config")` 会抛
    ///   `KeyNotFoundException: Data key 'config' is not registered`
    ///   → 整局退回原版卡池 → 玩家看到「没有随机效果」。
    ///
    ///   ⇒ 读不到就退回默认值：功能按默认设置跑，而不是整局失效。
    /// </summary>
    private static readonly RandomConfigData Fallback = new();

    internal static RandomConfigData Data
    {
        get
        {
            try
            {
                return RitsuLibFramework.GetDataStore(ModId).Get<RandomConfigData>(DataKey);
            }
            catch (Exception)
            {
                // 键还没注册（设置页没打开过）→ 用默认值，绝不影响卡池生成。
                return Fallback;
            }
        }
    }

    internal static bool RandomPortrait => Data.RandomPortrait;
    internal static bool RandomStartingDeck => Data.RandomStartingDeck;
    internal static bool NoDuplicateArt => Data.NoDuplicateArt;
    internal static bool CompoundEffect => Data.CompoundEffect;
    internal static bool OnlySelectedCharacters => Data.OnlySelectedCharacters;
    internal static bool FullyRandomValues => Data.ValueMode == RandomValueMode.FullyRandom;

    /// <summary>
    /// 注册设置页。必须在模组 Init 里调用一次。
    ///
    /// ⚠️ 大坑（配置存档联机.txt §6.4）：子页不传 pageId 会整页覆盖，
    ///    这里只有一个页面，所以显式传 ModId 当 pageId。
    /// </summary>
    internal static void Register()
    {
        // ★★ 2026-11-10 用户实机「设置页全部 Setting failed to load」：
        //   绑定时会去 data store 取 "config"，但我们**从没注册过**这个数据条目
        //   → KeyNotFoundException: Data key 'config' is not registered → 每个条目都建不出来。
        //   本体 Entry.cs:74 的标准写法就是先 BeginModDataRegistration 再 store.Register。
        using (RitsuLibFramework.BeginModDataRegistration(ModId))
        {
            var store = RitsuLibFramework.GetDataStore(ModId);
            store.Register(DataKey, "config.json", SaveScope.Global, () => new RandomConfigData(), true);
        }
        // 每个字段一个 binding，setter 里不做额外动作：值由 data store 自己保存，
        // 我们的读取走 Data（实时读 store），所以不需要缓存/事件。
        var portraitBinding = ModSettingsBindings.Global<RandomConfigData, bool>(
            ModId, DataKey, model => model.RandomPortrait, (model, value) => model.RandomPortrait = value);

        var deckBinding = ModSettingsBindings.Global<RandomConfigData, bool>(
            ModId, DataKey, model => model.RandomStartingDeck, (model, value) => model.RandomStartingDeck = value);

        var noDuplicateBinding = ModSettingsBindings.Global<RandomConfigData, bool>(
            ModId, DataKey, model => model.NoDuplicateArt, (model, value) => model.NoDuplicateArt = value);

        var valueModeBinding = ModSettingsBindings.Global<RandomConfigData, RandomValueMode>(
            ModId, DataKey, model => model.ValueMode, (model, value) => model.ValueMode = value);

        var compoundBinding = ModSettingsBindings.Global<RandomConfigData, bool>(
            ModId, DataKey, model => model.CompoundEffect, (model, value) => model.CompoundEffect = value);

        var selectedOnlyBinding = ModSettingsBindings.Global<RandomConfigData, bool>(
            ModId, DataKey, model => model.OnlySelectedCharacters,
            (model, value) => model.OnlySelectedCharacters = value);

        RitsuLibFramework.RegisterModSettings(ModId, page => page
            .WithTitle(ModSettingsText.Literal("东尼算法：可爱小祥"))
            .WithDescription(ModSettingsText.Literal(
                "随机卡池的行为控制。改动会在下一次开局（或重新生成卡池）时生效。"))
            .AddSection("random_core", section => section
                .WithTitle(ModSettingsText.Literal("生成"))

                .AddToggle("random_portrait",
                    ModSettingsText.Literal("随机卡图"),
                    portraitBinding,
                    ModSettingsText.Literal(
                        "开启：随机卡从本角色真实卡池里挑语义最接近的卡面。\n"
                        + "关闭：使用东尼算法的默认卡面。"))

                .AddToggle("random_starting_deck",
                    ModSettingsText.Literal("初始牌随机"),
                    deckBinding,
                    ModSettingsText.Literal(
                        "开启：开局把初始牌组（10 张）换成随机生成的卡。\n"
                        + "做法与东尼算法一致：拦截角色的 StartingDeck。"))

                .AddToggle("no_duplicate_art",
                    ModSettingsText.Literal("禁止重复卡图"),
                    noDuplicateBinding,
                    ModSettingsText.Literal(
                        "开启（默认）：同一角色池内尽量每张卡用不同卡面，图用完后才轮转复用。\n"
                        + "关闭：允许卡面重复。"))

                .AddEnumChoice("value_mode",
                    ModSettingsText.Literal("数值控制"),
                    valueModeBinding,
                    mode => ModSettingsText.Literal(mode switch
                    {
                        RandomValueMode.OriginalValue => "符合原版价值",
                        RandomValueMode.FullyRandom => "完全随机",
                        _ => mode.ToString()
                    }),
                    ModSettingsText.Literal(
                        "符合原版价值：按本体卡池实测曲线给数值（白卡普通、金卡高数值）。\n"
                        + "完全随机：不做稀有度缩放与上下限收敛，数值可能很离谱。"))

                .AddToggle("only_selected_characters",
                    ModSettingsText.Literal("只随机选中角色"),
                    selectedOnlyBinding,
                    ModSettingsText.Literal(
                        "开启（默认）：只生成本局所选角色的随机卡池；单人只生成你选的角色，\n"
                        + "多人会包含队友选择的角色。\n"
                        + "关闭：三个角色全部生成（更慢）。"))

                .AddToggle("compound_effect",
                    ModSettingsText.Literal("复合效果"),
                    compoundBinding,
                    ModSettingsText.Literal(
                        "开启：不再生成「只造成伤害 / 只获得格挡 / 只抽牌」的纯单效果卡，\n"
                        + "一律带上本角色的 mod 机制（压力 / 音符和弦 / 芭菲猫咪）。\n"
                        + "关闭：允许纯单效果卡（默认）。"))
                // ★ 客户端锁定：本局由房主决定设置，客户端不可改（主机权威）
                .WithEntryEnabledWhen("random_portrait", () => !RandomGameplaySync.ShouldLockGameplaySettings)
                .WithEntryEnabledWhen("random_starting_deck", () => !RandomGameplaySync.ShouldLockGameplaySettings)
                .WithEntryEnabledWhen("value_mode", () => !RandomGameplaySync.ShouldLockGameplaySettings)
                .WithEntryEnabledWhen("compound_effect", () => !RandomGameplaySync.ShouldLockGameplaySettings)
                .WithEntryEnabledWhen("only_selected_characters", () => !RandomGameplaySync.ShouldLockGameplaySettings)
            ), ModId);
    }
}









