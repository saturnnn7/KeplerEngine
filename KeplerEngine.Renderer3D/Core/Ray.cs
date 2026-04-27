using System;

namespace KeplerEngine.Renderer3D.Core;

/// <summary>
/// Ray in 3D space. Used for mouse picking.
/// origin + direction * t = point on ray
/// </summary>
public readonly struct Ray
{
    public readonly float[] Origin;
    public readonly float[] Direction;

    public Ray(float[] origin, float[] direction)
    {
        Origin    = origin;
        Direction = MathHelper.Normalize(direction);
    }

    /// <summary>
    /// Ray vs sphere intersection.
    /// Returns true if the ray hits the sphere, outputs distance t.
    /// </summary>
    public bool IntersectsSphere(float[] center, float radius, out float t)
    {
        t = 0f;
        float[] oc = MathHelper.Sub(Origin, center);

        float a = MathHelper.Dot(Direction, Direction);
        float b = 2f * MathHelper.Dot(oc, Direction);
        float c = MathHelper.Dot(oc, oc) - radius * radius;
        float d = b * b - 4f * a * c;

        if (d < 0) return false;

        t = (-b - MathF.Sqrt(d)) / (2f * a);
        if (t < 0) t = (-b + MathF.Sqrt(d)) / (2f * a);
        return t >= 0;
    }

    /// <summary>
    /// Build a ray from mouse screen coordinates.
    /// </summary>
    public static Ray FromScreenPoint(
        float mouseX, float mouseY,
        int screenW,  int screenH,
        float[] view, float[] proj)
    {
        // Normalized device coordinates
        float ndcX = (2f * mouseX / screenW) - 1f;
        float ndcY = 1f - (2f * mouseY / screenH);

        // Clip space
        float[] clip = { ndcX, ndcY, -1f, 1f };

        // Inverse projection -> view space
        float[] invProj = Invert4x4(proj);
        float[] viewRay = Transform4(invProj, clip);
        viewRay[2] = -1f; viewRay[3] = 0f;

        // Inverse view -> world space
        float[] invView = Invert4x4(view);
        float[] worldRay = Transform4(invView, viewRay);

        float[] dir = MathHelper.Normalize(new[]{ worldRay[0], worldRay[1], worldRay[2] });

        // Eye position from inverse view matrix
        float[] origin = new[]{ invView[12], invView[13], invView[14] };

        return new Ray(origin, dir);
    }

    // ── Matrix helpers ────────────────────────────────────────────────────────

    private static float[] Transform4(float[] m, float[] v)
    {
        return new[]
        {
            m[0]*v[0] + m[4]*v[1] + m[8] *v[2] + m[12]*v[3],
            m[1]*v[0] + m[5]*v[1] + m[9] *v[2] + m[13]*v[3],
            m[2]*v[0] + m[6]*v[1] + m[10]*v[2] + m[14]*v[3],
            m[3]*v[0] + m[7]*v[1] + m[11]*v[2] + m[15]*v[3],
        };
    }

    private static float[] Invert4x4(float[] m)
    {
        float[] inv = new float[16];

        inv[0]  =  m[5]*m[10]*m[15] - m[5]*m[11]*m[14] - m[9]*m[6]*m[15] + m[9]*m[7]*m[14] + m[13]*m[6]*m[11] - m[13]*m[7]*m[10];
        inv[4]  = -m[4]*m[10]*m[15] + m[4]*m[11]*m[14] + m[8]*m[6]*m[15] - m[8]*m[7]*m[14] - m[12]*m[6]*m[11] + m[12]*m[7]*m[10];
        inv[8]  =  m[4]*m[9] *m[15] - m[4]*m[11]*m[13] - m[8]*m[5]*m[15] + m[8]*m[7]*m[13] + m[12]*m[5]*m[11] - m[12]*m[7]*m[9];
        inv[12] = -m[4]*m[9] *m[14] + m[4]*m[10]*m[13] + m[8]*m[5]*m[14] - m[8]*m[6]*m[13] - m[12]*m[5]*m[10] + m[12]*m[6]*m[9];
        inv[1]  = -m[1]*m[10]*m[15] + m[1]*m[11]*m[14] + m[9]*m[2]*m[15] - m[9]*m[3]*m[14] - m[13]*m[2]*m[11] + m[13]*m[3]*m[10];
        inv[5]  =  m[0]*m[10]*m[15] - m[0]*m[11]*m[14] - m[8]*m[2]*m[15] + m[8]*m[3]*m[14] + m[12]*m[2]*m[11] - m[12]*m[3]*m[10];
        inv[9]  = -m[0]*m[9] *m[15] + m[0]*m[11]*m[13] + m[8]*m[1]*m[15] - m[8]*m[3]*m[13] - m[12]*m[1]*m[11] + m[12]*m[3]*m[9];
        inv[13] =  m[0]*m[9] *m[14] - m[0]*m[10]*m[13] - m[8]*m[1]*m[14] + m[8]*m[2]*m[13] + m[12]*m[1]*m[10] - m[12]*m[2]*m[9];
        inv[2]  =  m[1]*m[6] *m[15] - m[1]*m[7] *m[14] - m[5]*m[2]*m[15] + m[5]*m[3]*m[14] + m[13]*m[2]*m[7]  - m[13]*m[3]*m[6];
        inv[6]  = -m[0]*m[6] *m[15] + m[0]*m[7] *m[14] + m[4]*m[2]*m[15] - m[4]*m[3]*m[14] - m[12]*m[2]*m[7]  + m[12]*m[3]*m[6];
        inv[10] =  m[0]*m[5] *m[15] - m[0]*m[7] *m[13] - m[4]*m[1]*m[15] + m[4]*m[3]*m[13] + m[12]*m[1]*m[7]  - m[12]*m[3]*m[5];
        inv[14] = -m[0]*m[5] *m[14] + m[0]*m[6] *m[13] + m[4]*m[1]*m[14] - m[4]*m[2]*m[13] - m[12]*m[1]*m[6]  + m[12]*m[2]*m[5];
        inv[3]  = -m[1]*m[6] *m[11] + m[1]*m[7] *m[10] + m[5]*m[2]*m[11] - m[5]*m[3]*m[10] - m[9]*m[2]*m[7]   + m[9]*m[3]*m[6];
        inv[7]  =  m[0]*m[6] *m[11] - m[0]*m[7] *m[10] - m[4]*m[2]*m[11] + m[4]*m[3]*m[10] + m[8]*m[2]*m[7]   - m[8]*m[3]*m[6];
        inv[11] = -m[0]*m[5] *m[11] + m[0]*m[7] *m[9]  + m[4]*m[1]*m[11] - m[4]*m[3]*m[9]  - m[8]*m[1]*m[7]   + m[8]*m[3]*m[5];
        inv[15] =  m[0]*m[5] *m[10] - m[0]*m[6] *m[9]  - m[4]*m[1]*m[10] + m[4]*m[2]*m[9]  + m[8]*m[1]*m[6]   - m[8]*m[2]*m[5];

        float det = m[0]*inv[0] + m[1]*inv[4] + m[2]*inv[8] + m[3]*inv[12];
        if (MathF.Abs(det) < 1e-12f) return MathHelper.Identity();

        float invDet = 1f / det;
        for (int i = 0; i < 16; i++) inv[i] *= invDet;
        return inv;
    }

    /// <summary>
    /// Find closest point on orbit line to the ray.
    /// Returns true if within threshold, outputs true anomaly at that point.
    /// </summary>
    public bool IntersectsOrbitLine(
        KeplerEngine.Physics.OrbitalBody body,
        float scale, float threshold,
        out float closestNuDeg)
    {
        closestNuDeg = 0f;
        float minDist = float.MaxValue;
        bool  hit     = false;
    
        var points = body.GetOrbitPoints(256);
    
        for (int i = 0; i < points.Length; i++)
        {
            var p = points[i];
            float[] worldPt =
            {
                (float)p.X * scale,
                (float)p.Z * scale,
                (float)p.Y * scale
            };
    
            // Distance from point to ray
            float[] toPoint = MathHelper.Sub(worldPt, Origin);
            float   t       = MathHelper.Dot(toPoint, Direction);
            if (t < 0) continue;
    
            float[] closest = new[]
            {
                Origin[0] + Direction[0] * t,
                Origin[1] + Direction[1] * t,
                Origin[2] + Direction[2] * t,
            };
    
            float[] diff = MathHelper.Sub(worldPt, closest);
            float   dist = MathF.Sqrt(MathHelper.Dot(diff, diff));
    
            if (dist < threshold && dist < minDist)
            {
                minDist      = dist;
                // True anomaly proportional to index
                closestNuDeg = i / 256f * 360f;
                hit          = true;
            }
        }
    
        return hit;
    }
}