using UnityEngine;

namespace StreetRush
{
    /// <summary>
    /// Third-person on-foot controller with a free orbit camera (drag on the
    /// right half of the screen), camera-relative movement, walking/running/
    /// jumping animation, and entering/exiting cars with a GTA-style chase
    /// camera while driving. Port of PlayerCharacter.gd.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerCharacter : MonoBehaviour
    {
        [Header("Movement")]
        public float walkSpeed = 3.2f;
        public float runSpeed = 6.8f;
        public float acceleration = 24f;
        public float deceleration = 32f;
        public float jumpVelocity = 6.5f;
        public float gravity = 18f;
        public float meshTurnSpeed = 10f;

        [Header("Camera")]
        public float lookSensitivity = 0.18f;   // degrees per pixel
        public float minPitch = -40f;
        public float maxPitch = 60f;
        public float defaultPitch = 12f;
        public float cameraSmoothing = 20f;
        public float onFootDistance = 3.6f;
        public float pivotHeight = 1.6f;

        [Header("Driving camera")]
        public float drivingFollowSpeed = 6f;
        public float drivingPitch = 14f;
        public float drivingDistance = 7f;
        public float drivingPivotHeight = 1.9f;
        public float reverseViewSpeedKmh = 4f;

        public Camera Cam { get; private set; }
        public Transform Visual { get; private set; }

        CharacterController _cc;
        Animator _anim;
        Vector3 _velocity;
        float _yaw, _pitch, _yawTarget, _pitchTarget;
        bool _drivingReversed;
        float _camDistance;
        string _currentAnim = "";
        WorldCar _nearbyCar;
        float _manualLookTimer;

        public static PlayerCharacter Spawn(Vector3 position)
        {
            var go = new GameObject("PlayerCharacter");
            go.layer = 2; // Ignore Raycast: camera and AI rays skip the player capsule
            go.transform.position = position;
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0, 0.9f, 0);
            cc.stepOffset = 0.45f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.04f;
            return go.AddComponent<PlayerCharacter>();
        }

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            Visual = CharacterModel.Create(transform, 1.8f, out _anim);

            var camGo = new GameObject("PlayerCamera");
            camGo.tag = "MainCamera";
            Cam = camGo.AddComponent<Camera>();
            Cam.fieldOfView = 62f;
            Cam.nearClipPlane = 0.15f;
            Cam.farClipPlane = 1400f;
            camGo.AddComponent<AudioListener>();
            GraphicsQuality.ConfigureCamera(Cam);

            _pitch = _pitchTarget = defaultPitch;
            _yaw = _yawTarget = transform.eulerAngles.y;
            _camDistance = onFootDistance;
        }

        void Start()
        {
            if (GameManager.Instance != null) GameManager.Instance.PlayerRef = transform;
            UpdateCamera(0f, true);
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            _nearbyCar = gm.IsDriving ? null : FindNearbyCar();
            gm.NearVehicle = _nearbyCar != null;

            if (MobileInput.InteractPressed || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F))
            {
                MobileInput.InteractPressed = false;
                if (gm.IsDriving) ExitCar();
                else if (_nearbyCar != null) EnterCar(_nearbyCar);
            }

            if (gm.IsDriving) DrivingUpdate();
            else OnFootUpdate();
        }

        void LateUpdate()
        {
            var gm = GameManager.Instance;
            bool driving = gm != null && gm.IsDriving;
            if (driving) DrivingCamera();
            else ReadLook();
            UpdateCamera(Time.deltaTime, false);
        }

        // ---------------- Camera ----------------

        void ReadLook()
        {
            Vector2 look = MobileInput.LookDelta;
            MobileInput.LookDelta = Vector2.zero;
            if (Input.GetMouseButton(1)) look += new Vector2(Input.GetAxis("Mouse X"), -Input.GetAxis("Mouse Y")) * 12f;
            _yawTarget += look.x * lookSensitivity;
            _pitchTarget = Mathf.Clamp(_pitchTarget + look.y * lookSensitivity, minPitch, maxPitch);
        }

        void DrivingCamera()
        {
            var gm = GameManager.Instance;
            var car = gm.ActiveVehicle;
            if (car == null) return;

            if (MobileInput.LookDelta.sqrMagnitude > 0.0001f)
            {
                _manualLookTimer = 1.2f;
                ReadLook();
                return;
            }
            if (_manualLookTimer > 0f) { _manualLookTimer -= Time.deltaTime; return; }

            float fwd = car.ForwardSpeed;
            if (Mathf.Abs(fwd) * 3.6f > reverseViewSpeedKmh) _drivingReversed = fwd < 0f;
            float target = car.transform.eulerAngles.y + (_drivingReversed ? 180f : 0f);
            _yawTarget = Mathf.LerpAngle(_yawTarget, target, Mathf.Clamp01(drivingFollowSpeed * Time.deltaTime));
            // Pull the camera back a little at speed for a sense of velocity.
            _pitchTarget = Mathf.Lerp(_pitchTarget, drivingPitch, Mathf.Clamp01(2f * Time.deltaTime));
        }

        void UpdateCamera(float dt, bool snap)
        {
            var gm = GameManager.Instance;
            bool driving = gm != null && gm.IsDriving && gm.ActiveVehicle != null;

            float t = snap || cameraSmoothing <= 0 ? 1f : Mathf.Clamp01(cameraSmoothing * dt);
            _yaw = Mathf.LerpAngle(_yaw, _yawTarget, t);
            _pitch = Mathf.Lerp(_pitch, _pitchTarget, t);

            float speedKmh = driving ? gm.ActiveVehicle.SpeedKmh : 0f;
            float wantedDistance = driving ? drivingDistance + Mathf.Clamp01(speedKmh / 160f) * 2.2f : onFootDistance;
            Vector3 pivot = driving
                ? gm.ActiveVehicle.transform.position + Vector3.up * drivingPivotHeight
                : transform.position + Vector3.up * pivotHeight;

            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 dir = rot * Vector3.back;

            // Spring arm: keep the camera in front of walls.
            float dist = wantedDistance;
            var hits = Physics.SphereCastAll(pivot, 0.28f, dir, wantedDistance, ~(1 << 2), QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (driving && h.rigidbody != null && h.rigidbody == gm.ActiveVehicle.Body) continue;
                if (h.distance > 0f && h.distance < dist) dist = Mathf.Max(0.4f, h.distance - 0.1f);
            }
            _camDistance = snap ? dist : (dist < _camDistance ? dist : Mathf.Lerp(_camDistance, dist, Mathf.Clamp01(6f * dt)));

            Cam.transform.SetPositionAndRotation(pivot + dir * _camDistance, rot);
            float fovTarget = driving ? 62f + Mathf.Clamp01(speedKmh / 170f) * 12f : 62f;
            Cam.fieldOfView = snap ? fovTarget : Mathf.Lerp(Cam.fieldOfView, fovTarget, Mathf.Clamp01(3f * dt));
        }

        // ---------------- On foot ----------------

        void OnFootUpdate()
        {
            float dt = Time.deltaTime;
            bool running = MobileInput.RunHeld || Input.GetKey(KeyCode.LeftShift);
            Vector2 input = ReadMoveInput();
            if (input.magnitude > 1f) input.Normalize();

            Vector3 camFwd = Quaternion.Euler(0, _yaw, 0) * Vector3.forward;
            Vector3 camRight = Quaternion.Euler(0, _yaw, 0) * Vector3.right;
            Vector3 moveDir = camRight * input.x + camFwd * input.y;
            bool moving = moveDir.magnitude > 0.05f;
            if (moving) moveDir.Normalize();

            float speed = (running ? runSpeed : walkSpeed) * (moving ? Mathf.Clamp01(input.magnitude * 1.4f) : 0f);
            Vector3 targetVel = moveDir * speed;
            float accel = moving ? acceleration : deceleration;
            _velocity.x = Mathf.MoveTowards(_velocity.x, targetVel.x, accel * dt);
            _velocity.z = Mathf.MoveTowards(_velocity.z, targetVel.z, accel * dt);

            bool grounded = _cc.isGrounded;
            bool jumping = false;
            if (grounded)
            {
                _velocity.y = -2f;
                if (MobileInput.JumpPressed || Input.GetKeyDown(KeyCode.Space))
                {
                    _velocity.y = jumpVelocity;
                    jumping = true;
                }
            }
            else _velocity.y -= gravity * dt;
            MobileInput.JumpPressed = false;

            // Face the movement direction, or the camera direction when idle.
            float targetYaw = moving ? Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg : Visual.eulerAngles.y;
            if (moving)
            {
                float y = Mathf.LerpAngle(Visual.eulerAngles.y, targetYaw, Mathf.Clamp01(meshTurnSpeed * dt));
                Visual.rotation = Quaternion.Euler(0, y, 0);
            }

            _cc.Move(_velocity * dt);

            // Respawn if the player somehow falls through the world.
            if (transform.position.y < -30f) Teleport(CityBuilder.Instance != null ? CityBuilder.Instance.PlayerSpawn : Vector3.up * 3f);

            float hSpeed = new Vector2(_velocity.x, _velocity.z).magnitude;
            if (jumping || !grounded && _velocity.y > 1f) Play("Jump", 0.05f);
            else if (!grounded && _velocity.y < -6f) Play("Jump", 0.2f);
            else if (!moving || hSpeed < 0.3f) Play("Idle", 0.2f);
            else if (running && hSpeed > walkSpeed + 0.5f) Play("Run", 0.2f);
            else Play("Walk", 0.2f);
        }

        Vector2 ReadMoveInput()
        {
            Vector2 kb = new Vector2(
                (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0),
                (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0));
            return kb.sqrMagnitude > 0.01f ? kb : MobileInput.MoveVector;
        }

        void Play(string state, float blend)
        {
            if (_anim == null || _currentAnim == state) return;
            _currentAnim = state;
            _anim.CrossFadeInFixedTime(state, blend);
        }

        public void Teleport(Vector3 pos)
        {
            _cc.enabled = false;
            transform.position = pos;
            _cc.enabled = true;
            _velocity = Vector3.zero;
        }

        // ---------------- Cars ----------------

        WorldCar FindNearbyCar()
        {
            WorldCar best = null;
            float bestDist = 3.4f;
            foreach (var car in WorldCar.All)
            {
                if (car == null || car.mode == WorldCar.Mode.PolicePursuit) continue;
                float d = Vector3.Distance(transform.position, car.transform.position);
                if (d < bestDist) { bestDist = d; best = car; }
            }
            return best;
        }

        void DrivingUpdate()
        {
            var car = GameManager.Instance.ActiveVehicle;
            if (car == null) { ExitCar(); return; }
            transform.position = car.transform.position;
        }

        public void EnterCar(WorldCar car)
        {
            var gm = GameManager.Instance;
            gm.OnEnterVehicle(car);
            car.SetDriver(true);
            car.SetSimulated(true);
            Visual.gameObject.SetActive(false);
            _cc.enabled = false;
            _yaw = _yawTarget = car.transform.eulerAngles.y;
            _pitch = _pitchTarget = drivingPitch;
            _drivingReversed = false;
            _manualLookTimer = 0f;
        }

        public void ExitCar()
        {
            var gm = GameManager.Instance;
            var car = gm.ActiveVehicle;
            Vector3 pos = transform.position;
            if (car != null)
            {
                car.SetDriver(false);
                pos = car.transform.position - car.transform.right * 2.3f + Vector3.up * 0.3f;
                Visual.rotation = Quaternion.Euler(0, car.transform.eulerAngles.y, 0);
            }
            transform.position = pos;
            Visual.gameObject.SetActive(true);
            _cc.enabled = true;
            _velocity = Vector3.zero;
            _currentAnim = "";
            gm.OnExitVehicle();
            _pitchTarget = defaultPitch;
        }
    }
}
