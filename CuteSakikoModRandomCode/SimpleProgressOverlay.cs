using Godot;

namespace CuteSakikoModRandom.CuteSakikoModRandomCode;

/// <summary>
/// 独立于 AA overlay 的进度层：AA 的 host 挂在 GameTransitionRect 上时会被 NTransition 的黑色
/// ColorRect 盖住；这里用最高层 CanvasLayer 直接画在根窗口上，保证生成阶段一定能看到文字。
///
/// ★ 2026-11-12：从 RandomEarlyProgressPatch 里抽出来，单人和多人开局共用。
///   注意：调用方必须在**主线程**每帧调用 <see cref="Report"/>/<see cref="Entering"/>，并让主循环出帧；
///   如果主线程被同步阻塞，Godot 不会重绘，标签就看不到（多人同步前缀踩过这个坑）。
/// </summary>
internal sealed class SimpleProgressOverlay : IDisposable
{
    private readonly CanvasLayer _layer;
    private readonly Label _label;
    private readonly int _total;

    internal SimpleProgressOverlay(int total)
    {
        _total = Math.Max(1, total);
        _layer = new CanvasLayer { Layer = 32760 };
        var host = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        host.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect,
            Control.LayoutPresetMode.Minsize, 0);

        var backdrop = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.82f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect,
            Control.LayoutPresetMode.Minsize, 0);

        _label = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect,
            Control.LayoutPresetMode.Minsize, 0);
        _label.AddThemeFontSizeOverride("font_size", 34);
        _label.AddThemeColorOverride("font_color", Colors.White);
        _label.AddThemeColorOverride("font_outline_color", Colors.Black);
        _label.AddThemeConstantOverride("outline_size", 8);

        host.AddChild(backdrop);
        host.AddChild(_label);
        _layer.AddChild(host);
        if (Engine.GetMainLoop() is SceneTree tree)
            tree.Root.AddChild(_layer);

        Report(1);
    }

    internal void Report(int current)
    {
        _label.Text = $"正在生成卡池中……（{Math.Clamp(current, 1, _total)}/{_total}）";
    }

    internal void Entering() => _label.Text = "正在进入游戏……";

    public void Dispose()
    {
        if (_layer.IsInsideTree()) _layer.QueueFree();
        else _layer.Free();
    }
}
