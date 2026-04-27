using KeplerEngine.Core;
using KeplerEngine.Physics;
using KeplerEngine.Orbital;
using KeplerEngine.Time;
using KeplerEngine.Simulation;

Console.WriteLine("=== KeplerEngine — 2-Body Test ===\n");

// ── 1. Создаём планету ────────────────────────────────────────────────────────
var earth = CelestialBody.Earth();
Console.WriteLine("── Планета ──────────────────────────────────────────");
Console.WriteLine(earth);

// Можно создать через поверхностное ускорение + радиус
var earthFromG = CelestialBody.FromSurfaceGravityAndRadius("Earth (from g)", 9.80665, 6_371_000);
Console.WriteLine(earthFromG);

// Kerbin из KSP
var kerbin = CelestialBody.Kerbin();
Console.WriteLine(kerbin);

// ── 2. Создаём орбитальное тело ───────────────────────────────────────────────
Console.WriteLine("\n── Орбитальные элементы ─────────────────────────────");

var elements = new KeplerianElements(
    a:     earth.Radius + 400_000,          // 400 км над поверхностью
    e:     0.01,                            // почти круговая
    i:     OrbitalMath.DegToRad(51.6),      // наклонение как у МКС
    lan:   OrbitalMath.DegToRad(0),         // долгота восходящего узла
    argPe: OrbitalMath.DegToRad(0),         // аргумент перигея
    nu:    OrbitalMath.DegToRad(0));         // истинная аномалия (стартовая позиция)

Console.WriteLine(elements);
Console.WriteLine($"  Перигей:  {(elements.Periapsis  - earth.Radius) / 1000:F1} км");
Console.WriteLine($"  Апогей:   {(elements.Apoapsis   - earth.Radius) / 1000:F1} км");

var satellite = new OrbitalBody("Satellite-1", earth, elements);
Console.WriteLine(satellite);

// ── 3. Конвертация Keplerian ↔ Cartesian ─────────────────────────────────────
Console.WriteLine("\n── State Vector ─────────────────────────────────────");

var sv = StateVector.FromKeplerian(elements, earth.Mu);
Console.WriteLine($"  Позиция:  {sv.Position}");
Console.WriteLine($"  Скорость: {sv.Velocity}");
Console.WriteLine($"  |v| =     {sv.Velocity.Magnitude:F2} м/с");

// Обратная конвертация — должны получить те же элементы
var recovered = StateVector.ToKeplerian(sv, earth.Mu);
Console.WriteLine($"  Recovered: {recovered}");

// ── 4. Propagation — прокручиваем орбиту ─────────────────────────────────────
Console.WriteLine("\n── Propagation ──────────────────────────────────────");

double period = satellite.Period;
Console.WriteLine($"  Орбитальный период: {period / 60:F1} мин ({period:F0} с)");

// Прокрутим ровно на один период — ν должна вернуться в 0
double nuBefore = satellite.TrueAnomaly;
KeplerPropagator.Propagate(satellite, period);
double nuAfter = satellite.TrueAnomaly;

Console.WriteLine($"  ν до:    {OrbitalMath.RadToDeg(nuBefore):F4}°");
Console.WriteLine($"  ν после: {OrbitalMath.RadToDeg(nuAfter):F4}°  (один полный оборот)");

// Прокрутим на четверть периода
KeplerPropagator.Propagate(satellite, period / 4.0);
Console.WriteLine($"  ν после T/4: {satellite.TrueAnomalyDeg:F2}°  (ожидаем ~90°)");

// ── 5. Симуляция с временем ───────────────────────────────────────────────────
Console.WriteLine("\n── Simulation + Clock ───────────────────────────────");

var sim = OrbitSimulation.KerbinExample();
Console.WriteLine(sim);

// Варп x100, гоним 60 "реальных" секунд по 1/60 за кадр
sim.Clock.SetWarp(100);
for (int frame = 0; frame < 60 * 60; frame++)   // 60 сек * 60 fps
    sim.Tick(1.0 / 60.0);

Console.WriteLine($"  После 60с реального времени при варпе x100:");
Console.WriteLine($"  {sim.Clock}");
Console.WriteLine($"  {sim.Orbitals[0]}");

// ── 6. Маневр — ApplyDeltaV ───────────────────────────────────────────────────
Console.WriteLine("\n── Манёвр (Δv) ──────────────────────────────────────");

var probe = sim.Orbitals[0];
double altBefore = probe.Altitude / 1000;
double speedBefore = probe.Speed;

Console.WriteLine($"  До:    alt={altBefore:F1} км  v={speedBefore:F1} м/с  e={probe.Eccentricity:F4}");

probe.ApplyDeltaV(deltaPrograde: 100, deltaNormal: 0, deltaRadial: 0); // +100 м/с прографт

Console.WriteLine($"  После: alt={probe.Altitude/1000:F1} км  v={probe.Speed:F1} м/с  e={probe.Eccentricity:F4}");
Console.WriteLine($"  Новый апогей: {(probe.Elements.Apoapsis - sim.Celestials[0].Radius)/1000:F1} км");

// ── 7. Точки орбиты для рендеринга ───────────────────────────────────────────
Console.WriteLine("\n── Orbit Points (для рендера) ───────────────────────");

var points = probe.GetOrbitPoints(6);
for (int i = 0; i < points.Length; i++)
    Console.WriteLine($"  [{i}] {points[i]}");

Console.WriteLine("\n=== Готово ===");