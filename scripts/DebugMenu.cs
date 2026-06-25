using Godot;
using System.Collections.Generic;

/// <summary>
/// Menu de debug in-game para ajustar parâmetros de movimento em tempo real.
/// Tab  → abre / fecha o painel.
/// </summary>
public partial class DebugMenu : CanvasLayer
{
    private Panel                              _panel;
    private RagdollPlayer                      _player;
    private readonly Dictionary<string, HSlider> _sliders = new();
    private readonly Dictionary<string, Label>   _valLbls = new();

    // (nome, min, max, step)
    private static readonly (string, float, float, float)[] Params =
    {
        ("MoveSpeed",    0.5f, 15f,  0.1f),
        ("JumpVelocity", 1f,   30f,  0.5f),
        ("Gravity",      5f,   50f,  0.5f),
        ("Acceleration", 1f,   30f,  0.5f),
    };

    public override void _Ready()
    {
        Layer   = 20;
        Visible = false;

        if (!InputMap.HasAction("debug_menu"))
        {
            InputMap.AddAction("debug_menu");
            InputMap.ActionAddEvent("debug_menu", new InputEventKey { Keycode = Key.Tab });
        }

        BuildUI();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("debug_menu"))
            Toggle();
    }

    public override void _Process(double delta)
    {
        if (_player == null)
        {
            var nodes = GetTree().GetNodesInGroup("player_ragdoll");
            if (nodes.Count > 0) _player = nodes[0] as RagdollPlayer;
        }
    }

    // ── toggle ────────────────────────────────────────────

    private void Toggle()
    {
        Visible = !Visible;

        if (Visible)
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
            if (_player != null) SyncFromPlayer();
        }
        else
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

    // ── sincronização ─────────────────────────────────────

    private void SyncFromPlayer()
    {
        SetSlider("MoveSpeed",    _player.MoveSpeed);
        SetSlider("JumpVelocity", _player.JumpVelocity);
        SetSlider("Gravity",      _player.Gravity);
        SetSlider("Acceleration", _player.Acceleration);
    }

    private void SetSlider(string name, float value)
    {
        if (_sliders.TryGetValue(name, out var sl)) sl.SetValueNoSignal(value);
        if (_valLbls.TryGetValue(name, out var lb)) lb.Text = value.ToString("F1");
    }

    private void OnSliderChanged(string name, double rawValue)
    {
        float v = (float)rawValue;
        if (_valLbls.TryGetValue(name, out var lb)) lb.Text = v.ToString("F1");
        if (_player == null) return;

        switch (name)
        {
            case "MoveSpeed":    _player.MoveSpeed    = v; break;
            case "JumpVelocity": _player.JumpVelocity = v; break;
            case "Gravity":      _player.Gravity      = v; break;
            case "Acceleration": _player.Acceleration = v; break;
        }
    }

    // ── UI ────────────────────────────────────────────────

    private void BuildUI()
    {
        _panel = new Panel();
        _panel.Position = new Vector2(20, 20);
        _panel.Size     = new Vector2(360, 60 + Params.Length * 48);
        AddChild(_panel);

        var margin = new MarginContainer();
        margin.AnchorRight  = 1; margin.AnchorBottom = 1;
        margin.OffsetLeft   = 10; margin.OffsetTop    = 10;
        margin.OffsetRight  = -10; margin.OffsetBottom = -10;
        _panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);
        margin.AddChild(vbox);

        var title = new Label { Text = "⚙  Debug Menu  —  Tab para fechar" };
        title.AddThemeFontSizeOverride("font_size", 15);
        vbox.AddChild(title);
        vbox.AddChild(new HSeparator());

        foreach (var (pName, pMin, pMax, pStep) in Params)
            AddParamRow(vbox, pName, pMin, pMax, pStep);
    }

    private void AddParamRow(Container parent, string pName, float min, float max, float step)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        parent.AddChild(row);

        var lbl = new Label
        {
            Text = pName,
            CustomMinimumSize = new Vector2(160, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        row.AddChild(lbl);

        var slider = new HSlider
        {
            MinValue = min, MaxValue = max, Step = step,
            CustomMinimumSize   = new Vector2(130, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        row.AddChild(slider);

        var valLbl = new Label
        {
            Text = "—",
            CustomMinimumSize   = new Vector2(52, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment   = VerticalAlignment.Center
        };
        row.AddChild(valLbl);

        _sliders[pName] = slider;
        _valLbls[pName] = valLbl;

        string captured = pName;
        slider.ValueChanged += (v) => OnSliderChanged(captured, v);
    }
}
