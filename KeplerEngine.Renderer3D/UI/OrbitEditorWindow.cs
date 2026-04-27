using System;
using ImGuiNET;
using System.Numerics;
using KeplerEngine.Physics;
using KeplerEngine.Core;

namespace KeplerEngine.Renderer3D.UI;

/// <summary>
/// Orbit editor panel — opens when user clicks on a satellite.
/// Allows editing all Keplerian elements and current speed.
/// Step buttons: 0.01 / 0.1 / 1 / 10  —  LMB = add, RMB = subtract.
/// </summary>
public class OrbitEditorWindow
{
    public bool        IsOpen { get; set; } = false;
    public OrbitalBody? Target { get; set; }

    // Active step size for increment/decrement buttons
    private float _step = 1f;

    private static readonly float[] Steps     = { 0.01f, 0.1f, 1f, 10f };
    private static readonly string[] StepLabels = { "0.01", "0.1", "1", "10" };

    public void Draw()
    {
        if (!IsOpen || Target == null) return;

        ImGui.SetNextWindowSize(new Vector2(400, 0), ImGuiCond.Once);
        ImGui.SetNextWindowPos(new Vector2(ImGui.GetIO().DisplaySize.X - 420, 10),
            ImGuiCond.Once);
        ImGui.SetNextWindowBgAlpha(0.92f);

        bool open = IsOpen;
        ImGui.Begin($"Orbit Editor -- {Target.Name}##editor", ref open);
        IsOpen = open;

        if (Target == null) { ImGui.End(); return; }

        // Step selector
        ImGui.Text("Step size:");
        ImGui.SameLine();
        for (int i = 0; i < Steps.Length; i++)
        {
            bool active = MathF.Abs(_step - Steps[i]) < 1e-6f;
            if (active)
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.18f, 0.60f, 0.34f, 1f));

            if (ImGui.Button(StepLabels[i]))
                _step = Steps[i];

            if (active)
                ImGui.PopStyleColor();

            if (i < Steps.Length - 1) ImGui.SameLine();
        }

        ImGui.Spacing();
        ImGui.TextColored(new Vector4(0.5f,0.5f,0.5f,1f),
            "LMB = +step    RMB = -step");
        ImGui.Separator();
        ImGui.Spacing();

        // ── Keplerian Elements ─────────────────────────────────────────────
        ImGui.TextColored(new Vector4(0.9f, 0.7f, 0.2f, 1f), "Keplerian Elements");
        ImGui.Spacing();

        double valA   = Target.Elements.SemiMajorAxis / 1000.0;
        double valE   = Target.Elements.Eccentricity;
        double valInc = Target.Elements.InclinationDeg;
        double valLan = Target.Elements.LANDeg;
        double valW   = Target.Elements.ArgumentOfPeriapsisDeg;
        double valNu  = Target.Elements.TrueAnomalyDeg;
        
        if (ElementRow("Semi-major axis", ref valA,   "km",  100, 1_000_000))
            Target.Elements.SemiMajorAxis = valA * 1000.0;
        
        if (ElementRow("Eccentricity",    ref valE,   "",    0,   0.99))
            Target.Elements.Eccentricity = valE;
        
        if (ElementRow("Inclination",     ref valInc, "deg", 0,   180))
            Target.Elements.InclinationDeg = valInc;
        
        if (ElementRow("LAN (Omega)",     ref valLan, "deg", 0,   360))
            Target.Elements.LANDeg = valLan;
        
        if (ElementRow("Arg Periapsis",   ref valW,   "deg", 0,   360))
            Target.Elements.ArgumentOfPeriapsisDeg = valW;
        
        if (ElementRow("True Anomaly",    ref valNu,  "deg", 0,   360))
            Target.Elements.TrueAnomalyDeg = valNu;

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // ── Velocity edit ──────────────────────────────────────────────────
        ImGui.TextColored(new Vector4(0.9f, 0.7f, 0.2f, 1f), "Velocity (instant delta-v)");
        ImGui.Spacing();

        double dv = 0;
        if (VelocityRow("Prograde",  out dv)) Target.ApplyDeltaV(dv, 0, 0);
        if (VelocityRow("Normal",    out dv)) Target.ApplyDeltaV(0, dv, 0);
        if (VelocityRow("Radial",    out dv)) Target.ApplyDeltaV(0, 0, dv);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // ── Read-only telemetry ────────────────────────────────────────────
        ImGui.TextColored(new Vector4(0.9f, 0.7f, 0.2f, 1f), "Derived (read-only)");
        ImGui.Spacing();

        TelRow("Altitude",  $"{Target.Altitude/1000:F2} km");
        TelRow("Speed",     $"{Target.Speed:F2} m/s");
        TelRow("Period",    $"{Target.Period/60:F2} min");
        TelRow("Periapsis", $"{(Target.Elements.Periapsis - Target.Primary.Radius)/1000:F2} km");
        TelRow("Apoapsis",  $"{(Target.Elements.Apoapsis  - Target.Primary.Radius)/1000:F2} km");

        ImGui.End();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// One element row: label | [-] value [+] | unit
    /// Returns true if value was changed.
    /// </summary>
    private bool ElementRow(string label, ref double val, string unit,
        double min, double max)
    {
        bool changed = false;
        ImGui.Text($"{label,-18}");
        ImGui.SameLine();

        // LMB = +step, RMB = -step  on [-] button
        ImGui.PushID(label + "dn");
        if (ImGui.Button("-"))          { val = Math.Clamp(val - _step, min, max); changed = true; }
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                                        { val = Math.Clamp(val - _step, min, max); changed = true; }
        ImGui.PopID();

        ImGui.SameLine();
        ImGui.SetNextItemWidth(110);
        double tmp = val;
        if (ImGui.InputDouble($"##{label}", ref tmp, 0, 0, "%.4f",
            ImGuiInputTextFlags.EnterReturnsTrue))
        { val = Math.Clamp(tmp, min, max); changed = true; }

        ImGui.SameLine();
        ImGui.PushID(label + "up");
        if (ImGui.Button("+"))          { val = Math.Clamp(val + _step, min, max); changed = true; }
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                                        { val = Math.Clamp(val - _step, min, max); changed = true; }
        ImGui.PopID();

        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.5f,0.8f,1f,1f), unit);
        return changed;
    }

    /// <summary>
    /// Velocity delta row — returns delta value if button pressed.
    /// LMB on [+] = +step m/s,  RMB on [+] = -step m/s.
    /// </summary>
    private bool VelocityRow(string label, out double delta)
    {
        delta = 0;
        ImGui.Text($"{label,-18}");
        ImGui.SameLine();

        ImGui.PushID(label + "vdn");
        if (ImGui.Button("-"))
            { delta = -_step; ImGui.PopID(); return true; }
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            { delta = -_step; ImGui.PopID(); return true; }
        ImGui.PopID();

        ImGui.SameLine();
        ImGui.Text($"{Target!.Speed,10:F2} m/s");
        ImGui.SameLine();

        ImGui.PushID(label + "vup");
        if (ImGui.Button("+"))
            { delta = +_step; ImGui.PopID(); return true; }
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            { delta = -_step; ImGui.PopID(); return true; }
        ImGui.PopID();

        return false;
    }

    private static void TelRow(string label, string value)
    {
        ImGui.Text($"{label,-18}");
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.7f, 0.9f, 0.7f, 1f), value);
    }
}