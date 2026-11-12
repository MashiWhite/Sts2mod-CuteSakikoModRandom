using Godot;
using Godot.Bridge;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 模组入口。
///
/// 全部注册都在 Init() 里完成——**不要**挪到 ModelDb.InitIds 的 Postfix，
/// 原因见 <see cref="RandomIntegration"/> 的类注释（AutoAnthony 会在那个 Postfix
/// 里抢先冻结组件包注册表）。
///
/// ⚠️ 2026-11-09 踩过的坑：词片 PartId 与 AutoAnthony 原版词片重名（如 Parry），
///    会让 RegisterAll 抛异常 → 整个模组静默失效 → 表现为「卡池全是本体默认卡」。
///    现在每个角色独立 try/catch + 明确日志，坏一个不会连累另一个。
/// </summary>
[ModInitializer(nameof(Init))]
public class Entry
{
    private static readonly Harmony Harmony = new("cutesakikomod.random");

    /// <summary>初始化是否完整成功（供其他类判断要不要启用补丁）。</summary>
    public static bool Ready { get; private set; }

    public static void Init()
    {
        // ⚠️ PatchAll 必须包 try/catch：任一个补丁目标解析失败会抛
        //   ArgumentException，之前就因此让整个模组初始化失败（2026-11-09 实测）。
        try
        {
            Harmony.PatchAll();
        }
        catch (Exception exception)
        {
            Log.Error($"[CuteSakikoModRandom] Harmony 补丁安装失败（其余功能继续）：{exception}");
        }

        // 初始牌随机补丁：手动安装，逐个角色 try/catch（见 RandomStartingDeckPatch 的注释）
        try { RandomStartingDeckPatch.Install(Harmony); }
        catch (Exception exception) { Log.Error($"[CuteSakikoModRandom] 初始牌随机补丁安装失败：{exception}"); }
        // 设置页与主机权威快照（必须在生成之前注册）
        try { RandomConfig.Register(); }
        catch (Exception exception) { Log.Error($"[CuteSakikoModRandom] 设置页注册失败：{exception}"); }
        try { RandomGameplaySync.Init(); }
        catch (Exception exception) { Log.Error($"[CuteSakikoModRandom] 配置同步初始化失败：{exception}"); }
        ScriptManagerBridge.LookupScriptsInAssembly(typeof(Entry).Assembly);

        var ok = true;
        foreach (var profileId in RandomProfiles.All)
        {
            try
            {
                RandomIntegration.RegisterProfile(profileId);
                Log.Info($"[CuteSakikoModRandom] {RandomProfiles.DisplayNameFor(profileId)} 注册完成。");
            }
            catch (Exception exception)
            {
                ok = false;
                // 单个角色失败不影响另外两个；但必须留下能一眼看懂的日志。
                Log.Error($"[CuteSakikoModRandom] {RandomProfiles.DisplayNameFor(profileId)} "
                          + $"注册失败（该角色保持原版卡池）：{exception}");
            }
        }

        try
        {
            RandomRunInstaller.Init();
        }
        catch (Exception exception)
        {
            ok = false;
            Log.Error($"[CuteSakikoModRandom] 开局安装钩子注册失败：{exception}");
        }

        Ready = ok;
        if (ok)
            Log.Info("[CuteSakikoModRandom] 初始化完成：三个角色的 profile / 词片 / 路由 / 组件包 / 卡槽 全部注册。");
        else
            Log.Error("[CuteSakikoModRandom] 初始化未完整成功，失败的角色会退回原版卡池。");
    }

    /// <summary>
    /// 2026-11-12 用户实机「获得1。」：RitsuLib 的 TextEnergyIconPath 覆盖会走
    /// PreloadManager.Cache.GetTexture2D(path)，但随机卡渲染时这张图还没进缓存，
    /// 于是 [img] 标签失效，只渲染出数字。
    /// 这里主动把三个卡池的 TextEnergyIconPath 加载进缓存，并同时缓存正/反斜杠两种键，
    /// 因为 Path.Join 在 Windows 上产出的键是混合斜杠。
    /// </summary>
    internal static void PrecacheEnergyIcons()
    {
        void Cache(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            var normalized = path.Replace('\\', '/');
            var texture = GD.Load<Texture2D>("res://" + normalized);
            if (texture is null)
            {
                Log.Warn($"[CuteSakikoModRandom] 能量图标加载失败：{normalized}");
                return;
            }
            foreach (var key in new[]
                     {
                         path,
                         normalized,
                         normalized.Replace('/', '\\')
                     })
                MegaCrit.Sts2.Core.Assets.PreloadManager.Cache.SetAsset(key, texture);
            Log.Info($"[CuteSakikoModRandom] 能量图标已缓存：{normalized}");
        }

        Cache(MegaCrit.Sts2.Core.Models.ModelDb
            .CardPool<CuteSakikoMod.CuteSakikoModCode.Pools.Saki.CuteSakiCardPool>().TextEnergyIconPath);
        Cache(MegaCrit.Sts2.Core.Models.ModelDb
            .CardPool<CuteSakikoMod.CuteSakikoModCode.Pools.Anon.CuteAnonCardPool>().TextEnergyIconPath);
        Cache(MegaCrit.Sts2.Core.Models.ModelDb
            .CardPool<CuteSakikoMod.CuteSakikoModCode.Pools.Rana.CuteRanaCardPool>().TextEnergyIconPath);
    }
}





