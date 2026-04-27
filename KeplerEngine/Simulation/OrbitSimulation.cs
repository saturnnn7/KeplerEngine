using System;
using System.Collections.Generic;
using KeplerEngine.Orbital;
using KeplerEngine.Physics;
using KeplerEngine.Time;
 
namespace KeplerEngine.Simulation
{
    /// <summary>
    /// Top-level simulation container.
    /// Holds the clock, celestial bodies, and orbital bodies.
    /// Call Tick(realDt) each frame to advance time and propagate all orbits.
    /// </summary>
    public class OrbitSimulation
    {
        // -- Time ----------

        public SimulationClock Clock { get; } = new SimulationClock();

        // -- Bodies ----------

        private readonly List<CelestialBody>    _celestials = new();
        private readonly List<OrbitalBody>      _orbitals   = new();

        public IReadOnlyList<CelestialBody> Celestials  => _celestials;
        public IReadOnlyList<OrbitalBody>   Orbitals    => _orbitals;

        // -- Events ----------

        /// <summary>Fired after each simulation tick. Carry updated UT and sim delta.</summary>
        public event Action<double /*ut*/, double /*simDelta*/>? OnTick; // (UT, simDelta)

        // -- Body registration ----------

        public void AddCelestial(CelestialBody body) => _celestials.Add(body);
        public void AddOrbital(OrbitalBody body) => _orbitals.Add(body);

        public void RemoveCelestial(CelestialBody body) => _celestials.Remove(body);
        public void RemoveOrbital(OrbitalBody body) => _orbitals.Remove(body);

        // -- Simulation step ----------

        /// <summary>
        /// Advance the simulation by realDeltaSeconds of real (wall-clock) time.
        /// Applies time warp internally. Call this once per render/update frame.
        /// </summary>
        public void Tick(double realDeltaSeconds)
        {
            double simDelta = Clock.Tick(realDeltaSeconds);
            if (simDelta <= 0) return;
            
            foreach (var body in _orbitals)
                KeplerPropagator.Propagate(body, simDelta);
            
            OnTick?.Invoke(Clock.UT, simDelta);
        }

        // -- Convenience factory methods ----------

        /// <summary>
        /// Create an Earth-like primary and place a satellite in circular LEO.
        /// </summary>
        public static OrbitSimulation EarthExample()
        {
            var sim     = new OrbitSimulation();
            var earth   = CelestialBody.Earth();
            var el      = KeplerianElements.CircularOrbit(400_000, earth.Radius, inclinationRad: Core.OrbitalMath.DegToRad(51.6)); // ISS-like
            var sat     = new OrbitalBody("Satellite-1", earth, el);
 
            sim.AddCelestial(earth);
            sim.AddOrbital(sat);
            return sim;
        }

        /// <summary>
        /// Kerbin + satellite on an elliptic orbit (100km × 500km).
        /// </summary>
        public static OrbitSimulation KerbinExample()
        {
            var sim    = new OrbitSimulation();
            var kerbin = CelestialBody.Kerbin();
 
            double pe  = kerbin.Radius + 100_000;
            double ap  = kerbin.Radius + 500_000;
            double a   = (pe + ap) / 2.0;
            double e   = (ap - pe) / (ap + pe);
 
            var el  = new Orbital.KeplerianElements(a, e, 0, 0, 0, 0);
            var sat = new OrbitalBody("Probe", kerbin, el);
 
            sim.AddCelestial(kerbin);
            sim.AddOrbital(sat);
            return sim;
        }

        public override string ToString() => $"OrbitSim | {Clock} | Bodies: {_celestials.Count} primary, {_orbitals.Count} orbital";
    }
}