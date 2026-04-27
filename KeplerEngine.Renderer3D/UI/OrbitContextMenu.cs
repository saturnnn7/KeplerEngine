using System;
using ImGuiNET;
using System.Numerics;

namespace KeplerEngine.Renderer3D.UI;

public class OrbitContextMenu
{
    public bool  IsOpen       { get; private set; } = false;
    public float ClickedNuDeg { get; private set; } = 0f;

    private bool _pendingOpen = false;

    public Action? OnWarpTo      { get; set; }
    public Action? OnAddManeuver { get; set; }

    // Called from mouse handler — just sets a flag, no ImGui calls
    public void Open(float nuDeg)
    {
        ClickedNuDeg = nuDeg;
        _pendingOpen = true;
        IsOpen       = true;
    }

    // Called every frame inside ImGui frame
    public void Draw()
    {
        if (!IsOpen) return;

        // Open popup on the first frame after flag is set
        if (_pendingOpen)
        {
            ImGui.OpenPopup("##orbitctx");
            _pendingOpen = false;
        }

        if (ImGui.BeginPopup("##orbitctx"))
        {
            ImGui.TextColored(new Vector4(0.9f, 0.7f, 0.2f, 1f),
                $"Orbit point  nu = {ClickedNuDeg:F1} deg");
            ImGui.Separator();
            ImGui.Spacing();

            if (ImGui.MenuItem("Warp to this point"))
            {
                OnWarpTo?.Invoke();
                IsOpen = false;
                ImGui.CloseCurrentPopup();
            }

            if (ImGui.MenuItem("Add maneuver node here"))
            {
                OnAddManeuver?.Invoke();
                IsOpen = false;
                ImGui.CloseCurrentPopup();
            }

            ImGui.Spacing();
            if (ImGui.MenuItem("Cancel"))
            {
                IsOpen = false;
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
        else
        {
            // Popup was closed by clicking outside
            IsOpen = false;
        }
    }
}