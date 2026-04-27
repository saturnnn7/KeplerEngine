using System;
using Silk.NET.Input;

namespace KeplerEngine.Renderer3D.Core;

/// <summary>
/// Camera with two modes:
///   Orbit — rotates around the target (left-click drag + scroll to zoom)
///   Free — flies around the scene (WASD + mouse, hold right-click)
/// Toggle: C key
/// </summary>
public class Camera
{
    public enum Mode { Orbit, Free }
    public Mode CurrentMode { get; private set; } = Mode.Orbit;

    // -- Orbit state ----------
    public float[] Target   = { 0f, 0f, 0f };
    public float   Distance = 3f;
    public float   Yaw      = 0f;       // radians
    public float   Pitch    = 0.4f;     // radians

    // -- Free state ----------
    public float[] FreePos  = { 0f, 1f, 5f };
    public float   FreeYaw  = 0f;
    public float   FreePitch= 0f;

    // -- Shared ----------
    public float Fov    = MathHelper.DegToRad(60f);
    public float Near   = 0.01f;
    public float Far    = 1_000_000f;

    // -- Input state ----------
    private bool  _orbitDrag;
    private bool  _freeLook;
    private float _lastX, _lastY;
    private bool  _firstMouse = true;

    // -- Free move ----------
    public float MoveSpeed  = 0.5f;
    public float LookSpeed  = 0.002f;

    // -- Matrices ----------
    public float[] GetView()
    {
        if (CurrentMode == Mode.Orbit)
        {
            float[] eye = GetOrbitEye();
            return MathHelper.LookAt(eye, Target, new[]{ 0f, 1f, 0f });
        }
        else
        {
            float[] dir = GetFreeDir();
            float[] center = new[]
            {
                FreePos[0] + dir[0],
                FreePos[1] + dir[1],
                FreePos[2] + dir[2]
            };
            return MathHelper.LookAt(FreePos, center, new[]{ 0f, 1f, 0f });
        }
    }

    public float[] GetProjection(float aspect)
        => MathHelper.Perspective(Fov, aspect, Near, Far);

    public float[] GetEyePosition()
        => CurrentMode == Mode.Orbit ? GetOrbitEye() : FreePos;

    // -- Input handlers ----------

    public void OnMouseDown(MouseButton btn, float x, float y)
    {
        if (CurrentMode == Mode.Orbit  && btn == MouseButton.Left)
        { _orbitDrag = true; _firstMouse = true; }

        if (CurrentMode == Mode.Free   && btn == MouseButton.Right)
        { _freeLook  = true; _firstMouse = true; }
    }

    public void OnMouseUp(MouseButton btn)
    {
        if (btn == MouseButton.Left)  _orbitDrag = false;
        if (btn == MouseButton.Right) _freeLook  = false;
    }

    public void OnMouseMove(float x, float y)
    {
        if (!_orbitDrag && !_freeLook) return;
        if (_firstMouse) { _lastX = x; _lastY = y; _firstMouse = false; return; }

        float dx = x - _lastX;
        float dy = y - _lastY;
        _lastX = x; _lastY = y;

        if (_orbitDrag)
        {
            Yaw   -= dx * 0.005f;
            Pitch += dy * 0.005f;
            Pitch  = Math.Clamp(Pitch, -MathHelper.Pi/2f + 0.05f, MathHelper.Pi/2f - 0.05f);
        }

        if (_freeLook)
        {
            FreeYaw   += dx * LookSpeed;
            FreePitch -= dy * LookSpeed;
            FreePitch  = Math.Clamp(FreePitch, -MathHelper.Pi/2f + 0.05f, MathHelper.Pi/2f - 0.05f);
        }
    }

    public void OnScroll(float delta)
    {
        if (CurrentMode == Mode.Orbit)
        {
            Distance *= delta > 0 ? 0.9f : 1.1f;
            Distance  = Math.Clamp(Distance, 0.1f, 100_000f);
        }
        else
        {
            MoveSpeed *= delta > 0 ? 1.2f : 0.8f;
            MoveSpeed  = Math.Clamp(MoveSpeed, 0.001f, 10000f);
        }
    }

    public void OnKeyDown(Key key)
    {
        if (key == Key.C) ToggleMode();
        if (key == Key.R) Reset();
    }

    public void UpdateFreeMove(IKeyboard kb, float dt)
    {
        if (CurrentMode != Mode.Free) return;

        float[] fwd   = GetFreeDir();
        float[] right = MathHelper.Normalize(MathHelper.Cross(fwd, new[]{ 0f, 1f, 0f }));
        float   spd   = MoveSpeed * dt * 60f;

        if (kb.IsKeyPressed(Key.W)) Move(fwd,    spd);
        if (kb.IsKeyPressed(Key.S)) Move(fwd,   -spd);
        if (kb.IsKeyPressed(Key.A)) Move(right, -spd);
        if (kb.IsKeyPressed(Key.D)) Move(right,  spd);
        if (kb.IsKeyPressed(Key.E)) FreePos[1] += spd;
        if (kb.IsKeyPressed(Key.Q)) FreePos[1] -= spd;
    }

    // -- Utilities ----------

    public void ToggleMode()
    {
        if (CurrentMode == Mode.Orbit)
        {
            // Переносим позицию из orbit в free
            FreePos     = GetOrbitEye();
            FreeYaw     = Yaw;
            FreePitch   = -Pitch;
            CurrentMode = Mode.Free;
        }
        else
        {
            CurrentMode = Mode.Orbit;
        }
    }

    public void Reset()
    {
        Yaw = 0; Pitch = 0.4f; Distance = 3f;
        Target = new[]{ 0f, 0f, 0f };
    }

    /// <summary>Set the distance so that an object with radius r is fully visible.</summary>
    public void FocusOnRadius(float radius)
    {
        Distance = radius * 3.5f;
        Near     = radius * 0.001f;
        Far      = radius * 1_000f;
    }

    private float[] GetOrbitEye()
    {
        float x = Target[0] + Distance * MathF.Cos(Pitch) * MathF.Sin(Yaw);
        float y = Target[1] + Distance * MathF.Sin(Pitch);
        float z = Target[2] + Distance * MathF.Cos(Pitch) * MathF.Cos(Yaw);
        return new[]{ x, y, z };
    }

    private float[] GetFreeDir()
    {
        float x =  MathF.Cos(FreePitch) * MathF.Sin(FreeYaw);
        float y =  MathF.Sin(FreePitch);
        float z = -MathF.Cos(FreePitch) * MathF.Cos(FreeYaw);
        return MathHelper.Normalize(new[]{ x, y, z });
    }

    private void Move(float[] dir, float spd)
    {
        FreePos[0] += dir[0] * spd;
        FreePos[1] += dir[1] * spd;
        FreePos[2] += dir[2] * spd;
    }
}