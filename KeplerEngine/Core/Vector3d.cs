using System;
using System.Diagnostics.CodeAnalysis;

namespace KeplerEngine.Core
{
    /// <summary>
    /// Double-precision 3D vector. Used throughout the engine for positions, velocities, etc.
    /// </summary>
    public readonly struct Vector3d : IEquatable<Vector3d>
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Z;

        public static readonly Vector3d Zero    = new(0, 0, 0);
        public static readonly Vector3d UnitX   = new(1, 0, 0);
        public static readonly Vector3d UnitY   = new(0, 1, 0);
        public static readonly Vector3d UnitZ   = new(0, 0, 1);

        public Vector3d(double x, double y, double z) { X = x; Y = y; Z = z; }

        // Squared Magnitude = the square of the vector's length.
        public double SqrMagnitude => X * X + Y * Y + Z * Z;
        public double Magnitude => Math.Sqrt(SqrMagnitude);

        public Vector3d Normalized
        {
            get
            {
                double m = Magnitude;
                if (m < double.Epsilon) return Zero;
                return new Vector3d(X / m, Y / m, Z / m);
            }
        }

        // -- Operators ----------
        public static Vector3d operator +(Vector3d a, Vector3d b)   => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vector3d operator -(Vector3d a, Vector3d b)   => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vector3d operator -(Vector3d a)               => new(-a.X, -a.Y, -a.Z);
        public static Vector3d operator *(Vector3d a, double s)     => new(a.X * s, a.Y * s, a.Z * s);
        public static Vector3d operator *(double s, Vector3d a)     => a * s;
        public static Vector3d operator /(Vector3d a, double s)     => new(a.X / s, a.Y / s, a.Z / s);

        // -- Products ----------
        public static double Dot(Vector3d a, Vector3d b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Vector3d Cross(Vector3d a, Vector3d b) => new(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X);
        
        public static double Angle(Vector3d a, Vector3d b)
        {
            double demon = a.Magnitude * b.Magnitude;
            if (demon < double.Epsilon) return 0;
            double cos = Math.Clamp(Dot(a,b) / demon, -1.0, 1.0);
            return Math.Acos(cos); // radians
        }

        public static double Distance(Vector3d a, Vector3d b) => (a - b).Magnitude;

        // -- Equality ----------
        public bool Equals(Vector3d other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object? obj) => obj is Vector3d v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
        public static bool operator ==(Vector3d a, Vector3d b) => a.Equals(b);
        public static bool operator !=(Vector3d a, Vector3d b) => !a.Equals(b);

        public override string ToString() => $"({X:G6}, {Y:G6}, {Z:g^})";
    }
}