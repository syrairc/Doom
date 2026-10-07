using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;
using ExileCore;
using ExileImGui;
using ImWindows = ExileImGui.Windows;
using ImGuiNET;
using ManagedDoom;
using ManagedDoom.UserInput;
using ManagedDoom.Video;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using DoomEngine = ManagedDoom.Doom;
using Vector2 = System.Numerics.Vector2;

namespace Doom;

public class Doom : BaseSettingsPlugin<DoomSettings>
{
    private const string TextureName = "doom_frame";
    private const float AspectRatio = 4f / 3f;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private DoomHost _host;
    private bool _open;

    public override bool Initialise()
    {
        ConfigUtilities.DataDirectory = ConfigDirectory;
        Force = true;
        return true;
    }

    public override void Render()
    {
        if (Settings.ToggleWindow.PressedOnce()) _open = !_open;
        if (!_open) return;

        if (ImWindows.Begin("Doom", "doom", ref _open, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse, new Vector2(660, 520)))
        {
            try
            {
                DrawGame();
            }
            catch (Exception e)
            {
                LogError($"Doom crashed: {e}");
                CloseGame();
                _open = false;
            }
        }
        ImWindows.End();

        if (!_open) _host?.ReleaseKeys();
    }

    public override void DrawSettings()
    {
        base.DrawSettings();
        if (ImGui.Button("Show Doom window")) _open = true;
        ImGui.Separator();

        if (!Tables.Begin("doom_keys", new[] { ("Key", 160f), ("Action", 0f) }, flags: ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV)) return;
        (string Key, string Action)[] rows = [($"{Settings.ToggleWindow.Value.Key}", "Show / hide the Doom window"), .. KeyHelp];
        for (var i = 0; i < rows.Length; i++)
        {
            using var row = Tables.Row(i);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(rows[i].Key);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(rows[i].Action);
        }
        Tables.End();
    }

    private static readonly (string Key, string Action)[] KeyHelp =
    {
        ("W / Up", "Forward"),
        ("S / Down", "Back"),
        ("A / D", "Strafe left / right"),
        ("Left / Right", "Turn"),
        ("Alt + Left / Right", "Strafe instead of turn"),
        ("Shift", "Walk (always-run is on)"),
        ("Ctrl / Left click", "Fire"),
        ("Space / Right click", "Use - doors, switches"),
        ("1 - 7", "Weapons"),
        ("Tab", "Automap"),
        ("- / =", "Shrink / grow view (zoom on automap)"),
        ("F / M / C", "Automap follow / mark / clear marks"),
        ("Pause", "Pause"),
        ("H", "HUD S3ttings"),
        ("Esc", "Menu - Enter selects, Y / N confirms"),
        ("F1", "Help"),
        ("F2 / F3", "Save / Load"),
        ("F6 / F9", "Quicksave / Quickload"),
        ("F7", "End game"),
        ("F8", "Toggle messages"),
        ("F10", "Quit"),
        ("F11", "Gamma"),
        ("IDDQD / IDKFA", "God mode / all weapons and keys"),
        ("IDCLIP / IDCLEV##", "No-clip / warp to level"),
    };

    public override void Dispose()
    {
        CloseGame();
        base.Dispose();
    }

    private void DrawGame()
    {
        _host ??= new DoomHost(Path.Combine(DirectoryFullName, "freedoom1.wad"));

        var avail = ImGui.GetContentRegionAvail();
        var size = avail.X / avail.Y > AspectRatio ? new Vector2(avail.Y * AspectRatio, avail.Y) : new Vector2(avail.X, avail.X / AspectRatio);
        ImGui.SetCursorPos(ImGui.GetCursorPos() + (avail - size) / 2);
        var topLeft = ImGui.GetCursorScreenPos();
        ImGui.Dummy(size);

        _host.Poll(ImGui.IsWindowFocused(), ImGui.IsItemHovered());

        if (!_host.Step(_clock.Elapsed))
        {
            CloseGame();
            _open = false;
            return;
        }

        if (_host.TakeFrame(out var image))
        {
            using (image) Graphics.AddOrUpdateImage(TextureName, image);
        }

        var bottomRight = topLeft + size;
        ImGui.GetWindowDrawList().AddImageQuad(Graphics.GetTextureId(TextureName),
            topLeft, new Vector2(bottomRight.X, topLeft.Y), bottomRight, new Vector2(topLeft.X, bottomRight.Y),
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
    }

    private void CloseGame()
    {
        _host?.Dispose();
        _host = null;
    }
}

internal sealed class DoomHost : IVideo, IUserInput, IDisposable
{
    private static readonly (Keys Vk, DoomKey Key)[] KeyMap = Enumerable.Range(0, 26).Select(i => (Keys.A + i, DoomKey.A + i))
        .Concat(Enumerable.Range(0, 10).Select(i => (Keys.D0 + i, DoomKey.Num0 + i)))
        .Concat(Enumerable.Range(0, 12).Select(i => (Keys.F1 + i, DoomKey.F1 + i)))
        .Concat(new[]
        {
            (Keys.Escape, DoomKey.Escape), (Keys.Return, DoomKey.Enter), (Keys.Space, DoomKey.Space), (Keys.Back, DoomKey.Backspace), (Keys.Tab, DoomKey.Tab),
            (Keys.Up, DoomKey.Up), (Keys.Down, DoomKey.Down), (Keys.Left, DoomKey.Left), (Keys.Right, DoomKey.Right),
            (Keys.LControlKey, DoomKey.LControl), (Keys.RControlKey, DoomKey.RControl), (Keys.LShiftKey, DoomKey.LShift), (Keys.RShiftKey, DoomKey.RShift), (Keys.LMenu, DoomKey.LAlt), (Keys.RMenu, DoomKey.RAlt),
            (Keys.PageUp, DoomKey.PageUp), (Keys.PageDown, DoomKey.PageDown), (Keys.Home, DoomKey.Home), (Keys.End, DoomKey.End), (Keys.Insert, DoomKey.Insert), (Keys.Delete, DoomKey.Delete), (Keys.Pause, DoomKey.Pause),
            (Keys.OemMinus, DoomKey.Subtract), (Keys.Oemplus, DoomKey.Equal), (Keys.Oemcomma, DoomKey.Comma), (Keys.OemPeriod, DoomKey.Period), (Keys.OemQuestion, DoomKey.Slash), (Keys.OemSemicolon, DoomKey.Semicolon),
            (Keys.OemOpenBrackets, DoomKey.LBracket), (Keys.OemCloseBrackets, DoomKey.RBracket), (Keys.OemPipe, DoomKey.Backslash),
        })
        .ToArray();

    private readonly Config _config;
    private readonly GameContent _content;
    private readonly Renderer _renderer;
    private readonly DoomEngine _engine;
    private readonly byte[] _frame;
    private readonly HashSet<DoomKey> _held = new();
    private readonly HashSet<DoomMouseButton> _buttons = new();
    private readonly FakeExileHud _hud;
    private bool _focused;
    private bool _frameReady;
    private int _turnHeld;
    private int _tic = -1;
    private TimeSpan _due;

    public DoomHost(string wadPath)
    {
        _config = new Config(ConfigUtilities.GetConfigPath());
        _config.video_fpsscale = Math.Clamp(_config.video_fpsscale, 1, 4);
        var args = new CommandLineArgs(new[] { "-iwad", wadPath });
        _content = new GameContent(args);
        _renderer = new Renderer(_config, _content);
        _frame = new byte[4 * _renderer.Width * _renderer.Height];
        _engine = new DoomEngine(args, _config, _content, this, null, null, this);
        _hud = new FakeExileHud(_content);
        _renderer.Overlay = screen =>
        {
            if (_hud.Open) _hud.Draw(screen, _tic / _config.video_fpsscale);
            else if (_engine.State == DoomState.Game && _engine.Game.State == GameState.Level) _hud.DrawHint(screen);
        };
    }

    public void Poll(bool focused, bool hovered)
    {
        _focused = focused;
        foreach (var (vk, key) in KeyMap)
        {
            var down = focused && Input.IsKeyDown(vk);
            if (down == _held.Contains(key)) continue;
            if (down) _held.Add(key);
            else _held.Remove(key);
            if (key == DoomKey.H && down) _hud.Open = !_hud.Open;
            _engine.PostEvent(new DoomEvent(down ? EventType.KeyDown : EventType.KeyUp, key));
        }

        _buttons.Clear();
        if (!focused || !hovered) return;
        if (Input.IsKeyDown(Keys.LButton)) _buttons.Add(DoomMouseButton.Mouse1);
        if (Input.IsKeyDown(Keys.RButton)) _buttons.Add(DoomMouseButton.Mouse2);
        if (Input.IsKeyDown(Keys.MButton)) _buttons.Add(DoomMouseButton.Mouse3);
    }

    public void ReleaseKeys() => Poll(false, false);

    public bool Step(TimeSpan now)
    {
        var scale = _config.video_fpsscale;
        var step = TimeSpan.FromSeconds(1.0 / (35 * scale));
        if (now - _due > TimeSpan.FromMilliseconds(250)) _due = now;

        while (_due <= now)
        {
            _due += step;
            _tic++;
            if (_tic % scale == 0 && _engine.Update() == UpdateResult.Completed) return false;
            _renderer.Render(_engine, _frame, Fixed.FromInt(_tic % scale + 1) / scale);
            _frameReady = true;
        }

        return true;
    }

    public bool TakeFrame(out Image<Rgba32> image)
    {
        image = _frameReady ? Image.LoadPixelData<Rgba32>(_frame, _renderer.Height, _renderer.Width) : null;
        _frameReady = false;
        return image != null;
    }

    public void Dispose()
    {
        _config.Save(ConfigUtilities.GetConfigPath());
        _content.Dispose();
    }

    public void BuildTicCmd(TicCmd cmd)
    {
        var keyTurnLeft = IsPressed(_config.key_turnleft);
        var keyTurnRight = IsPressed(_config.key_turnright);
        var strafe = IsPressed(_config.key_strafe);
        var speed = IsPressed(_config.key_run) ? 1 : 0;
        if (_config.game_alwaysrun) speed = 1 - speed;

        cmd.Clear();

        _turnHeld = keyTurnLeft || keyTurnRight ? _turnHeld + 1 : 0;
        var turnSpeed = _turnHeld < PlayerBehavior.SlowTurnTics ? 2 : speed;

        var forward = 0;
        var side = 0;

        if (strafe)
        {
            if (keyTurnRight) side += PlayerBehavior.SideMove[speed];
            if (keyTurnLeft) side -= PlayerBehavior.SideMove[speed];
        }
        else
        {
            if (keyTurnRight) cmd.AngleTurn -= (short)PlayerBehavior.AngleTurn[turnSpeed];
            if (keyTurnLeft) cmd.AngleTurn += (short)PlayerBehavior.AngleTurn[turnSpeed];
        }

        if (IsPressed(_config.key_forward)) forward += PlayerBehavior.ForwardMove[speed];
        if (IsPressed(_config.key_backward)) forward -= PlayerBehavior.ForwardMove[speed];
        if (IsPressed(_config.key_strafeleft)) side -= PlayerBehavior.SideMove[speed];
        if (IsPressed(_config.key_straferight)) side += PlayerBehavior.SideMove[speed];
        if (IsPressed(_config.key_fire)) cmd.Buttons |= TicCmdButtons.Attack;
        if (IsPressed(_config.key_use)) cmd.Buttons |= TicCmdButtons.Use;

        for (var i = 0; i < 7; i++)
        {
            if (!_held.Contains(DoomKey.Num1 + i)) continue;
            cmd.Buttons |= TicCmdButtons.Change;
            cmd.Buttons |= (byte)(i << TicCmdButtons.WeaponShift);
            break;
        }

        cmd.ForwardMove += (sbyte)Math.Clamp(forward, -PlayerBehavior.MaxMove, PlayerBehavior.MaxMove);
        cmd.SideMove += (sbyte)Math.Clamp(side, -PlayerBehavior.MaxMove, PlayerBehavior.MaxMove);
    }

    private bool IsPressed(KeyBinding binding) => binding.Keys.Any(_held.Contains) || binding.MouseButtons.Any(_buttons.Contains);

    public void Render(DoomEngine doom, Fixed frameFrac) => _renderer.Render(doom, _frame, frameFrac);
    public void InitializeWipe() => _renderer.InitializeWipe();
    public bool HasFocus() => _focused;
    public void Reset() { }
    public void GrabMouse() { }
    public void ReleaseMouse() { }

    public int MaxMouseSensitivity => 15;
    public int MouseSensitivity { get => _config.mouse_sensitivity; set => _config.mouse_sensitivity = value; }
    public int WipeBandCount => _renderer.WipeBandCount;
    public int WipeHeight => _renderer.WipeHeight;
    public int MaxWindowSize => _renderer.MaxWindowSize;
    public int WindowSize { get => _renderer.WindowSize; set => _renderer.WindowSize = value; }
    public bool DisplayMessage { get => _renderer.DisplayMessage; set => _renderer.DisplayMessage = value; }
    public int MaxGammaCorrectionLevel => _renderer.MaxGammaCorrectionLevel;
    public int GammaCorrectionLevel { get => _renderer.GammaCorrectionLevel; set => _renderer.GammaCorrectionLevel = value; }
}

internal sealed class FakeExileHud
{
    private static readonly (string Name, bool Enabled)[] Plugins = { ("Radar", true), ("Minimap", true), ("HealthBars", true), ("PickIt", false), ("Stashie", true), ("Doom", true) };
    private static readonly (string Label, bool? Checked)[] Rows = { ("Enable", true), ("Toggle window", null), ("God mode", true), ("All keys", false), ("Nightmare", null), ("Rip and tear", true) };

    private readonly Patch[] _skulls;
    private readonly byte _bg, _title, _border, _frame, _header, _check;

    public bool Open;

    public FakeExileHud(GameContent content)
    {
        var palette = content.Palette[0];
        byte Nearest(int r, int g, int b) => (byte)Enumerable.Range(0, 256).MinBy(i => Square((int)(palette[i] & 0xFF) - r) + Square((int)(palette[i] >> 8 & 0xFF) - g) + Square((int)(palette[i] >> 16 & 0xFF) - b));
        _bg = Nearest(15, 15, 15);
        _title = Nearest(41, 74, 122);
        _border = Nearest(110, 110, 128);
        _frame = Nearest(29, 47, 73);
        _header = Nearest(36, 58, 90);
        _check = Nearest(66, 150, 250);
        _skulls = new[] { Patch.FromWad(content.Wad, "M_SKULL1"), Patch.FromWad(content.Wad, "M_SKULL2") };
    }

    private static int Square(int v) => v * v;

    public void DrawHint(DrawScreen screen)
    {
        const string hint = "Press H to open HUD";
        var s = screen.Width / 320;
        var scale = Math.Max(1, s / 2);
        screen.DrawText(hint, 318 * s - screen.MeasureText(hint, scale), 166 * s, scale);
    }

    public void Draw(DrawScreen screen, int tic)
    {
        var s = screen.Width / 320;
        void Rect(int x, int y, int w, int h, byte color) => screen.FillRect(x * s, y * s, w * s, h * s, color);
        void Text(string text, int x, int y) => screen.DrawText(text, x * s, (y + 7) * s, s);
        void Checkbox(int x, int y, bool on)
        {
            Rect(x, y, 7, 7, _frame);
            if (on) Rect(x + 2, y + 2, 3, 3, _check);
        }

        Rect(16, 10, 288, 180, _border);
        Rect(17, 11, 286, 178, _bg);
        Rect(17, 11, 286, 11, _title);
        Text("HUD S3ttings (v6.66)", 21, 13);
        Text("X", 294, 13);

        Rect(21, 26, 96, 11, _frame);
        Text("Filter", 24, 28);
        Text("Core", 31, 41);
        for (var i = 0; i < Plugins.Length; i++)
        {
            var y = 52 + i * 11;
            var selected = i == Plugins.Length - 1;
            if (selected) Rect(21, y - 2, 96, 11, _header);
            Checkbox(21, y, Plugins[i].Enabled);
            Text(Plugins[i].Name, 31, y);
            if (selected) screen.DrawPatch(_skulls[tic / 8 % 2], 106 * s, (y - 2) * s, Math.Max(1, s / 2));
        }

        Rect(120, 24, 1, 164, _border);
        Text("Doom", 126, 28);
        Rect(126, 38, 172, 1, _border);
        for (var i = 0; i < Rows.Length; i++)
        {
            var y = 44 + i * 13;
            var (label, on) = Rows[i];
            if (on.HasValue) Checkbox(126, y, on.Value);
            Text(label, on.HasValue ? 136 : 126, y);
        }

        Rect(240, 55, 50, 11, _frame);
        Text("F8", 258, 57);
        Rect(200, 96, 90, 7, _frame);
        Rect(282, 96, 8, 7, _check);
        Text("H - back to hell", 126, 176);
    }
}
