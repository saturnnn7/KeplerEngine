using System;
using ImGuiNET;
using System.Numerics;
using KeplerEngine.Physics;
using KeplerEngine.Orbital;
using KeplerEngine.Simulation;
using KeplerEngine.Core;

namespace KeplerEngine.Renderer3D.UI;

public class StartupMenu
{
    public bool IsOpen { get; private set; } = true;
    public bool IsDone { get; private set; } = false;

    // UI state
    private int    _step         = 0;
    private int    _planetPreset = 0;

    // Planet params
    private string _planetName = "Kerbin";
    private float  _radiusKm   = 600f;
    private float  _surfaceG   = 9.81f;

    // Satellite params
    private string _satName   = "Probe-1";
    private int    _orbitMode = 0;
    private float  _altKm     = 100f;
    private float  _ecc       = 0f;
    private float  _incDeg    = 0f;
    private float  _lanDeg    = 0f;
    private float  _argPeDeg  = 0f;
    private float  _nuDeg     = 0f;

    public CelestialBody? ResultPlanet    { get; private set; }
    public OrbitalBody?   ResultSatellite { get; private set; }

    private static readonly string[] PlanetPresets = { "Kerbin", "Earth", "Custom" };
    private static readonly string[] OrbitModes    = { "Circular (altitude)", "Full Keplerian" };

    public void Draw()
    {
        if (!IsOpen) return;

        var display = ImGui.GetIO().DisplaySize;
        ImGui.SetNextWindowPos(display / 2f, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(480, 0), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0.96f);

        ImGui.Begin("##startup",
            ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoResize   |
            ImGuiWindowFlags.NoMove);

        // Header
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.18f, 0.80f, 0.44f, 1f));
        CenterText("KEPLER ENGINE");
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.6f, 1f));
        CenterText("Orbital Simulation");
        ImGui.PopStyleColor();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (_step == 0) DrawPlanetStep();
        else            DrawSatelliteStep();

        ImGui.End();
    }

    // ── Step 1: Planet ────────────────────────────────────────────────────────

    private void DrawPlanetStep()
    {
        ImGui.TextColored(new Vector4(0.9f, 0.7f, 0.2f, 1f), "Step 1 / 2 -- Create Planet");
        ImGui.Spacing();

        ImGui.Text("Preset:");
        ImGui.SameLine();
        if (ImGui.Combo("##preset", ref _planetPreset, PlanetPresets, PlanetPresets.Length))
            ApplyPlanetPreset();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        Row("Name");
        ImGui.SameLine(140); ImGui.SetNextItemWidth(220);
        ImGui.InputText("##pname", ref _planetName, 64);

        Row("Radius (km)");
        ImGui.SameLine(140); ImGui.SetNextItemWidth(220);
        ImGui.DragFloat("##radius", ref _radiusKm, 1f, 10f, 100_000f, "%.1f km");

        Row("Surface g");
        ImGui.SameLine(140); ImGui.SetNextItemWidth(220);
        ImGui.DragFloat("##surfg", ref _surfaceG, 0.01f, 0.1f, 50f, "%.2f m/s2");

        // Derived
        double mu   = (double)_surfaceG * ((double)_radiusKm * 1000) * ((double)_radiusKm * 1000);
        double mass = mu / 6.674e-11;
        ImGui.Spacing();
        ImGui.TextColored(new Vector4(0.5f, 0.8f, 1f, 1f),
            $"  mu = {mu:E3} m3/s2     Mass = {mass:E3} kg");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.SetCursorPosX((ImGui.GetWindowWidth() - 220f) / 2f);
        if (ImGui.Button("Next -- Create Satellite >>", new Vector2(220, 32)))
        {
            ResultPlanet = CelestialBody.FromSurfaceGravityAndRadius(
                _planetName, _surfaceG, _radiusKm * 1000.0);
            _step = 1;
        }
    }

    // ── Step 2: Satellite ─────────────────────────────────────────────────────

    private void DrawSatelliteStep()
    {
        ImGui.TextColored(new Vector4(0.9f, 0.7f, 0.2f, 1f), "Step 2 / 2 -- Create Satellite");
        ImGui.Spacing();

        Row("Name");
        ImGui.SameLine(160); ImGui.SetNextItemWidth(200);
        ImGui.InputText("##sname", ref _satName, 64);

        ImGui.Spacing();
        Row("Orbit type");
        ImGui.SameLine(160);
        ImGui.Combo("##omode", ref _orbitMode, OrbitModes, OrbitModes.Length);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (_orbitMode == 0) DrawCircularInputs();
        else                 DrawKeplerianInputs();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        float btnW = 140f;
        ImGui.SetCursorPosX((ImGui.GetWindowWidth() - btnW * 2 - 16f) / 2f);

        if (ImGui.Button("<< Back", new Vector2(btnW, 32))) _step = 0;
        ImGui.SameLine(0, 16);
        if (ImGui.Button("Launch Simulation >>", new Vector2(btnW, 32))) Confirm();
    }

    private void DrawCircularInputs()
    {
        Row("Altitude (km)");
        ImGui.SameLine(180); ImGui.SetNextItemWidth(160);
        ImGui.DragFloat("##alt", ref _altKm, 1f, 1f, 500_000f, "%.1f km");

        Row("Inclination");
        ImGui.SameLine(180); ImGui.SetNextItemWidth(160);
        ImGui.SliderFloat("##inc0", ref _incDeg, 0f, 180f, "%.1f deg");

        if (ResultPlanet != null)
        {
            double a = ResultPlanet.Radius + _altKm * 1000.0;
            double v = Math.Sqrt(ResultPlanet.Mu / a);
            double T = OrbitalMath.OrbitalPeriod(a, ResultPlanet.Mu);
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.5f, 0.8f, 1f, 1f),
                $"  v = {v:F0} m/s     T = {T/60:F1} min");
        }
    }

    private void DrawKeplerianInputs()
    {
        DragRow("Semi-major axis (km)", ref _altKm,    1f,     100f,  1_000_000f, "%.1f");
        DragRow("Eccentricity",         ref _ecc,      0.001f, 0f,    0.99f,      "%.4f");
        DragRow("Inclination (deg)",    ref _incDeg,   0.1f,   0f,    180f,       "%.1f");
        DragRow("LAN Omega (deg)",      ref _lanDeg,   0.1f,   0f,    360f,       "%.1f");
        DragRow("Arg Periapsis (deg)",  ref _argPeDeg, 0.1f,   0f,    360f,       "%.1f");
        DragRow("True Anomaly (deg)",   ref _nuDeg,    0.1f,   0f,    360f,       "%.1f");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void ApplyPlanetPreset()
    {
        switch (_planetPreset)
        {
            case 0: _planetName = "Kerbin"; _radiusKm = 600f;  _surfaceG = 9.81f;  break;
            case 1: _planetName = "Earth";  _radiusKm = 6371f; _surfaceG = 9.807f; break;
            case 2: break;
        }
    }

    private void Confirm()
    {
        if (ResultPlanet == null) return;

        KeplerianElements el;
        if (_orbitMode == 0)
        {
            el = KeplerianElements.CircularOrbit(
                _altKm * 1000.0,
                ResultPlanet.Radius,
                OrbitalMath.DegToRad(_incDeg));
        }
        else
        {
            el = new KeplerianElements(
                _altKm * 1000.0,
                _ecc,
                OrbitalMath.DegToRad(_incDeg),
                OrbitalMath.DegToRad(_lanDeg),
                OrbitalMath.DegToRad(_argPeDeg),
                OrbitalMath.DegToRad(_nuDeg));
        }

        ResultSatellite = new OrbitalBody(_satName, ResultPlanet, el);
        IsOpen = false;
        IsDone = true;
    }

    private static void Row(string label) => ImGui.Text(label);

    private static void DragRow(string label, ref float val,
        float speed, float min, float max, string fmt)
    {
        ImGui.Text(label);
        ImGui.SameLine(210); ImGui.SetNextItemWidth(160);
        ImGui.DragFloat($"##{label}", ref val, speed, min, max, fmt);
    }

    private static void CenterText(string text)
    {
        float w = ImGui.CalcTextSize(text).X;
        ImGui.SetCursorPosX((ImGui.GetWindowWidth() - w) / 2f);
        ImGui.Text(text);
    }
}