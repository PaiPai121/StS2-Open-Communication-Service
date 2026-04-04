using System;
using System.Globalization;
using System.IO;
using Godot;
using MegaCrit.Sts2.Core.Modding;

namespace ModTemplate.ModTemplateCode;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "ModTemplate";
    private static readonly string ConfigPath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "SlayTheSpire2", "modtemplate-speed.txt");

    // 修复 CS8618 警告：加上 ? 标记，告诉编译器这些 UI 节点初始允许为 null
    private static CanvasLayer? _uiLayer;
    private static PanelContainer? _panel;
    private static Label? _speedLabel;
    private static HSlider? _speedSlider;
    
    private static float _currentSpeed = 1.0f;
    public const float MinSpeed = 0.1f;
    public const float MaxSpeed = 50.0f; 
    public const float DefaultSpeed = 1.0f; // 修复 CS0103 错误：补回默认速度定义
    private const float SpeedStep = 0.5f;
    private static bool _isPanelVisible = true;
    private static bool _isUpdatingUi = false;

    private static bool _wasUpPressed = false;
    private static bool _wasDownPressed = false;
    private static bool _wasZeroPressed = false;
    private static bool _wasF6Pressed = false;

    // 🔥 --- 杀戮尖塔 标志性色彩空间 ---
    private static readonly Color ColorBgParchment = new Color(0.18f, 0.16f, 0.14f, 0.96f); 
    private static readonly Color ColorStsBronze = new Color(0.6f, 0.5f, 0.35f, 0.8f);    
    private static readonly Color ColorStsWhite = new Color(0.85f, 0.85f, 0.85f);       
    private static readonly Color ColorStsBlightGreen = new Color(0.2f, 0.8f, 0.4f);    
    private static readonly Color ColorStsBloodRed = new Color(0.45f, 0.15f, 0.15f, 0.9f); 
    private static readonly Color ColorStsMaroon = new Color(0.35f, 0.12f, 0.12f);       
    private static readonly Color ColorStsGold = new Color(0.95f, 0.9f, 0.6f);         

    public static void Initialize()
    {
        _currentSpeed = LoadSavedSpeed();
        Callable.From(StartInjectionLoop).CallDeferred();
    }

    private static void StartInjectionLoop()
    {
        if (Engine.GetMainLoop() is SceneTree tree) { tree.ProcessFrame += OnEveryFrame; }
        else { Callable.From(StartInjectionLoop).CallDeferred(); }
    }

    private static void OnEveryFrame()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null) return;
        
        if (!GodotObject.IsInstanceValid(_uiLayer)) 
        { 
            BuildStsUI(tree.Root); 
            ApplySpeed(_currentSpeed, false); 
        }
        PollInputs();
    }

    private static void BuildStsUI(Window root)
    {
        _uiLayer = new CanvasLayer { Name = "SpeedModOverlay", Layer = 128 };
        
        _panel = new PanelContainer { Visible = _isPanelVisible };
        _panel.Position = new Vector2(24, 24);
        _panel.CustomMinimumSize = new Vector2(320, 160);
        
        var style = new StyleBoxFlat();
        style.BgColor = ColorBgParchment;
        style.BorderColor = ColorStsBronze;
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(6);
        
        style.ShadowColor = new Color(0, 0, 0, 0.5f);
        style.ShadowSize = 6;
        style.ShadowOffset = new Vector2(2, 2);
        
        _panel.AddThemeStyleboxOverride("panel", style);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_bottom", 12);

        var vbox = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        vbox.AddThemeConstantOverride("separation", 10);

        var title = new Label { Text = "Speed Control (F6)" };
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeColorOverride("font_color", ColorStsWhite);
        title.AddThemeFontSizeOverride("font_size", 16);
        vbox.AddChild(title);

        _speedLabel = new Label { Text = "1.00x" };
        _speedLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _speedLabel.AddThemeFontSizeOverride("font_size", 30);
        _speedLabel.AddThemeColorOverride("font_color", ColorStsBlightGreen);
        vbox.AddChild(_speedLabel);

        _speedSlider = new HSlider
        {
            MinValue = MinSpeed, MaxValue = 10.0f, Step = 0.1f,
            Value = _currentSpeed, MouseFilter = Control.MouseFilterEnum.Stop
        };
        
        var grabber = new StyleBoxFlat { BgColor = ColorStsGold, BorderColor = ColorBgParchment };
        grabber.SetCornerRadiusAll(4);
        grabber.SetBorderWidthAll(1);
        
        _speedSlider.MouseFilter = Control.MouseFilterEnum.Stop;
        _speedSlider.ValueChanged += OnSliderChanged;
        vbox.AddChild(_speedSlider);

        var btnRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        btnRow.AddThemeConstantOverride("separation", 10);
        btnRow.AddChild(CreatePresetButton("1x", 1.0f));
        btnRow.AddChild(CreatePresetButton("2x", 2.0f));
        btnRow.AddChild(CreatePresetButton("3x", 3.0f));
        btnRow.AddChild(CreatePresetButton("5x", 5.0f));
        vbox.AddChild(btnRow);

        margin.AddChild(vbox);
        _panel.AddChild(margin);
        _uiLayer.AddChild(_panel);
        root.AddChild(_uiLayer);
    }

    private static Button CreatePresetButton(string text, float speed)
    {
        var btn = new Button { Text = text, CustomMinimumSize = new Vector2(55, 30) };
        btn.MouseFilter = Control.MouseFilterEnum.Stop;
        
        var styleNormal = new StyleBoxFlat { BgColor = ColorStsBloodRed };
        styleNormal.SetCornerRadiusAll(4);
        styleNormal.SetBorderWidthAll(1);
        styleNormal.BorderColor = ColorStsBronze;
        btn.AddThemeStyleboxOverride("normal", styleNormal);
        
        // 修复 CS8602 警告：使用强制类型转换而不是 as 关键字
        var styleHover = (StyleBoxFlat)styleNormal.Duplicate();
        styleHover.BgColor = ColorStsBloodRed.Lightened(0.1f);
        btn.AddThemeStyleboxOverride("hover", styleHover);
        
        var stylePressed = (StyleBoxFlat)styleNormal.Duplicate();
        stylePressed.BgColor = ColorStsMaroon;
        btn.AddThemeStyleboxOverride("pressed", stylePressed);

        btn.AddThemeColorOverride("font_color", ColorStsMaroon.Lightened(0.6f));
        btn.AddThemeColorOverride("font_hover_color", ColorStsMaroon.Lightened(0.8f));
        btn.AddThemeColorOverride("font_pressed_color", Colors.White);
        btn.AddThemeFontSizeOverride("font_size", 14);

        btn.Pressed += () => ApplySpeed(speed);
        return btn;
    }

    private static void OnSliderChanged(double value)
    { 
        if (!_isUpdatingUi) ApplySpeed((float)value); 
    }

    private static void PollInputs()
    {
        bool isCtrl = Input.IsPhysicalKeyPressed(Key.Ctrl);
        bool isUp = Input.IsPhysicalKeyPressed(Key.Up);
        bool isDown = Input.IsPhysicalKeyPressed(Key.Down);
        bool isZero = Input.IsPhysicalKeyPressed(Key.Key0);
        bool isF6 = Input.IsPhysicalKeyPressed(Key.F6);

        if (isCtrl && isUp && !_wasUpPressed) ApplySpeed(_currentSpeed + SpeedStep);
        if (isCtrl && isDown && !_wasDownPressed) ApplySpeed(_currentSpeed - SpeedStep);
        if (isZero && !_wasZeroPressed) ApplySpeed(DefaultSpeed);
        
        if (isF6 && !_wasF6Pressed) 
        { 
            if (GodotObject.IsInstanceValid(_panel) && _panel != null) 
            { 
                _isPanelVisible = !_panel.Visible; 
                _panel.Visible = _isPanelVisible; 
            } 
        }
        
        _wasUpPressed = isUp; 
        _wasDownPressed = isDown; 
        _wasZeroPressed = isZero; 
        _wasF6Pressed = isF6;
    }

    private static void ApplySpeed(float newSpeed, bool save = true)
    {
        _currentSpeed = Mathf.Clamp(newSpeed, MinSpeed, MaxSpeed);
        Engine.TimeScale = _currentSpeed;
        _isUpdatingUi = true;
        
        if (GodotObject.IsInstanceValid(_speedLabel) && _speedLabel != null) 
        { 
            _speedLabel.Text = $"{_currentSpeed:0.00}x"; 
        }
        
        if (GodotObject.IsInstanceValid(_speedSlider) && _speedSlider != null && !Mathf.IsEqualApprox((float)_speedSlider.Value, _currentSpeed)) 
        { 
            _speedSlider.SetValueNoSignal(_currentSpeed); 
        }
        
        _isUpdatingUi = false;
        if (save) SaveSpeed(_currentSpeed);
    }

    private static float LoadSavedSpeed()
    { 
        try 
        { 
            if (File.Exists(ConfigPath) && float.TryParse(File.ReadAllText(ConfigPath), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) 
            { 
                return Mathf.Clamp(parsed, MinSpeed, MaxSpeed); 
            } 
        } 
        catch { } 
        return 1.0f; 
    }

    private static void SaveSpeed(float speed)
    { 
        try 
        { 
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!); 
            File.WriteAllText(ConfigPath, speed.ToString(CultureInfo.InvariantCulture)); 
        } 
        catch { } 
    }
}