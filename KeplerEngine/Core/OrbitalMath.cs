using System;
 
namespace KeplerEngine.Core
{
    public static class OrbitalMath
    {
        // -- Physical constants ----------

        /// <summary>Universal gravitational constant G, m³/(kg·s²)</summary>
        public const double G = 6.674_30e-11;

        /// <summary>π shortcut</summary>
        public const double Pi = Math.PI;
        public const double TwoPi = 2.0 * Math.PI;

        // -- Gravitational parameter (μ = GM) ----------

        /// <summary>μ from mass</summary>
        public static double MuFromMass(double mass) => G * mass;

        /// <summary>mass from μ</summary>
        public static double MassFromMu(double mu) => mu / G;

        // -- Surface gravity ----------

        /// <summary>g = GM / R²</summary>
        public static double SurfaceGravity(double mu, double radius) => mu / (radius * radius);

        /// <summary>μ from surface gravity and radius: μ = g·R²</summary>
        public static double MuFromSurfaceGravity(double g, double radius) => g * radius * radius;

        /// <summary>Radius from μ and surface gravity: R = √(μ/g)</summary>
        public static double RadiusFromMuAndGravity(double mu, double g) => Math.Sqrt(mu / g);

        // -- Orbital period ----------

        /// <summary>T = 2π √(a³/μ)</summary>
        public static double OrbitalPeriod(double semiMajorAxis, double mu) => TwoPi * Math.Sqrt(semiMajorAxis * semiMajorAxis * semiMajorAxis / mu);

        /// <summary>a from period: a = (μ·(T/2π)²)^(1/3)</summary>
        public static double SemiMajorAxisFromPeriod(double period, double mu)
        {
            double t = period / TwoPi;
            return Math.Cbrt(mu * t * t);
        }

        // -- Vis-viva equation ----------

        /// <summary>v² = μ(2/r − 1/a)  →  speed at any point on the orbit</summary>
        public static double VisViva(double mu, double r, double semiMajorAxis) => Math.Sqrt(mu * (2.0 / r - 1.0 / semiMajorAxis));

        // -- Escape velocity ----------

        /// <summary>v_esc = √(2μ/r)</summary>
        public static double EscapeVelocity(double mu, double r) => Math.Sqrt(2.0 * mu / r);

        // -- Anomaly conversions ----------

        /// <summary>
        /// Solve Kepler's equation  M = E - e·sin(E)  for eccentric anomaly E.
        /// Uses Newton-Raphson iteration. Works for e in [0, 1) (elliptic orbits).
        /// </summary>
        public static double EccentricAnomalyFromMean(double M, double e, int maxIter = 100, double tol = 1e-12)
        {
            M = WrapAngle(M);
            double E = (e > 0.8) ? Pi : M; // better initial guess for high eccentricity
            for (int i = 0; i < maxIter; i++)
            {
                double dE = (M - E + e * Math.Sin(E)) / (1.0 - e * Math.Cos(E));
                E += dE;
                if (Math.Abs(dE) < tol) break;
            }
            return E;
        }

        /// <summary>True anomaly ν from eccentric anomaly E</summary>
        public static double TrueAnomalyFromEccentric(double E, double e)
        {
            double sinV = Math.Sqrt(1.0 - e * e) * Math.Sin(E);
            double cosV = Math.Cos(E) - e;
            return Math.Atan2(sinV, cosV);
        }

        /// <summary>True anomaly directly from mean anomaly</summary>
        public static double TrueAnomalyFromMean(double M, double e) => TrueAnomalyFromEccentric(EccentricAnomalyFromMean(M, e), e);

        /// <summary>Mean anomaly from eccentric anomaly  M = E - e·sin(E)</summary>
        public static double MeanAnomalyFromEccentric(double E, double e) => E - e * Math.Sin(E);
 
        /// <summary>Eccentric anomaly from true anomaly</summary>
        public static double EccentricAnomalyFromTrue(double nu, double e)
        {
            double sinE = Math.Sqrt(1.0 - e * e) * Math.Sin(nu) / (1.0 + e * Math.Cos(nu));
            double cosE = (e + Math.Cos(nu)) / (1.0 + e * Math.Cos(nu));
            return Math.Atan2(sinE, cosE);
        }

        // -- Orbital radius at true anomaly ----------

        /// <summary>r = a(1 - e²) / (1 + e·cos ν)</summary>
        public static double RadiusAtTrueAnomaly(double a, double e, double nu) => a * (1.0 - e * e) / (1.0 + e * Math.Cos(nu));

        // -- Perigee / apogee ----------

        public static double Periapsis(double a, double e) => a * (1.0 - e);
        public static double Apoapsis(double a, double e)  => a * (1.0 + e);

        // -- Angle utilities ----------
        
        /// <summary>Wrap angle to [0, 2π)</summary>
        public static double WrapAngle(double angle)
        {
            angle %= TwoPi;
            if (angle < 0) angle += TwoPi;
            // Clamp floating-point overshoot just below 2π back to 0
            if (angle >= TwoPi - 1e-12) angle = 0;
            return angle;
        }

        public static double DegToRad(double deg) => deg * Pi / 180.0;
        public static double RadToDeg(double rad) => rad * 180.0 / Pi;
        
        // --  ----------
    }
}