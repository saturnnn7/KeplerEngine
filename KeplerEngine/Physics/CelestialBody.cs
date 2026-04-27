using System;
using KeplerEngine.Core;

namespace KeplerEngine.Physics
{
    /// <summary>
    /// A massive body treated as the gravitational attractor (the primary in a 2-body system).
    /// You can construct it from any combination of:
    ///   • mass + radius
    ///   • μ (GM) + radius
    ///   • surface gravity + radius
    ///   • surface gravity + mass  (radius derived)
    ///
    /// All values in SI units (kg, m, m/s²) unless otherwise stated.
    /// </summary>
    public class CelestialBody
    {
        // -- Identity ----------
        public string Name { get; set; }

        // -- Stored primary properties ----------
        private double _mu;         // gravitational parameter, m³/s²
        private double _radius;     // mean radius, m

        // -- Derived / cached ----------
        public double Mu        => _mu;
        public double Radius    => _radius;
        public double Mass      => OrbitalMath.MassFromMu(_mu);
        public double SurfaceGravity => OrbitalMath.SurfaceGravity(_mu, _radius);

        // -- Position in simulation space (defaults to origin — the attractor sits still in 2-body) ----------
        public Vector3d Position { get; set; } = Vector3d.Zero;

        // -- Constructors ----------
        private CelestialBody(string name) { Name = name; }

        // -- Factory: mass + radius ----------
        public static CelestialBody FromMassAndRadius(string name, double mass, double radius)
        {
            ValidatePositive(mass, nameof(mass));
            ValidatePositive(radius, nameof(radius));
            return new CelestialBody(name)
            {
                _mu = OrbitalMath.MuFromMass(mass),
                _radius = radius
            };
        }

        // -- Factory: μ + radius ----------
        public static CelestialBody FromMuAndRadius(string name, double mu, double radius)
        {
            ValidatePositive(mu, nameof(mu));
            ValidatePositive(radius, nameof(radius));
            return new CelestialBody(name)
            {
                _mu = mu,
                _radius = radius
            };
        }

        // -- Factory: surface gravity + radius ----------
        public static CelestialBody FromSurfaceGravityAndRadius(string name, double g, double radius)
        {
            ValidatePositive(g, nameof(g));
            ValidatePositive(radius, nameof(radius));
            return new CelestialBody(name)
            {
                _mu = OrbitalMath.MuFromSurfaceGravity(g, radius),
                _radius = radius
            };
        }

        // -- Factory: surface gravity + mass  (radius derived) ----------
        public static CelestialBody FromSurfaceGravityAndMass(string name, double g, double mass)
        {
            ValidatePositive(g, nameof(g));
            ValidatePositive(mass, nameof(mass));
            double mu       = OrbitalMath.MuFromMass(mass);
            double radius   = OrbitalMath.RadiusFromMuAndGravity(mu, g);
            return new CelestialBody(name) { _mu = mu, _radius = radius };
        }

        // -- Mutation helpers ----------

        /// <summary>Change mass, keeping radius. μ is recalculated.</summary>
        public void SetMass(double mass)
        {
            ValidatePositive(mass, nameof(mass));
            _mu = OrbitalMath.MuFromMass(mass);
        }

        /// <summary>Change μ directly.</summary>
        public void SetMu(double mu)
        {
            ValidatePositive(mu, nameof(mu));
            _mu = mu;
        }

        /// <summary>Change radius, keeping μ. Surface gravity recalculates.</summary>
        public void SetRadius(double radius)
        {
            ValidatePositive(radius, nameof(radius));
            _radius = radius;
        }

        /// <summary>Change surface gravity, keeping radius. Mass/μ recalculated.</summary>
        public void SetSurfaceGravity(double g)
        {
            ValidatePositive(g, nameof(g));
            _mu = OrbitalMath.MuFromSurfaceGravity(g, _radius);
        }

        // -- Convenience queries ----------

        /// <summary>Orbital period for a circular orbit at given altitude above surface.</summary>
        public double CircularPeriodAtAltitude(double altitude)
            => OrbitalMath.OrbitalPeriod(_radius + altitude, _mu);
 
        /// <summary>Circular velocity at a given distance from center.</summary>
        public double CircularVelocity(double r)
            => Math.Sqrt(_mu / r);
 
        /// <summary>Escape velocity from a given distance from center.</summary>
        public double EscapeVelocity(double r)
            => OrbitalMath.EscapeVelocity(_mu, r);


        // -- Predefined bodies (real-world values) ----------

        /// <summary>Earth-like body.</summary>
        public static CelestialBody Earth()
            => FromMuAndRadius("Earth", 3.986_004_418e14, 6_371_000);
 
        /// <summary>Kerbin-like body (KSP's home planet).</summary>
        public static CelestialBody Kerbin()
            => FromMuAndRadius("Kerbin", 3.531_600_000e12, 600_000);

        // -- Helpers ----------
        private static void ValidatePositive(double value, string name)
        {
            if (value <= 0) throw new ArgumentException($"{name} must be positive, got {value}.");
        }

        public override string ToString() => $"{Name} | R={Radius:E3} m | M={Mass:E3} kg | g={SurfaceGravity:F4} m/s²  μ={Mu:E6}";
    }
}