using System;
using ImGuiNET;
using System.Numerics;
using KeplerEngine.Physics;
using KeplerEngine.Orbital;
using KeplerEngine.Core;
using KeplerEngine.Simulation;

namespace KeplerEngine.Renderer3D.UI;

/// <summary>
/// Maneuver planner window.
/// Opened by clicking a point on the orbit line.
/// Shows predicted orbit after delta-v.
/// Execute button warps to T-20s then applies delta-v.
/// </summary>
public class ManeuverWindow
{
    public bool        IsOpen        { get; set; } = false;
    public OrbitalBody? Target       { get; set; }

    // Maneuver node true anomaly (where on orbit the burn happens)
    public float ManeuverNuDeg { get; set; } = 0f;

    // Delta-v components in m/s
    public double DvPrograde { get; private set; } = 0;
    public double DvNormal   { get; private set; } = 0;
    public double DvRadial   { get; private set; } = 0;

    // Predicted orbit after maneuver
    public KeplerianElements? PredictedElements { get; private set; }

    // Execution state
    public bool  IsExecuting     { get; private set; } = false;
    public bool  ExecuteReady    { get; private set; } = false;

    private float _step = 1f;
    private static readonly float[]  Steps      = { 0.01f, 0.1f, 1f, 10f, 100f };
    private static readonly string[] StepLabels = { "0.01", "0.1", "1", "10", "100" };

    private OrbitSimulation? _sim;

    public void SetSimulation(OrbitSimulation sim) => _sim = sim;

    public void Draw()
    {
        if (!IsOpen || Target == null) return;

        ImGui.SetNextWindowSize(new Vector2(420, 0), ImGuiCond.Once);
        ImGui.SetNextWindowPos(
            new Vector2(ImGui.GetIO().DisplaySize.X - 420, 10),
            ImGuiCond.Once);
        ImGui.SetNextWindowBgAlpha(0.92f);

        bool open = IsOpen;
        ImGui.Begin($"Maneuver Node -- {Target.Name}##mnv", ref open);
        IsOpen = open;

        if (Target == null) { ImGui.End(); return; }

        // Maneuver position info
        ImGui.TextColored(new Vector4(0.9f, 0.7f, 0.2f, 1f), "Maneuver Node");
        ImGui.Separator();
        ImGui.Text($"Position   nu = {ManeuverNuDeg:F1} deg");

        // Time to node
        if (_sim != null)
        {
            double nuRad = OrbitalMath.DegToRad(ManeuverNuDeg);
            double timeToNode = KeplerPropagator.TimeToTrueAnomaly(
                Target, nuRad, _sim.Clock.UT);
            ImGui.Text($"Time to node   {FormatTime(timeToNode - _sim.Clock.UT)}");
        }

        ImGui.Spacing();

        // Step selector
        ImGui.Text("Step (m/s):");
        ImGui.SameLine();
        for (int i = 0; i < Steps.Length; i++)
        {
            bool active = MathF.Abs(_step - Steps[i]) < 1e-6f;
            if (active)
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.18f, 0.60f, 0.34f, 1f));
            if (ImGui.Button(StepLabels[i])) { _step = Steps[i]; UpdatePrediction(); }
            if (active) ImGui.PopStyleColor();
            if (i < Steps.Length - 1) ImGui.SameLine();
        }

        ImGui.Spacing();
        ImGui.TextColored(new Vector4(0.5f,0.5f,0.5f,1f), "LMB = +step    RMB = -step");
        ImGui.Separator();
        ImGui.Spacing();

        // Delta-v inputs
        ImGui.TextColored(new Vector4(0.9f, 0.7f, 0.2f, 1f), "Delta-V (m/s)");
        ImGui.Spacing();

        if (DvRow("Prograde",  ref _dvPro)) UpdatePrediction();
        if (DvRow("Normal",    ref _dvNrm)) UpdatePrediction();
        if (DvRow("Radial",    ref _dvRad)) UpdatePrediction();

        DvPrograde = _dvPro;
        DvNormal   = _dvNrm;
        DvRadial   = _dvRad;

        // Total dv
        double totalDv = Math.Sqrt(_dvPro*_dvPro + _dvNrm*_dvNrm + _dvRad*_dvRad);
        ImGui.Spacing();
        ImGui.TextColored(new Vector4(0.7f, 0.9f, 0.7f, 1f),
            $"Total dV = {totalDv:F2} m/s");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Predicted orbit info
        if (PredictedElements != null)
        {
            ImGui.TextColored(new Vector4(0.9f, 0.7f, 0.2f, 1f), "Predicted Orbit");
            ImGui.Spacing();
            double pe = (PredictedElements.Periapsis  - Target.Primary.Radius) / 1000.0;
            double ap = (PredictedElements.Apoapsis   - Target.Primary.Radius) / 1000.0;
            double T  =  OrbitalMath.OrbitalPeriod(
                PredictedElements.SemiMajorAxis, Target.Primary.Mu) / 60.0;
            TelRow("Pe",     $"{pe:F1} km");
            TelRow("Ap",     $"{ap:F1} km");
            TelRow("Period", $"{T:F1} min");
            TelRow("Ecc",    $"{PredictedElements.Eccentricity:F4}");
            TelRow("Inc",    $"{PredictedElements.InclinationDeg:F1} deg");
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Execute button
        ImGui.SetCursorPosX((ImGui.GetWindowWidth() - 260f) / 2f);
        ImGui.PushStyleColor(ImGuiCol.Button,
            new Vector4(0.78f, 0.15f, 0.15f, 1f));

        if (ImGui.Button("Execute Maneuver (warp to T-20s)", new Vector2(260, 32)))
        {
            IsExecuting  = false;
            ExecuteReady = true;
        }

        ImGui.PopStyleColor();

        if (IsExecuting)
        {
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.9f, 0.3f, 0.3f, 1f), "Warping to maneuver...");
        }

        // Reset button
        ImGui.SameLine(0, 8);
        if (ImGui.Button("Reset"))
        {
            _dvPro = 0; _dvNrm = 0; _dvRad = 0;
            UpdatePrediction();
        }

        ImGui.End();
    }

    // ── Internal dv fields ────────────────────────────────────────────────────
    private double _dvPro = 0;
    private double _dvNrm = 0;
    private double _dvRad = 0;

    // ── Prediction ────────────────────────────────────────────────────────────

    public void UpdatePrediction()
    {
        if (Target == null) return;

        var snapEl = Target.Elements.Clone();
        snapEl.TrueAnomaly = OrbitalMath.DegToRad(ManeuverNuDeg);
        var sv = StateVector.FromKeplerian(snapEl, Target.Primary.Mu);

        double vMag = sv.Velocity.Magnitude;
        double rMag = sv.Position.Magnitude;
        if (vMag < 1e-10 || rMag < 1e-10) return;

        // Prograde — unit vector along velocity
        var prograde = sv.Velocity * (1.0 / vMag);

        // Radial — unit vector away from central body
        var radial = sv.Position * (1.0 / rMag);

        // Normal — perpendicular to orbital plane
        // Must be computed as r × v (specific angular momentum direction)
        // then normalized. This is perpendicular to BOTH prograde and radial.
        var hVec = Vector3d.Cross(sv.Position, sv.Velocity);
        double hMag = hVec.Magnitude;

        Console.WriteLine($"hVec raw = {hVec}  |h|={hMag:E4}");

        Console.WriteLine($"hVec.X={hVec.X:E4}  hVec.Y={hVec.Y:E4}  hVec.Z={hVec.Z:E4}");

        Vector3d normal;
        if (hMag > 1.0)   // h has units of m²/s, should be large
        {
            normal = hVec * (1.0 / hMag);
        }
        else
        {
            // Degenerate — use Z axis
            normal = Vector3d.UnitZ;
        }

        Console.WriteLine($"normal.X={normal.X:E4}  normal.Y={normal.Y:E4}  normal.Z={normal.Z:E4}");

        // Verify orthogonality — normal must be perpendicular to prograde
        // If not, reorthogonalize
        double dot = Vector3d.Dot(normal, prograde);
        if (Math.Abs(dot) > 1e-6)
        {
            // Gram-Schmidt reorthogonalization
            var normalOrtho = normal - prograde * dot;
            double noMag = normalOrtho.Magnitude;
            if (noMag > 1e-10) normal = normalOrtho * (1.0 / noMag);
        }

        Console.WriteLine($"prograde={prograde}  |p|={prograde.Magnitude:F6}");
        Console.WriteLine($"normal  ={normal}    |n|={normal.Magnitude:F6}");
        Console.WriteLine($"radial  ={radial}    |r|={radial.Magnitude:F6}");
        Console.WriteLine($"p·n={Vector3d.Dot(prograde,normal):E2}  p·r={Vector3d.Dot(prograde,radial):E2}  n·r={Vector3d.Dot(normal,radial):E2}");

        // Apply delta-v
        var dv    = prograde * _dvPro + normal * _dvNrm + radial * _dvRad;
        var newSv = new StateVector(sv.Position, sv.Velocity + dv);

        try
        {
            PredictedElements = StateVector.ToKeplerian(newSv, Target.Primary.Mu);

            Console.WriteLine($"Pe={( PredictedElements.Periapsis - Target.Primary.Radius)/1000:F1} km  " +
                              $"Ap={(PredictedElements.Apoapsis  - Target.Primary.Radius)/1000:F1} km  " +
                              $"inc={PredictedElements.InclinationDeg:F2} deg");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ToKeplerian error: {ex.Message}");
            PredictedElements = null;
        }
    }

    public void ResetExecution()
    {
        IsExecuting  = false;
        ExecuteReady = false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool DvRow(string label, ref double val)
    {
        bool changed = false;
        ImGui.Text($"{label,-12}");
        ImGui.SameLine();

        ImGui.PushID(label + "dn");
        if (ImGui.Button("-"))
            { val -= _step; changed = true; }
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            { val -= _step; changed = true; }
        ImGui.PopID();

        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        if (ImGui.InputDouble($"##{label}dv", ref val, 0, 0, "%.2f",
            ImGuiInputTextFlags.EnterReturnsTrue))
            changed = true;

        ImGui.SameLine();
        ImGui.PushID(label + "up");
        if (ImGui.Button("+"))
            { val += _step; changed = true; }
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            { val -= _step; changed = true; }
        ImGui.PopID();

        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.5f, 0.8f, 1f, 1f), "m/s");
        return changed;
    }

    private static void TelRow(string label, string value)
    {
        ImGui.Text($"{label,-10}");
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.7f, 0.9f, 0.7f, 1f), value);
    }

    private static string FormatTime(double seconds)
    {
        if (seconds < 0) return "passed";
        int h = (int)(seconds / 3600);
        int m = (int)(seconds % 3600 / 60);
        int s = (int)(seconds % 60);
        return $"{h:D2}:{m:D2}:{s:D2}";
    }
}