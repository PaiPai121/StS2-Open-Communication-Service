using System;
using System.IO;
using Godot;
using MegaCrit.Sts2.Core.Modding;

namespace ModTemplate.ModTemplateCode;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "ModTemplate";
    public static readonly string ProbeLogPath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "SlayTheSpire2", "sts2-speed-mod-probe.log");

    // 全局静态变量，直接持有原生的 Godot 组件，不需要任何自定义类！
    private static CanvasLayer _uiLayer;
    private static Label _speedLabel;
    private static float _currentSpeed = 1.0f;
    
    // 按键防抖变量
    private static bool _wasUpPressed = false;
    private static bool _wasDownPressed = false;
    private static bool _wasZeroPressed = false;

    public static void Initialize()
    {
        Probe("=== 降维打击版 Mod 启动 ===");
        Callable.From(StartInjectionLoop).CallDeferred();
    }

    private static void StartInjectionLoop()
    {
        if (Engine.GetMainLoop() is SceneTree tree)
        {
            // 核心魔法：我们在引擎渲染的每一帧里，直接统管一切！
            tree.ProcessFrame += OnEveryFrame;
            Probe("成功夺取引擎逐帧控制权！");
        }
        else
        {
            Callable.From(StartInjectionLoop).CallDeferred();
        }
    }

    // 这个方法每秒钟会执行 60 次！
    private static void OnEveryFrame()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null) return;

        // 1. 如果 UI 还没建立，直接用纯原生组件硬拼出来！
        if (!GodotObject.IsInstanceValid(_uiLayer))
        {
            BuildNativeUI(tree.Root);
        }

        // 2. 暴力读取键盘状态！
        PollInputs();
    }

    private static void BuildNativeUI(Window root)
    {
        // 全部使用 Godot 自带的类，它绝对能认出来！
        _uiLayer = new CanvasLayer { Name = "PureNativeOverlay", Layer = 128 };
        
        _speedLabel = new Label { Text = "Speed: 1.0x" };
        _speedLabel.AddThemeFontSizeOverride("font_size", 40);
        _speedLabel.AddThemeColorOverride("font_color", Colors.LimeGreen);
        _speedLabel.Position = new Vector2(30, 30);

        _uiLayer.AddChild(_speedLabel);
        root.AddChild(_uiLayer);
        
        Probe("纯原生 UI 挂载成功！你绝对能看到这行绿字！");
    }

    private static void PollInputs()
    {
        bool isCtrl = Input.IsPhysicalKeyPressed(Key.Ctrl);
        bool isUp = Input.IsPhysicalKeyPressed(Key.Up);
        bool isDown = Input.IsPhysicalKeyPressed(Key.Down);
        bool isZero = Input.IsPhysicalKeyPressed(Key.Key0);

        // 监听：Ctrl + ↑ (加速)
        if (isCtrl && isUp && !_wasUpPressed)
        {
            ApplySpeed(_currentSpeed + 0.5f);
        }
        
        // 监听：Ctrl + ↓ (减速)
        if (isCtrl && isDown && !_wasDownPressed)
        {
            ApplySpeed(_currentSpeed - 0.5f);
        }

        // 监听：0键 (重置)
        if (isZero && !_wasZeroPressed)
        {
            ApplySpeed(1.0f);
        }

        // 记录状态，防止按住不放瞬间加到几百倍
        _wasUpPressed = isUp;
        _wasDownPressed = isDown;
        _wasZeroPressed = isZero;
    }

    private static void ApplySpeed(float newSpeed)
    {
        _currentSpeed = Mathf.Clamp(newSpeed, 0.1f, 50.0f);
        Engine.TimeScale = _currentSpeed;

        if (GodotObject.IsInstanceValid(_speedLabel))
        {
            _speedLabel.Text = $"Speed: {_currentSpeed:0.0}x";
        }
        
        Probe($"速度切换为: {_currentSpeed}x");
    }

    public static void Probe(string message)
    {
        var line = $"{DateTime.Now:O} | {message}{System.Environment.NewLine}";
        try { File.AppendAllText(ProbeLogPath, line); } catch { }
    }
}