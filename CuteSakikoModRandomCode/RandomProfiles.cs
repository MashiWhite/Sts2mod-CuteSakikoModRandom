using ChaosCardGenerator;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 三个可玩角色的组件 Profile 常量。ProfileId 是外部角色在 AutoAnthony 里的**唯一身份**，
/// 一经发布就不能改（会进存档、进联机 checksum）。
/// </summary>
public static class RandomProfiles
{
    public const string Saki = "cutesakikomod:saki";
    public const string Anon = "cutesakikomod:anon";
    public const string Rana = "cutesakikomod:rana";

    /// <summary>
    /// 平衡原型：复用哪个原版角色的数值/合法性习惯。真身份仍是 ProfileId。
    /// 祥子有压力资源（越压越疼），贴近储君的蓝星节奏；爱音走音符/和弦，贴近猎手的弃牌流；
    /// 乐奈走芭菲（资源囤积后爆发），也贴近储君。
    /// </summary>
    public static GeneratedCharacter ArchetypeFor(string profileId) => profileId switch
    {
        Saki => GeneratedCharacter.Regent,
        Anon => GeneratedCharacter.Silent,
        Rana => GeneratedCharacter.Regent,
        _ => throw new ArgumentOutOfRangeException(nameof(profileId), profileId, "Unknown random profile.")
    };

    /// <summary>
    /// 富文本能量图标前缀。先用原版角色凑合（描述里的 {energyPrefix} 会渲染成对应图标），
    /// 要换成自家图标需要额外提供 res://images/packed/sprite_fonts/{prefix}_energy_icon.png。
    /// </summary>
    public static string EnergyIconPrefixFor(string profileId) => profileId switch
    {
        Saki => "regent",
        Anon => "silent",
        Rana => "regent",
        _ => "regent"
    };

    /// <summary>角色显示名（写日志 / 调试用）。</summary>
    public static string DisplayNameFor(string profileId) => profileId switch
    {
        Saki => "祥子",
        Anon => "爱音",
        Rana => "乐奈",
        _ => profileId
    };

    public static readonly string[] All = [Saki, Anon, Rana];
    /// <summary>
    /// 按角色**类型名**映射到 profile（供 Harmony 补丁用，那里只能拿到对象实例）。
    /// 三个小祥角色之外一律返回 null。
    /// </summary>
    internal static string? ProfileOfCharacterTypeName(string? typeName) => typeName switch
    {
        "CuteSaki" => Saki,
        "CuteAnon" => Anon,
        "CuteRana" => Rana,
        _ => null
    };
}
