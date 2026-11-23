using UnityEngine;

namespace StreetRush
{
    /// <summary>Small wandering dog built from primitives, with a trotting bob. Port of Animal.gd.</summary>
    public class Animal : MonoBehaviour
    {
        public float wanderRadius = 10f;
        public float speed = 2.2f;

        Vector3 _home, _target;
        CharacterController _cc;
        Transform _meshRoot, _tail;
        float _gravityVel, _phase;

        public static Animal Spawn(Vector3 pos, Transform parent)
        {
            var go = new GameObject("Dog");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var cc = go.AddComponent<CharacterController>();
            cc.height = 0.55f; cc.radius = 0.2f; cc.center = new Vector3(0, 0.3f, 0); cc.stepOffset = 0.2f;

            var a = go.AddComponent<Animal>();
            a._cc = cc;
            a._meshRoot = new GameObject("MeshRoot").transform;
            a._meshRoot.SetParent(go.transform, false);

            Color[] furs = { new Color(0.45f, 0.31f, 0.18f), new Color(0.12f, 0.1f, 0.09f), new Color(0.85f, 0.75f, 0.55f), new Color(0.6f, 0.6f, 0.6f) };
            var fur = new Material(CarFactory.Mat("Skin")) { name = "Fur" };
            fur.SetColor("_BaseColor", furs[Random.Range(0, furs.Length)]);

            Part(a._meshRoot, PrimitiveType.Capsule, new Vector3(0, 0.34f, 0), new Vector3(0.26f, 0.32f, 0.26f), Quaternion.Euler(90, 0, 0), fur);
            Part(a._meshRoot, PrimitiveType.Sphere, new Vector3(0, 0.48f, 0.34f), new Vector3(0.2f, 0.2f, 0.24f), Quaternion.identity, fur);
            Part(a._meshRoot, PrimitiveType.Sphere, new Vector3(0, 0.45f, 0.46f), new Vector3(0.1f, 0.09f, 0.14f), Quaternion.identity, fur);
            foreach (float sx in new[] { -1f, 1f })
            {
                Part(a._meshRoot, PrimitiveType.Cube, new Vector3(sx * 0.07f, 0.6f, 0.3f), new Vector3(0.05f, 0.1f, 0.04f), Quaternion.Euler(0, 0, sx * 15f), fur);
                foreach (float sz in new[] { -0.18f, 0.18f })
                    Part(a._meshRoot, PrimitiveType.Cylinder, new Vector3(sx * 0.08f, 0.12f, sz), new Vector3(0.06f, 0.12f, 0.06f), Quaternion.identity, fur);
            }
            a._tail = Part(a._meshRoot, PrimitiveType.Cylinder, new Vector3(0, 0.45f, -0.3f), new Vector3(0.035f, 0.12f, 0.035f), Quaternion.Euler(-40, 0, 0), fur).transform;
            return a;
        }

        static GameObject Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        void Start()
        {
            _home = transform.position;
            PickNewTarget();
            _phase = Random.value * 10f;
        }

        void PickNewTarget()
        {
            float a = Random.value * Mathf.PI * 2f, d = Random.value * wanderRadius;
            _target = _home + new Vector3(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector3 to = _target - transform.position;
            to.y = 0;
            if (to.magnitude < 0.6f) { PickNewTarget(); to = _target - transform.position; to.y = 0; }
            Vector3 dir = to.normalized;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, Mathf.LerpAngle(transform.eulerAngles.y, yaw, Mathf.Clamp01(6f * dt)), 0);

            _gravityVel = _cc.isGrounded ? -1f : _gravityVel - 18f * dt;
            _cc.Move((dir * speed + Vector3.up * _gravityVel) * dt);

            _phase += dt;
            _meshRoot.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(_phase * 9f)) * 0.06f, 0);
            if (_tail) _tail.localRotation = Quaternion.Euler(-40, Mathf.Sin(_phase * 14f) * 30f, 0);
        }

        public void SetSimulated(bool on) => enabled = on;
    }
}
