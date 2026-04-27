using System;
using System.Numerics;
using Silk.NET.Windowing;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using ImGuiNET;
using KeplerEngine.Renderer3D.Core;
using KeplerEngine.Renderer3D.Rendering;
using KeplerEngine.Renderer3D.UI;
using KeplerEngine.Simulation;
using KeplerEngine.Physics;
using KeplerEngine.Orbital;
using KeplerEngine.Core;

namespace KeplerEngine.Renderer3D.App;

public class Renderer3DApp
{
    private readonly IWindow        _window;
    private GL?                     _gl;
    private Camera                  _camera  = new();
    private SceneRenderer?          _scene;
    private OrbitSimulation?        _sim;
    private Silk.NET.OpenGL.Extensions.ImGui.ImGuiController? _imgui;
    private StartupMenu             _startupMenu = new();
    private IKeyboard?              _keyboard;
    private IInputContext?          _input;
    private DateTime                _lastTick = DateTime.UtcNow;

    private ManeuverWindow    _maneuverWindow = new();
    private OrbitContextMenu  _contextMenu    = new();

    private OrbitEditorWindow _orbitEditor  = new();
    private float             _mouseX, _mouseY;
    private bool              _mouseWasDragging;

    public Renderer3DApp()
    {
        var opts = WindowOptions.Default with
        {
            Title = "KeplerEngine — 3D Orbit Viewer",
            Size  = new Silk.NET.Maths.Vector2D<int>(1400, 860),
            PreferredDepthBufferBits = 24,
        };

        _window = Window.Create(opts);
        _window.Load    += OnLoad;
        _window.Render  += OnRender;
        _window.Update  += OnUpdate;
        _window.Closing += OnClose;
    }

    public void Run() => _window.Run();

    private void OnLoad()
    {
        _gl    = _window.CreateOpenGL();
        _input = _window.CreateInput();
        _imgui = new Silk.NET.OpenGL.Extensions.ImGui.ImGuiController(_gl, _window, _input);

        _keyboard = _input.Keyboards[0];
        _keyboard.KeyDown += (_, k, _) => _camera.OnKeyDown(k);

        var mouse = _input.Mice[0];
        mouse.MouseUp   += (_, btn) => _camera.OnMouseUp(btn);
        mouse.Scroll    += (_, s)   => _camera.OnScroll(s.Y);

        mouse.MouseMove += (m, pos) =>
        {
            _mouseX = pos.X;
            _mouseY = pos.Y;
            _camera.OnMouseMove(pos.X, pos.Y);
            if (m.IsButtonPressed(MouseButton.Left))
                _mouseWasDragging = true;
        };

        mouse.MouseDown += (m, btn) =>
        {
            _camera.OnMouseDown(btn, m.Position.X, m.Position.Y);
            _mouseWasDragging = false;
        };

        mouse.MouseUp += (m, btn) =>
        {
            _camera.OnMouseUp(btn);
            if (btn == MouseButton.Left && !_mouseWasDragging && !_startupMenu.IsOpen)
                TryPickObject(_mouseX, _mouseY);
        };

        _window.Resize += size =>
        {
            _gl?.Viewport(0, 0, (uint)size.X, (uint)size.Y);
            _imgui?.Update(0);
        };

        
    }

    private void TryPickObject(float mx, float my)
    {
        if (_sim == null || _scene == null) return;
        if (ImGui.GetIO().WantCaptureMouse) return;

        float aspect = _window.Size.X / (float)_window.Size.Y;
        var view = _camera.GetView();
        var proj = _camera.GetProjection(aspect);

        var ray = Core.Ray.FromScreenPoint(mx, my,
            _window.Size.X, _window.Size.Y, view, proj);

        // ── 1. Check satellites ───────────────────────────────────────────────────
        foreach (var body in _sim.Orbitals)
        {
            float   scale  = _scene.Scale;
            float[] center =
            {
                (float)body.Position.X * scale,
                (float)body.Position.Z * scale,
                (float)body.Position.Y * scale
            };
            float hitRadius = Math.Max(
                (float)body.Primary.Radius * scale * 0.05f, 0.05f);

            if (ray.IntersectsSphere(center, hitRadius, out _))
            {
                // Close maneuver window, open orbit editor
                _maneuverWindow.IsOpen  = false;
                _orbitEditor.Target     = body;
                _orbitEditor.IsOpen     = true;
                _scene.SetPredictedOrbit(null, body);
                return;
            }
        }

        // ── 2. Check orbit lines ──────────────────────────────────────────────────
        foreach (var body in _sim.Orbitals)
        {
            float threshold = Math.Max(
                (float)body.Primary.Radius * _scene.Scale * 0.04f, 0.03f);

            if (ray.IntersectsOrbitLine(body, _scene.Scale, threshold,
                out float hitNuDeg))
            {
                // Freeze captured values for closures
                var   capturedBody = body;
                float capturedNu   = hitNuDeg;

                _contextMenu.Open(capturedNu);

                // Option 1: warp simulation time to this point on orbit
                _contextMenu.OnWarpTo = () =>
                {
                    if (_sim == null) return;
                    double nuRad    = OrbitalMath.DegToRad(capturedNu);
                    double nodeUT   = KeplerPropagator.TimeToTrueAnomaly(
                        capturedBody, nuRad, _sim.Clock.UT);
                    double Dt       = nodeUT - _sim.Clock.UT;
                    if (Dt < 0) return;
                    KeplerPropagator.PropagateElements(
                        capturedBody.Elements, capturedBody.Primary.Mu, Dt);
                    _sim.Clock.SetUT(nodeUT);
                };

                // Option 2: open maneuver planner at this node
                _contextMenu.OnAddManeuver = () =>
                {
                    _orbitEditor.IsOpen           = false;
                    _maneuverWindow.Target        = capturedBody;
                    _maneuverWindow.ManeuverNuDeg = capturedNu;
                    _maneuverWindow.IsOpen        = true;
                    _maneuverWindow.UpdatePrediction();
                };

                return;
            }
        }

        // ── 3. Click on empty space — close all panels ────────────────────────────
        _orbitEditor.IsOpen    = false;
        _maneuverWindow.IsOpen = false;
        _scene.SetPredictedOrbit(null,
            _sim.Orbitals.Count > 0 ? _sim.Orbitals[0] : null!);
    }

    private void OnUpdate(double dt)
    {
        _imgui?.Update((float)dt);

        // Пока стартовое меню открыто — не тикаем симуляцию
        if (_startupMenu.IsOpen) return;

        // Меню закрылось — инициализируем симуляцию один раз
        if (_sim == null && _startupMenu.IsDone)
            InitSimulation();

        if (_sim == null) return;

        // Maneuver execution
        if (_sim != null && _maneuverWindow.ExecuteReady && _maneuverWindow.Target != null)
        {
            var    body   = _maneuverWindow.Target;
            double nuRad  = OrbitalMath.DegToRad(_maneuverWindow.ManeuverNuDeg);
        
            // Get absolute UT when body reaches maneuver node
            double nodeUT = KeplerPropagator.TimeToTrueAnomaly(
                body, nuRad, _sim.Clock.UT);
        
            Console.WriteLine($"Current UT  = {_sim.Clock.UT:F2}");
            Console.WriteLine($"Node UT     = {nodeUT:F2}");
            Console.WriteLine($"dt to node  = {nodeUT - _sim.Clock.UT:F2} s");
        
            if (nodeUT <= _sim.Clock.UT)
            {
                // Already past this point in current orbit — wait for next pass
                nodeUT += body.Period;
                Console.WriteLine($"Next pass node UT = {nodeUT:F2}");
            }
        
            // Propagate body to exactly the node
            double dtToNode = nodeUT - _sim.Clock.UT;
            KeplerPropagator.PropagateElements(body.Elements, body.Primary.Mu, dtToNode);
            _sim.Clock.SetUT(nodeUT);
        
            Console.WriteLine($"Body nu before dV = {body.TrueAnomalyDeg:F2} deg (should be {_maneuverWindow.ManeuverNuDeg:F2})");
        
            // Apply delta-v at the node
            body.ApplyDeltaV(
                _maneuverWindow.DvPrograde,
                _maneuverWindow.DvNormal,
                _maneuverWindow.DvRadial);
        
            Console.WriteLine($"After dV: Pe={(body.Elements.Periapsis - body.Primary.Radius)/1000:F1} km  Ap={(body.Elements.Apoapsis - body.Primary.Radius)/1000:F1} km");
        
            _maneuverWindow.ResetExecution();
            _maneuverWindow.IsOpen = false;
            _scene?.SetPredictedOrbit(null, body);
        }

        var now   = DateTime.UtcNow;
        double rdt = (now - _lastTick).TotalSeconds;
        _lastTick  = now;
        _sim.Tick(rdt);

        if (_keyboard != null)
            _camera.UpdateFreeMove(_keyboard, (float)dt);

        _scene?.UpdateDynamic(_sim);
    }

    private void OnRender(double dt)
    {
        if (_gl == null) return;

        // Всегда обновляем viewport по актуальному размеру окна


        if (_startupMenu.IsOpen)
        {
            _gl.ClearColor(0.05f, 0.05f, 0.10f, 1f);
            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            _startupMenu.Draw();
            _imgui?.Render();
            return;
        }

        _scene?.Render(_camera, _window.Size.X, _window.Size.Y);
        DrawHUD();
        _imgui?.Render();
    }

    private void DrawHUD()
    {
        if (_maneuverWindow.IsOpen && _maneuverWindow.Target != null)
        {
            ImGui.SetNextWindowPos(new Vector2(10, 400), ImGuiCond.Always);
            ImGui.Begin("##debug", ImGuiWindowFlags.NoTitleBar |
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize);
            ImGui.Text($"Node nu = {_maneuverWindow.ManeuverNuDeg:F2} deg");
            ImGui.Text($"Sat  nu = {_maneuverWindow.Target.TrueAnomalyDeg:F2} deg");
            ImGui.Text($"dVpro   = {_maneuverWindow.DvPrograde:F2} m/s");
            ImGui.End();
        }

        if (_sim == null) return;

        // Top-left: clock + warp controls
        ImGui.SetNextWindowPos(new Vector2(10, 10), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0.6f);
        ImGui.Begin("##hud",
            ImGuiWindowFlags.NoTitleBar        |
            ImGuiWindowFlags.NoResize          |
            ImGuiWindowFlags.NoMove            |
            ImGuiWindowFlags.AlwaysAutoResize);

        ImGui.TextColored(new Vector4(0.18f, 0.80f, 0.44f, 1f), _sim.Clock.FormatUT());
        ImGui.SameLine(0, 20);
        ImGui.TextColored(new Vector4(0.95f, 0.61f, 0.07f, 1f), $"Warp {_sim.Clock.WarpLabel}");

        ImGui.Spacing();
        if (ImGui.Button("<<"))    _sim.Clock.WarpDown();
        ImGui.SameLine();
        if (ImGui.Button(_sim.Clock.Paused ? "Resume" : "Pause")) _sim.Clock.Toggle();
        ImGui.SameLine();
        if (ImGui.Button(">>"))    _sim.Clock.WarpUp();

        ImGui.Spacing();
        string camMode = _camera.CurrentMode == Core.Camera.Mode.Orbit
            ? "Orbit cam  [C to switch]  LMB drag - rotate  Scroll - zoom"
            : "Free cam   [C to switch]  RMB drag - look    WASD - move  QE - up/dn";
        ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), camMode);

        ImGui.End();

        // Satellite telemetry panel
        if (_sim.Orbitals.Count > 0)
        {
            var body = _sim.Orbitals[0];

            ImGui.SetNextWindowPos(new Vector2(10, 130), ImGuiCond.Always);
            ImGui.SetNextWindowBgAlpha(0.6f);
            ImGui.Begin("##telem",
                ImGuiWindowFlags.NoTitleBar  |
                ImGuiWindowFlags.NoResize    |
                ImGuiWindowFlags.NoMove      |
                ImGuiWindowFlags.AlwaysAutoResize);

            ImGui.TextColored(new Vector4(0.91f, 0.30f, 0.24f, 1f), body.Name);
            ImGui.Separator();
            ImGui.Text($"Altitude  {body.Altitude/1000:F1} km");
            ImGui.Text($"Speed     {body.Speed:F0} m/s");
            ImGui.Text($"Period    {body.Period/60:F1} min");
            ImGui.Text($"Pe        {(body.Elements.Periapsis - body.Primary.Radius)/1000:F1} km");
            ImGui.Text($"Ap        {(body.Elements.Apoapsis  - body.Primary.Radius)/1000:F1} km");
            ImGui.Text($"Ecc       {body.Eccentricity:F4}");
            ImGui.Text($"Inc       {body.InclinationDeg:F1} deg");
            ImGui.Text($"Mode      {_camera.CurrentMode}  [C]");

            ImGui.End();
        }

        _maneuverWindow.Draw();
        _contextMenu.Draw();

        // Update predicted orbit visualization
        if (_sim != null && _maneuverWindow.IsOpen && _maneuverWindow.Target != null)
            _scene?.SetPredictedOrbit(_maneuverWindow.PredictedElements,
                _maneuverWindow.Target);
        else
            _scene?.SetPredictedOrbit(null, _sim?.Orbitals.Count > 0
                ? _sim.Orbitals[0] : null!);

        _orbitEditor.Draw();
    }

    private void InitSimulation()
    {
        if (_startupMenu.ResultPlanet == null ||
            _startupMenu.ResultSatellite == null) return;

        _sim = new OrbitSimulation();
        _sim.AddCelestial(_startupMenu.ResultPlanet);
        _sim.AddOrbital(_startupMenu.ResultSatellite);
        _sim.Clock.SetWarp(1);

        float refRadius = (float)_startupMenu.ResultPlanet.Radius;
        _scene = new SceneRenderer(_gl!, refRadius);
        _scene.BuildScene(_sim);

        _maneuverWindow.SetSimulation(_sim);

        _camera.FocusOnRadius(1f);
        _lastTick = DateTime.UtcNow;
    }

    private void OnClose()
    {
        _scene?.Dispose();
        _imgui?.Dispose();
    }
}