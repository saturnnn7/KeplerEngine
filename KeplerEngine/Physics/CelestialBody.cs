using System;
using KeplerEngine.Core;

namespace KeplerEngine.Physics
{
    public class CelestialBody
    {
        // -- Identity ----------
        public string Name { get; set; }

        // -- Stored primary properties ----------
        private double _mu;         // gravitational parameter, m³/s²
        private double _radius;     // mean radius, m

        // -- Derived / cached ----------
        public double Mu => _mu;
        public double Radius => _radius;
        public double Mass => OrbitalMath.MassFromMu(_mu);

        // --  ----------


        // --  ----------


        // --  ----------


        // --  ----------


        // --  ----------








    }
}