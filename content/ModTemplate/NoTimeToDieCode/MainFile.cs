using System;
using System.Globalization;
using System.IO;
using Godot;
using MegaCrit.Sts2.Core.Modding;

namespace NoTimeToDie.Code;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "NoTimeToDie"; 
    
    private static readonly string ConfigPath = Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), 
        "SlayTheSpire2", 
        $"{ModId.ToLower()}_config.txt"
    );

    private static CanvasLayer? _uiLayer;
    private static PanelContainer? _panel;
    private static Label? _speedLabel;
    private static HSlider? _speedSlider;
    
    private static float _currentSpeed = 1.0f;
    public const float MinSpeed = 0.1f;
    public const float MaxSpeed = 50.0f; 
    public const float DefaultSpeed = 1.0f;
    private const float SpeedStep = 0.25f; 
    private static bool _isPanelVisible = true;
    private static bool _isUpdatingUi = false;

    private static bool _wasUpPressed = false;
    private static bool _wasDownPressed = false;
    private static bool _wasZeroPressed = false;
    private static bool _wasF6Pressed = false;

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
        if (Engine.GetMainLoop() is not SceneTree tree || tree.Root == null) return;
        
        if (!GodotObject.IsInstanceValid(_uiLayer)) 
        { 
            BuildStsUI(tree.Root); 
            ApplySpeed(_currentSpeed, false); 
        }
        PollInputs();
    }

    private static void BuildStsUI(Window root)
    {
        _uiLayer = new CanvasLayer { Name = $"{ModId}Overlay", Layer = 128 };
        
        _panel = new PanelContainer { Visible = _isPanelVisible };
        _panel.Position = new Vector2(30, 30);
        _panel.CustomMinimumSize = new Vector2(320, 180); // 稍微加高一点点给提示留位置
        
        var style = new StyleBoxFlat();
        style.BgColor = ColorBgParchment;
        style.BorderColor = ColorStsBronze;
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(4);
        style.ShadowColor = new Color(0, 0, 0, 0.4f);
        style.ShadowSize = 8;
        _panel.AddThemeStyleboxOverride("panel", style);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_top", 15);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_bottom", 15);

        var vbox = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        vbox.AddThemeConstantOverride("separation", 10);

        // 标题
        var title = new Label { Text = "NO TIME TO DIE" };
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeColorOverride("font_color", ColorStsWhite);
        title.AddThemeFontSizeOverride("font_size", 18);
        vbox.AddChild(title);

        // 速度数值
        _speedLabel = new Label { Text = "1.00x" };
        _speedLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _speedLabel.AddThemeFontSizeOverride("font_size", 34);
        _speedLabel.AddThemeColorOverride("font_color", ColorStsBlightGreen);
        vbox.AddChild(_speedLabel);

        // 滑动条
        _speedSlider = new HSlider
        {
            MinValue = MinSpeed, MaxValue = 10.0f, Step = 0.1f,
            Value = _currentSpeed, MouseFilter = Control.MouseFilterEnum.Stop
        };
        _speedSlider.ValueChanged += OnSliderChanged;
        vbox.AddChild(_speedSlider);

        // 按钮排
        var btnRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        btnRow.AddThemeConstantOverride("separation", 10);
        btnRow.AddChild(CreatePresetButton("1x", 1.0f));
        btnRow.AddChild(CreatePresetButton("3x", 3.0f));
        btnRow.AddChild(CreatePresetButton("10x", 10.0f));
        vbox.AddChild(btnRow);

        // --- 关键：添加底部指引字样 ---
        var hint = new Label { Text = "Press F6 to Toggle UI | Ctrl + Up/Down" };
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        hint.AddThemeColorOverride("font_color", ColorStsGold.Darkened(0.3f)); // 使用暗金色，低调且专业
        hint.AddThemeFontSizeOverride("font_size", 12); // 小字号
        vbox.AddChild(hint);

        margin.AddChild(vbox);
        _panel.AddChild(margin);
        _uiLayer.AddChild(_panel);
        root.AddChild(_uiLayer);
    }

    private static Button CreatePresetButton(string text, float speed)
    {
        var btn = new Button { Text = text, CustomMinimumSize = new Vector2(60, 32) };
        btn.MouseFilter = Control.MouseFilterEnum.Stop;
        
        var styleNormal = new StyleBoxFlat { BgColor = ColorStsBloodRed };
        styleNormal.SetCornerRadiusAll(2);
        styleNormal.BorderColor = ColorStsBronze;
        styleNormal.SetBorderWidthAll(1);
        btn.AddThemeStyleboxOverride("normal", styleNormal);
        
        var styleHover = (StyleBoxFlat)styleNormal.Duplicate();
        styleHover.BgColor = ColorStsBloodRed.Lightened(0.15f);
        btn.AddThemeStyleboxOverride("hover", styleHover);
        
        btn.AddThemeColorOverride("font_color", ColorStsGold);
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
        
        _wasUpPressed = isUp; _wasDownPressed = isDown; _wasZeroPressed = isZero; _wasF6Pressed = isF6;
    }

    private static void ApplySpeed(float newSpeed, bool save = true)
    {
        _currentSpeed = Mathf.Clamp(newSpeed, MinSpeed, MaxSpeed);
        Engine.TimeScale = _currentSpeed;
        _isUpdatingUi = true;
        
        if (GodotObject.IsInstanceValid(_speedLabel) && _speedLabel != null) 
            _speedLabel.Text = $"{_currentSpeed:0.00}x"; 
        
        if (GodotObject.IsInstanceValid(_speedSlider) && _speedSlider != null) 
            _speedSlider.SetValueNoSignal(Mathf.Min(_currentSpeed, 10.0f)); 
        
        _isUpdatingUi = false;
        if (save) SaveSpeed(_currentSpeed);
    }

    private static float LoadSavedSpeed()
    { 
        try { if (File.Exists(ConfigPath) && float.TryParse(File.ReadAllText(ConfigPath), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) return Mathf.Clamp(parsed, MinSpeed, MaxSpeed); } 
        catch { } return 1.0f; 
    }

    private static void SaveSpeed(float speed)
    { 
        try { Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!); File.WriteAllText(ConfigPath, speed.ToString(CultureInfo.InvariantCulture)); } 
        catch { } 
    }
}