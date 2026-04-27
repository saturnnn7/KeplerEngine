using System;
using System.Collections.Generic;
using Silk.NET.OpenGL;
using KeplerEngine.Core;
using KeplerEngine.Physics;
using KeplerEngine.Orbital;

namespace KeplerEngine.Renderer3D.Rendering;

/// <summary>Generating geometry for orbits, spheres, and axes.</summary>
public static class MeshFactory
{
    /// <summary>Orbital path—256 segments along an ellipse.</summary>
    public static float[] OrbitLine(OrbitalBody body, float scale, int steps = 256)
    {
        var pts  = body.GetOrbitPoints(steps);
        var verts = new List<float>(steps * 6);

        for (int i = 0; i < pts.Length; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % pts.Length];

            verts.Add((float)(a.X * scale));
            verts.Add((float)(a.Z * scale));   // Z Up → Y in OpenGL
            verts.Add((float)(a.Y * scale));

            verts.Add((float)(b.X * scale));
            verts.Add((float)(b.Z * scale));
            verts.Add((float)(b.Y * scale));
        }

        return verts.ToArray();
    }

    /// <summary>Wireframe sphere (three circles aligned along the axes).</summary>
    public static float[] WireframeSphere(float radius, int steps = 64)
    {
        var v = new List<float>();

        void Circle(Func<float, (float, float, float)> fn)
        {
            for (int i = 0; i < steps; i++)
            {
                float a0 = i       / (float)steps * MathF.PI * 2;
                float a1 = (i + 1) / (float)steps * MathF.PI * 2;
                var (x0, y0, z0) = fn(a0);
                var (x1, y1, z1) = fn(a1);
                v.AddRange(new[]{ x0, y0, z0, x1, y1, z1 });
            }
        }

        Circle(a => (radius * MathF.Cos(a), radius * MathF.Sin(a), 0));   // XY
        Circle(a => (radius * MathF.Cos(a), 0, radius * MathF.Sin(a)));   // XZ
        Circle(a => (0, radius * MathF.Cos(a), radius * MathF.Sin(a)));   // YZ

        return v.ToArray();
    }

    /// <summary>The X (red), Y (green), and Z (blue) coordinate axes are stored in a single buffer.</summary>
    public static float[] AxesLines(float len = 1f) => new[]
    {
        // X
        0f, 0f, 0f,   len, 0f,  0f,
        // Y
        0f, 0f, 0f,   0f,  len, 0f,
        // Z
        0f, 0f, 0f,   0f,  0f,  len
    };

    /// <summary>Ecliptic grid (XZ plane).</summary>
    public static float[] GridLines(float size, int divisions)
    {
        var v    = new List<float>();
        float step = size * 2 / divisions;

        for (int i = 0; i <= divisions; i++)
        {
            float t = -size + i * step;
            v.AddRange(new[]{ t, 0f, -size,  t, 0f,  size });
            v.AddRange(new[]{ -size, 0f, t,   size, 0f, t  });
        }

        return v.ToArray();
    }

    /// <summary>The small cross marks a point in orbit.</summary>
    public static float[] CrossMarker(float size = 0.05f) => new[]
    {
        -size, 0f, 0f,   size, 0f, 0f,
         0f, -size, 0f,  0f,  size, 0f,
         0f, 0f, -size,  0f,  0f,  size
    };
}