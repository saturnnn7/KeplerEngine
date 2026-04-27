using System;

namespace KeplerEngine.Renderer3D.Core;

/// <summary>
/// Float-based matrices and vectors for OpenGL.
/// OpenGL expects floats, so this is separate from the double-precision engine.
/// </summary>
public static class MathHelper
{
    public const float Pi = MathF.PI;

    public static float DegToRad(float deg) => deg * Pi / 180f;

    // -- Vec3 helpers ----------
    public static float[] Normalize(float[] v)
    {
        float len = MathF.Sqrt(v[0]*v[0] + v[1]*v[1] + v[2]*v[2]);
        if (len < 1e-12f) return new float[]{ 0, 0, 0 };
        return new[]{ v[0]/len, v[1]/len, v[2]/len };
    }

    public static float[] Cross(float[] a, float[] b) => new[]
    {
        a[1]*b[2] - a[2]*b[1],
        a[2]*b[0] - a[0]*b[2],
        a[0]*b[1] - a[1]*b[0]
    };

    public static float Dot(float[] a, float[] b)
        => a[0]*b[0] + a[1]*b[1] + a[2]*b[2];

    public static float[] Sub(float[] a, float[] b)
        => new[]{ a[0]-b[0], a[1]-b[1], a[2]-b[2] };

    // -- 4x4 matrices (column-major, like OpenGL) ----------

    public static float[] Identity() => new float[]
    {
        1,0,0,0,
        0,1,0,0,
        0,0,1,0,
        0,0,0,1
    };

    public static float[] Perspective(float fovRad, float aspect, float near, float far)
    {
        float f = 1f / MathF.Tan(fovRad / 2f);
        float[] m = new float[16];
        m[0]  =  f / aspect;
        m[5]  =  f;
        m[10] =  (far + near) / (near - far);
        m[11] = -1f;
        m[14] =  (2f * far * near) / (near - far);
        return m;
    }

    public static float[] LookAt(float[] eye, float[] center, float[] up)
    {
        float[] f = Normalize(Sub(center, eye));
        float[] r = Normalize(Cross(f, up));
        float[] u = Cross(r, f);

        float[] m = Identity();
        m[0]  =  r[0]; m[4]  =  r[1]; m[8]  =  r[2];
        m[1]  =  u[0]; m[5]  =  u[1]; m[9]  =  u[2];
        m[2]  = -f[0]; m[6]  = -f[1]; m[10] = -f[2];
        m[12] = -Dot(r, eye);
        m[13] = -Dot(u, eye);
        m[14] =  Dot(f, eye);
        return m;
    }

    public static float[] Multiply(float[] a, float[] b)
    {
        float[] r = new float[16];
        for (int col = 0; col < 4; col++)
        for (int row = 0; row < 4; row++)
        {
            float sum = 0;
            for (int k = 0; k < 4; k++)
                sum += a[row + k*4] * b[k + col*4];
            r[row + col*4] = sum;
        }
        return r;
    }

    public static float[] RotationX(float angle)
    {
        float c = MathF.Cos(angle), s = MathF.Sin(angle);
        float[] m = Identity();
        m[5]=c; m[9]=-s; m[6]=s; m[10]=c;
        return m;
    }

    public static float[] RotationY(float angle)
    {
        float c = MathF.Cos(angle), s = MathF.Sin(angle);
        float[] m = Identity();
        m[0]=c; m[8]=s; m[2]=-s; m[10]=c;
        return m;
    }

    public static float[] Translation(float x, float y, float z)
    {
        float[] m = Identity();
        m[12]=x; m[13]=y; m[14]=z;
        return m;
    }

    public static float[] Scale(float s)
    {
        float[] m = Identity();
        m[0]=s; m[5]=s; m[10]=s;
        return m;
    }
}