using UnityEngine;

namespace StreetRush
{
    /// <summary>
    /// Ambient pedestrian: wanders around its home point, stops at the kerb
    /// and looks both ways before crossing, crosses briskly, avoids walls
    /// and other people, and gets knocked down by cars (adds a wanted star).
    /// Port of Pedestrian.gd, using the animated character model.
    /// </summary>
    public class Pedestrian : MonoBehaviour
    {
        enum State { Wander, WaitAtKerb, Crossing }

        public float walkSpeed = 1.5f;
        public float wanderRadius = 14f;
        public float lookAheadDistance = 1.8f;
        public float carAwarenessRange = 13f;
        public float safetyMarginSeconds = 3f;
        public float crossingSpeedBoost = 1.25f;
        public float obstacleSenseDistance = 1.4f;

        State _state = State.Wander;
        Vector3 _home, _target;
        bool _down;
        float _downTimer, _gravityVel, _lookTimer;
        CharacterController _cc;
        Animator _anim;
        Transform _visual;
        string _currentAnim = "";

        static readonly Color[] Tints =
        {
            new Color(1f, 1f, 1f), new Color(0.75f, 0.85f, 1f), new Color(1f, 0.85f, 0.7f), new Color(0.8f, 1f, 0.8f),
            new Color(1f, 0.75f, 0.75f), new Color(0.7f, 0.7f, 0.75f), new Color(1f, 1f, 0.75f), new Color(0.85f, 0.75f, 1f),
        };

        public static Pedestrian Spawn(Vector3 pos, Transform parent)
        {
            var go = new GameObject("Pedestrian");
            go.layer = Layers.Pedestrian; // doesn't physically collide with cars (no "brick wall" stops)
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.75f; cc.radius = 0.3f; cc.center = new Vector3(0, 0.88f, 0); cc.stepOffset = 0.4f;

            var p = go.AddComponent<Pedestrian>();

            // Trigger on the default layer, used to detect being hit by a car.
            var hitGo = new GameObject("HitZone");
            hitGo.transform.SetParent(go.transform, false);
            var rb = hitGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            var hit = hitGo.AddComponent<SphereCollider>();
            hit.isTrigger = true;
            hit.radius = 0.6f;
            hit.center = new Vector3(0, 0.9f, 0);
            hitGo.AddComponent<HitZone>().owner = p;

            p._cc = cc;
            p._visual = CharacterModel.Create(go.transform, Random.Range(1.62f, 1.86f), out p._anim, Tints[Random.Range(0, Tints.Length)]);
            p.walkSpeed = Random.Range(1.2f, 1.7f);
            return p;
        }

        void Start()
        {
            if (_cc == null) _cc = GetComponent<CharacterController>();
            _home = transform.position;
            PickNewTarget();
            if (_anim) _anim.Play("Walk", 0, Random.value);
        }

        void PickNewTarget()
        {
            float a = Random.value * Mathf.PI * 2f, d = Random.value * wanderRadius;
            _target = _home + new Vector3(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d);
        }

        bool OnRoad(Vector3 p)
        {
            var gm = GameManager.Instance;
            float half = gm.RoadWidth * 0.5f;
            foreach (float line in gm.RoadLines)
                if (Mathf.Abs(p.x - line) < half || Mathf.Abs(p.z - line) < half) return true;
            return false;
        }

        bool DangerApproaching(Vector3 crossingPoint)
        {
            foreach (var car in WorldCar.All)
            {
                if (car == null || car.Body == null || car.Body.isKinematic) continue;
                Vector3 v = car.Body.linearVelocity;
                float speed = v.magnitude;
                if (speed < 0.6f) continue;
                Vector3 toPed = crossingPoint - car.transform.position;
                toPed.y = 0;
                float dist = toPed.magnitude;
                if (dist > carAwarenessRange || dist < 0.01f) continue;
                float closing = Vector3.Dot(v, toPed / dist);
                if (closing <= 0.2f) continue;
                if (dist / closing < safetyMarginSeconds) return true;
            }
            return false;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_down)
            {
                _downTimer -= dt;
                if (_downTimer <= 0f)
                {
                    _down = false;
                    _visual.localRotation = Quaternion.identity;
                    _visual.localPosition = Vector3.zero;
                    _cc.enabled = true;
                    PickNewTarget();
                }
                return;
            }

            Vector3 toTarget = _target - transform.position;
            toTarget.y = 0;
            if (toTarget.magnitude < 0.7f) { PickNewTarget(); toTarget = _target - transform.position; toTarget.y = 0; }
            Vector3 dir = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.zero;
            Vector3 ahead = transform.position + dir * lookAheadDistance;

            float speed = walkSpeed;
            bool moving = true;
            switch (_state)
            {
                case State.WaitAtKerb:
                    moving = false;
                    if (!DangerApproaching(ahead)) _state = State.Crossing;
                    break;
                case State.Crossing:
                    if (DangerApproaching(transform.position + dir)) moving = false;
                    else
                    {
                        speed = walkSpeed * crossingSpeedBoost;
                        if (!OnRoad(transform.position)) _state = State.Wander;
                    }
                    break;
                default:
                    if (OnRoad(ahead) && !OnRoad(transform.position)) { _state = State.WaitAtKerb; moving = false; }
                    else if (dir != Vector3.zero && Physics.Raycast(transform.position + Vector3.up, dir, out var hit, obstacleSenseDistance, ~(1 << 2), QueryTriggerInteraction.Ignore) && hit.transform != transform)
                    {
                        PickNewTarget();
                        moving = false;
                    }
                    break;
            }

            Vector3 vel = Vector3.zero;
            if (moving && dir != Vector3.zero)
            {
                vel = dir * speed;
                float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0, Mathf.LerpAngle(transform.eulerAngles.y, yaw, Mathf.Clamp01(6f * dt)), 0);
            }
            else if (_state == State.WaitAtKerb)
            {
                // "Looking both ways" before crossing.
                _lookTimer += dt;
                float look = Mathf.Sin(_lookTimer * 2.2f) * 35f;
                _visual.localRotation = Quaternion.Euler(0, look, 0);
            }
            if (_state != State.WaitAtKerb) _visual.localRotation = Quaternion.Slerp(_visual.localRotation, Quaternion.identity, Mathf.Clamp01(6f * dt));

            _gravityVel = _cc.isGrounded ? -1f : _gravityVel - 18f * dt;
            vel.y = _gravityVel;
            _cc.Move(vel * dt);

            Play(moving ? "Walk" : "Idle");
        }

        void Play(string state)
        {
            if (_anim == null || _currentAnim == state) return;
            _currentAnim = state;
            _anim.CrossFadeInFixedTime(state, 0.25f);
            _anim.speed = state == "Walk" ? walkSpeed / 1.5f : 1f;
        }

        public void OnHit(Collider other)
        {
            if (_down) return;
            var car = other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<WorldCar>() : null;
            if (car == null || car.SpeedKmh < 6f) return;
            _down = true;
            _downTimer = 4f;
            _state = State.Wander;
            _cc.enabled = false;
            _visual.localRotation = Quaternion.Euler(-88f, 0, 0);
            _visual.localPosition = new Vector3(0, 0.25f, 0);
            Play("Idle");
            if (car.mode == WorldCar.Mode.PlayerDriven) GameManager.Instance.AddWanted(1);
        }

        public void SetSimulated(bool on)
        {
            enabled = on;
            if (_anim) _anim.enabled = on;
        }
    }

    /// <summary>Forwards trigger hits from the pedestrian's hit sphere.</summary>
    public class HitZone : MonoBehaviour
    {
        public Pedestrian owner;
        void OnTriggerEnter(Collider other) { if (owner) owner.OnHit(other); }
    }
}
