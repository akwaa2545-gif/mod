#nullable disable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NuclearOptionObserver
{
    // Per-aircraft bounded cache; persistent measurements are global heights, never local hit points.
    internal sealed class HoldAirframe : IDisposable
    {
        private readonly Aircraft aircraft;
        private readonly Pilot pilot;
        private readonly List<UnitPart> parts = new List<UnitPart>();
        private readonly List<IEngine> engines = new List<IEngine>();
        private readonly RaycastHit[] hits = new RaycastHit[16];
        private readonly TerrainPoint[] points = new TerrainPoint[8];
        private readonly TerrainScanSchedule cadence = new TerrainScanSchedule();
        private AircraftParameters parameters;
        private SwivelDuctSystem swivel;
        private Rigidbody body;
        private AirframeKind kind;
        private bool damaged, disposed;
        private double nextHealth;
        internal TerrainObservation Terrain { get; private set; }
        internal HoldAirframe(Aircraft aircraft, Pilot pilot) { this.aircraft = aircraft; this.pilot = pilot; }
        internal bool Prepare(out string reason)
        {
            reason = "Missing/unsupported airframe/component telemetry.";
            body = aircraft.CockpitRB(); parameters = aircraft.GetAircraftParameters();
            if (body == null || parameters == null || aircraft.cockpit == null) return false;
            var swivels = aircraft.GetComponentsInChildren<SwivelDuctSystem>(true);
            if (swivels.Length > 1) { reason = "Ambiguous swivel component."; return false; }
            swivel = swivels.Length == 1 ? swivels[0] : null;
            if (swivel != null && !swivel.transform.IsChildOf(aircraft.transform) && swivel.transform != aircraft.transform) return false;
            kind = pilot.pilotType == Pilot.PilotType.Plane ? AirframeKind.FixedWing : pilot.pilotType == Pilot.PilotType.VTOL ? AirframeKind.SwivelVtol : AirframeKind.Unsupported;
            if (aircraft.GetComponentInChildren<TiltWingController>(true) != null || aircraft.GetComponentInChildren<CompoundHeloController>(true) != null) kind = AirframeKind.Unsupported;
            var all = aircraft.GetComponentsInChildren<UnitPart>(false);
            if (all.Length > 512) { reason = "Part telemetry exceeds bounded cache."; return false; }
            foreach (var part in all)
            {
                if (part == null || !ReferenceEquals(part.parentUnit, aircraft)) continue;
                if (parts.Count >= 256) return false;
                parts.Add(part); part.onApplyDamage += Damage; part.onPartDetached += Detached;
                if (!FlightEnvelope.Finite(part.hitPoints) || part.hitPoints < 99.5f || part.IsDetached()) damaged = true;
            }
            if (parts.Count == 0 || aircraft.engines == null || aircraft.engines.Count == 0 || aircraft.engines.Count > 16) return false;
            foreach (var engine in aircraft.engines)
            {
                if (engine == null || engine.transform == null || !(engine.transform == aircraft.transform || engine.transform.IsChildOf(aircraft.transform))) return false;
                engines.Add(engine); engine.OnEngineDamage += EngineFault; engine.OnEngineDisable += EngineFault;
            }
            reason = ""; return true;
        }
        private void Damage(UnitPart.OnApplyDamage d) { if (d.detached || d.pierceDamage > 0 || d.blastDamage > 0 || d.fireDamage > 0 || d.impactDamage > 0) damaged = true; }
        private void Detached(UnitPart part) { damaged = true; }
        private void EngineFault() { damaged = true; }
        internal bool Read(out FlightTelemetry f, out string reason)
        {
            f = default; reason = "Airframe rigidbody/world datum unavailable.";
            if (disposed || body == null || body.isKinematic || parameters == null || Datum.origin == null || aircraft.cockpit == null) return false;
            if (Time.time >= nextHealth)
            {
                nextHealth = Time.time + .25;
                foreach (var part in parts) if (part == null || !ReferenceEquals(part.parentUnit, aircraft) || !part.isActiveAndEnabled || part.IsDetached() || !FlightEnvelope.Finite(part.hitPoints) || part.hitPoints < 99.5f) damaged = true;
            }
            bool running = aircraft.Ignition && aircraft.GetFuelLevel() > .01f;
            foreach (var engine in engines) { float rpm = engine.GetRPMRatio(); if (!FlightEnvelope.Finite(rpm) || rpm < .25f) running = false; }
            var frame = aircraft.cockpit.transform; var velocity = body.velocity; var air = velocity - aircraft.GetWindVelocity();
            var localAir = frame.InverseTransformDirection(air); var omega = frame.InverseTransformDirection(body.angularVelocity);
            float horizontal = new Vector2(velocity.x, velocity.z).magnitude;
            if (horizontal < 20) { reason = "Ground course is degenerate/too slow."; return false; }
            float pitch = Mathf.Asin(Mathf.Clamp(frame.forward.y, -1, 1)) * Mathf.Rad2Deg;
            float alpha = pitch - Mathf.Atan2(air.y, new Vector2(air.x, air.z).magnitude) * Mathf.Rad2Deg;
            f = new FlightTelemetry { Altitude = aircraft.transform.position.y - Datum.originPosition.y, Speed = aircraft.speed, GroundSpeed = horizontal, AirSpeed = localAir.z,
                VerticalSpeed = velocity.y, Pitch = pitch, Bank = Mathf.Atan2(-frame.right.y, frame.up.y) * Mathf.Rad2Deg, Alpha = alpha,
                OmegaX = omega.x, OmegaY = omega.y, OmegaZ = omega.z, Slip = Mathf.Atan2(localAir.x, localAir.z) * Mathf.Rad2Deg,
                CourseX = velocity.x / horizontal, CourseZ = velocity.z / horizontal, Throttle = aircraft.GetInputs().throttle, RadarAltitude = aircraft.radarAlt,
                MinAirspeed = Mathf.Max(45, Mathf.Max(parameters.takeoffSpeed, parameters.landingSpeed) * 1.35f), MaxSpeed = parameters.maxSpeed,
                NozzleAngle = swivel != null ? swivel.GetNozzleAngle() : 0, CustomAxis = aircraft.GetInputs().customAxis1,
                Ready = aircraft.LocalSim && !aircraft.disabled, GearRetracted = !aircraft.gearDeployed && aircraft.gearState == LandingGear.GearState.LockedRetracted,
                AutoHover = aircraft.IsAutoHoverEnabled(), Damaged = damaged, EnginesRunning = running, HasSwivel = swivel != null && swivel.isActiveAndEnabled, Kind = kind };
            reason = ""; return true;
        }
        private static bool Finite(Vector3 v) => FlightEnvelope.Finite(v.x) && FlightEnvelope.Finite(v.y) && FlightEnvelope.Finite(v.z);
        private bool Query(Vector3 origin, Vector3 direction, float distance, out bool found, out RaycastHit nearest)
        {
            found = false; nearest = default;
            if (!Finite(origin) || !Finite(direction) || !FlightEnvelope.Finite(distance) || distance <= 0) return false;
            int count = Physics.RaycastNonAlloc(origin, direction, hits, distance, PhysicsLayers.StaticsMask | PhysicsLayers.ShipsMask, QueryTriggerInteraction.Ignore);
            if (count >= hits.Length) return false;
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i]; if (hit.collider == null) return false;
                if (hit.collider.transform == aircraft.transform || hit.collider.transform.IsChildOf(aircraft.transform)) continue;
                if (!FlightEnvelope.Finite(hit.distance) || !FlightEnvelope.Finite(hit.point.y)) return false;
                if (hit.distance < best) { best = hit.distance; nearest = hit; found = true; }
            }
            return true;
        }
        internal bool SampleTerrain(FlightTelemetry f, bool enabled, out string reason)
        {
            reason = ""; if (!enabled) { Terrain = default; cadence.Reset(); return true; }
            if (!cadence.Due(Time.time)) return Terrain.Valid;
            Terrain = default;
            if (Datum.origin == null || PhysicsLayers.StaticsMask == 0 || PhysicsLayers.ShipsMask == 0) { reason = "Terrain world/layers unavailable."; return false; }
            var position = aircraft.cockpit.transform.position; float sea = Datum.LocalSeaY;
            if (!Finite(position) || !FlightEnvelope.Finite(sea) || !FlightEnvelope.Finite(aircraft.maxRadius)) { reason = "Terrain coordinate/radius invalid."; return false; }
            var direction = new Vector3(f.CourseX, 0, f.CourseZ); var right = new Vector3(f.CourseZ, 0, -f.CourseX);
            float horizon = Mathf.Clamp(f.GroundSpeed * 8, 400, 2000); int used = 0;
            for (int i = 0; i < 6; i++)
            {
                float distance = i == 0 ? 0 : i <= 3 ? horizon * i / 3 : horizon * 2 / 3;
                var point = position + direction * distance + (i >= 4 ? right * Mathf.Max(10, aircraft.maxRadius) * (i == 4 ? -1 : 1) : Vector3.zero);
                point.y = sea + 12000;
                if (!Query(point, Vector3.down, 14000, out var found, out var hit)) { reason = "Terrain query invalid/truncated."; return false; }
                points[used++] = new TerrainPoint(distance, found ? Mathf.Max(0, hit.point.y - Datum.originPosition.y) : 0);
            }
            var velocity = body.velocity; float length = velocity.magnitude;
            if (!FlightEnvelope.Finite(length) || length < 1) { reason = "Terrain descent vector invalid."; return false; }
            var ray = velocity / length; var start = position - Vector3.up * Mathf.Max(2, aircraft.maxRadius);
            if (!Query(start, ray, horizon, out var direct, out var collision)) { reason = "Direct terrain ray invalid/truncated."; return false; }
            if (direct) points[used++] = new TerrainPoint(collision.distance * new Vector2(ray.x, ray.z).magnitude, Mathf.Max(0, collision.point.y - Datum.originPosition.y));
            if (ray.y < -.001f)
            {
                float water = (sea - start.y) / ray.y;
                if (water >= 0 && water <= horizon && used < points.Length) points[used++] = new TerrainPoint(water * new Vector2(ray.x, ray.z).magnitude, 0);
            }
            Terrain = new TerrainObservation(true, Time.time, points, used); return true;
        }
        internal void ResetTerrain() { cadence.Reset(); Terrain = default; }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            foreach (var part in parts) if (!ReferenceEquals(part, null)) { part.onApplyDamage -= Damage; part.onPartDetached -= Detached; }
            foreach (var engine in engines) { engine.OnEngineDamage -= EngineFault; engine.OnEngineDisable -= EngineFault; }
            parts.Clear(); engines.Clear();
        }
    }
}
