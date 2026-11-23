using System.Collections.Generic;
using UnityEngine;

namespace StreetRush
{
    /// <summary>
    /// One script for every car in the open world: the player's car, civilian
    /// traffic and police. Physics uses WheelColliders with arcade stability
    /// helpers (speed-sensitive steering, anti-roll, downforce).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class WorldCar : MonoBehaviour
    {
        public enum Mode { Parked, PlayerDriven, PolicePursuit, CivilianWander }

        public static readonly List<WorldCar> All = new List<WorldCar>();

        public Mode mode = Mode.Parked;
        public bool isPolice;

        [Header("Engine")]
        public float maxMotorTorque = 1900f;
        public float reverseTorque = 1000f;
        public float maxBrakeTorque = 4200f;
        public float brakeRampSpeed = 3.5f;
        public float topSpeedKmh = 175f;
        public float cruiseSpeedKmh = 40f;
        public float pursuitSpeedKmh = 95f;

        [Header("Steering")]
        public float maxSteerAngle = 34f;
        public float steerSmoothing = 8.5f;
        public float highSpeedSteerFalloffKmh = 150f;
        public float minSteerMultiplier = 0.3f;

        [Header("Stability")]
        public float antiRoll = 9000f;
        public float downforce = 2.2f;

        public Rigidbody Body { get; private set; }
        public float SpeedKmh => Body ? Body.linearVelocity.magnitude * 3.6f : 0f;
        public float ForwardSpeed => Body ? Vector3.Dot(Body.linearVelocity, transform.forward) : 0f;

        WheelCollider[] _wheels;     // FL, FR, RL, RR
        Transform[] _wheelVisuals;
        GameObject[] _sirens;
        float _steer, _brake;
        Vector3 _wanderTarget;
        float _wanderTimer, _sirenTimer, _bustTimer, _stuckTimer;

        // ---------------- Construction ----------------

        public static WorldCar Create(Vector3 position, Quaternion rotation, Color paint, bool police, Mode mode)
        {
            var visual = CarFactory.BuildVisual(paint, police, out var wheelVisuals, out var sirens);
            visual.transform.SetPositionAndRotation(position, rotation);
            var car = visual.AddComponent<WorldCar>();
            car.isPolice = police;
            car.mode = mode;
            car._wheelVisuals = wheelVisuals;
            car._sirens = sirens;
            car.Setup();
            Layers.SetRecursively(visual, Layers.Vehicle);
            return car;
        }

        void Setup()
        {
            Body = GetComponent<Rigidbody>();
            Body.mass = 1350f;
            Body.linearDamping = 0.02f;
            Body.angularDamping = 0.4f;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.centerOfMass = new Vector3(0f, 0.42f, 0.05f);

            // Colliders: lower body + cabin.
            var lower = gameObject.AddComponent<BoxCollider>();
            lower.center = new Vector3(0, 0.72f, 0);
            lower.size = new Vector3(CarFactory.Width, 0.62f, CarFactory.Length);
            var cabin = gameObject.AddComponent<BoxCollider>();
            cabin.center = new Vector3(0, 1.2f, -0.3f);
            cabin.size = new Vector3(CarFactory.Width - 0.3f, 0.5f, 2.1f);

            var phys = new PhysicsMaterial("CarBody") { dynamicFriction = 0.25f, staticFriction = 0.3f, bounciness = 0.05f, frictionCombine = PhysicsMaterialCombine.Minimum };
            lower.sharedMaterial = phys;
            cabin.sharedMaterial = phys;

            _wheels = new WheelCollider[4];
            var pos = CarFactory.WheelPositions();
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("WheelCollider" + i);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = pos[i] + Vector3.up * 0.18f;
                var wc = go.AddComponent<WheelCollider>();
                wc.mass = 22f;
                wc.radius = CarFactory.WheelRadius;
                wc.suspensionDistance = 0.22f;
                wc.forceAppPointDistance = 0.12f;
                wc.wheelDampingRate = 0.4f;
                var spring = wc.suspensionSpring;
                spring.spring = 38000f;
                spring.damper = 4200f;
                spring.targetPosition = 0.45f;
                wc.suspensionSpring = spring;

                var fwd = wc.forwardFriction;
                fwd.extremumSlip = 0.4f; fwd.extremumValue = 1f; fwd.asymptoteSlip = 0.8f; fwd.asymptoteValue = 0.6f; fwd.stiffness = 1.6f;
                wc.forwardFriction = fwd;
                var side = wc.sidewaysFriction;
                side.extremumSlip = 0.25f; side.extremumValue = 1f; side.asymptoteSlip = 0.5f; side.asymptoteValue = 0.75f; side.stiffness = i < 2 ? 1.9f : 2.1f;
                wc.sidewaysFriction = side;
                wc.ConfigureVehicleSubsteps(5f, 12, 15);
                _wheels[i] = wc;
            }
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Start()
        {
            if (Body == null) Body = GetComponent<Rigidbody>();
            _wanderTarget = transform.position;
            SetSirens(false);
        }

        public void SetDriver(bool active)
        {
            mode = active ? Mode.PlayerDriven : (isPolice ? Mode.CivilianWander : Mode.Parked);
            if (!active)
            {
                MobileInput.Steer = 0; MobileInput.Throttle = 0;
            }
        }

        /// <summary>Puts the car to sleep / wakes it (used by the city's distance culling).</summary>
        public void SetSimulated(bool on)
        {
            if (Body.isKinematic == !on) return;
            Body.isKinematic = !on;
            foreach (var w in _wheels) w.enabled = on;
        }

        // ---------------- Loop ----------------

        void FixedUpdate()
        {
            if (Body.isKinematic) return;
            var gm = GameManager.Instance;

            if (isPolice && gm != null)
            {
                if (gm.WantedLevel > 0 && mode != Mode.PlayerDriven) mode = Mode.PolicePursuit;
                else if (gm.WantedLevel <= 0 && mode == Mode.PolicePursuit) mode = Mode.CivilianWander;
            }

            float steerTarget = 0f, motor = 0f, brakeTarget = 0f;
            switch (mode)
            {
                case Mode.PlayerDriven: DrivePlayer(ref steerTarget, ref motor, ref brakeTarget); break;
                case Mode.PolicePursuit: DrivePursuit(ref steerTarget, ref motor, ref brakeTarget); break;
                case Mode.CivilianWander: DriveWander(ref steerTarget, ref motor, ref brakeTarget); break;
                default: brakeTarget = maxBrakeTorque; break;
            }

            float dt = Time.fixedDeltaTime;
            _steer = Mathf.Lerp(_steer, steerTarget, steerSmoothing * dt);
            if (mode == Mode.PlayerDriven)
                _brake = Mathf.MoveTowards(_brake, brakeTarget, maxBrakeTorque * brakeRampSpeed * dt);
            else
                _brake = brakeTarget;

            if (SpeedKmh > topSpeedKmh && motor * ForwardSpeed > 0) motor = 0f;

            for (int i = 0; i < 4; i++)
            {
                var w = _wheels[i];
                if (i < 2) w.steerAngle = _steer;
                w.motorTorque = motor * 0.25f * (i < 2 ? 0.8f : 1.2f);
                w.brakeTorque = _brake * (i < 2 ? 0.6f : 0.4f);
            }

            ApplyAntiRoll(_wheels[0], _wheels[1]);
            ApplyAntiRoll(_wheels[2], _wheels[3]);
            Body.AddForce(-transform.up * downforce * Body.linearVelocity.magnitude * Body.mass * 0.1f);

            if (mode == Mode.PlayerDriven && gm != null) gm.CurrentSpeedKmh = SpeedKmh;
        }

        void LateUpdate()
        {
            if (_wheelVisuals == null) return;
            for (int i = 0; i < 4; i++)
            {
                if (_wheels[i] == null || !_wheels[i].enabled) continue;
                _wheels[i].GetWorldPose(out var p, out var q);
                _wheelVisuals[i].SetPositionAndRotation(p, q);
            }

            if (_sirens != null)
            {
                bool chasing = mode == Mode.PolicePursuit;
                if (chasing)
                {
                    _sirenTimer += Time.deltaTime;
                    bool red = ((int)(_sirenTimer * 4f)) % 2 == 0;
                    _sirens[0].SetActive(red);
                    _sirens[1].SetActive(!red);
                }
                else SetSirens(false);
            }
        }

        void SetSirens(bool on)
        {
            if (_sirens == null) return;
            foreach (var s in _sirens) if (s.activeSelf != on) s.SetActive(on);
        }

        void ApplyAntiRoll(WheelCollider left, WheelCollider right)
        {
            float travelL = 1f, travelR = 1f;
            bool gl = left.GetGroundHit(out var hitL);
            if (gl) travelL = (-left.transform.InverseTransformPoint(hitL.point).y - left.radius) / left.suspensionDistance;
            bool gr = right.GetGroundHit(out var hitR);
            if (gr) travelR = (-right.transform.InverseTransformPoint(hitR.point).y - right.radius) / right.suspensionDistance;
            float force = (travelL - travelR) * antiRoll;
            if (gl) Body.AddForceAtPosition(left.transform.up * -force, left.transform.position);
            if (gr) Body.AddForceAtPosition(right.transform.up * force, right.transform.position);
        }

        // ---------------- Driving modes ----------------

        void DrivePlayer(ref float steer, ref float motor, ref float brake)
        {
            float steerIn = MobileInput.Steer;
            float throttleIn = MobileInput.Throttle;
            bool brakeIn = MobileInput.Brake;
            bool reverseIn = MobileInput.Reverse;

            // Keyboard fallback
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) steerIn = -1f;
            else if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) steerIn = 1f;
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) throttleIn = 1f;
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) reverseIn = true;
            if (Input.GetKey(KeyCode.Space)) brakeIn = true;

            float falloff = Mathf.Clamp(1f - SpeedKmh / highSpeedSteerFalloffKmh, minSteerMultiplier, 1f);
            steer = steerIn * maxSteerAngle * falloff;

            if (brakeIn)
            {
                brake = maxBrakeTorque;
                motor = 0f;
            }
            else if (reverseIn)
            {
                // Brake first if still rolling forward, then reverse.
                if (ForwardSpeed > 1.5f) { brake = maxBrakeTorque * 0.7f; motor = 0f; }
                else { brake = 0f; motor = -reverseTorque; }
            }
            else
            {
                brake = throttleIn > 0.01f ? 0f : maxBrakeTorque * 0.04f; // light engine braking
                motor = throttleIn * maxMotorTorque;
            }
        }

        void DrivePursuit(ref float steer, ref float motor, ref float brake)
        {
            var gm = GameManager.Instance;
            var player = gm != null ? gm.PlayerRef : null;
            if (player == null) { brake = maxBrakeTorque; return; }

            Vector3 targetPos = gm.IsDriving && gm.ActiveVehicle != null ? gm.ActiveVehicle.transform.position : player.position;
            SteerTowards(targetPos, ref steer);

            float dist = Vector3.Distance(transform.position, targetPos);
            if (dist < 4f)
            {
                _bustTimer += Time.fixedDeltaTime;
                if (_bustTimer > 1.5f && !gm.IsDriving)
                {
                    gm.BustPlayer();
                    _bustTimer = 0f;
                }
            }
            else _bustTimer = 0f;

            if (dist > 4f && SpeedKmh < pursuitSpeedKmh) { motor = maxMotorTorque * 0.9f; brake = 0f; }
            else { motor = 0f; brake = maxBrakeTorque * 0.3f; }
            Unstick(ref motor, ref steer);
        }

        void DriveWander(ref float steer, ref float motor, ref float brake)
        {
            _wanderTimer -= Time.fixedDeltaTime;
            if (_wanderTimer <= 0f || Vector3.Distance(transform.position, _wanderTarget) < 6f)
            {
                _wanderTarget = PickWanderTarget();
                _wanderTimer = 14f;
            }
            SteerTowards(_wanderTarget, ref steer);

            // Slow down for anything right ahead (other cars, pedestrians, walls).
            bool blocked = Physics.Raycast(transform.position + Vector3.up * 0.8f + transform.forward * 2.4f, transform.forward, out _, Mathf.Max(4f, SpeedKmh * 0.25f), ~0, QueryTriggerInteraction.Ignore);
            if (blocked) { motor = 0f; brake = maxBrakeTorque * 0.6f; }
            else if (SpeedKmh < cruiseSpeedKmh) { motor = maxMotorTorque * 0.45f; brake = 0f; }
            else { motor = 0f; brake = maxBrakeTorque * 0.05f; }
            Unstick(ref motor, ref steer);
        }

        void SteerTowards(Vector3 worldTarget, ref float steer)
        {
            Vector3 local = transform.InverseTransformPoint(worldTarget);
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            steer = Mathf.Clamp(angle, -maxSteerAngle, maxSteerAngle);
        }

        /// <summary>AI cars that get stuck against something reverse out for a moment.</summary>
        void Unstick(ref float motor, ref float steer)
        {
            if (motor > 0 && SpeedKmh < 2f) _stuckTimer += Time.fixedDeltaTime;
            else if (_stuckTimer < 2.5f) _stuckTimer = 0f;
            if (_stuckTimer > 2.5f)
            {
                motor = -reverseTorque;
                steer = -steer;
                if (_stuckTimer > 4.5f) { _stuckTimer = 0f; _wanderTarget = PickWanderTarget(); }
                else _stuckTimer += Time.fixedDeltaTime;
            }
        }

        Vector3 PickWanderTarget()
        {
            var pts = GameManager.Instance != null ? GameManager.Instance.RoadPoints : null;
            if (pts == null || pts.Count == 0) return transform.position + transform.forward * 30f;
            return pts[Random.Range(0, pts.Count)];
        }
    }
}
