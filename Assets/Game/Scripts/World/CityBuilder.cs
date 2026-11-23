using System.Collections.Generic;
using UnityEngine;

namespace StreetRush
{
    /// <summary>
    /// Runtime side of the Miami city (port of City.gd). The city model and
    /// its baked MeshColliders are already placed in the World scene by the
    /// editor tooling; this script finds street-level spawn points, spawns
    /// pedestrians, animals, traffic, police and a few parked cars, and does
    /// distance-based simulation culling for those actors.
    /// </summary>
    public class CityBuilder : MonoBehaviour
    {
        public static CityBuilder Instance { get; private set; }

        // Active play area (the "Boulevard" district). Godot X is mirrored in Unity.
        public static readonly Vector2 AreaMin = new Vector2(-240f, -70f);
        public static readonly Vector2 AreaMax = new Vector2(-10f, 350f);
        const float GridStep = 14f;

        public int pedestrianCount = 16;
        public int animalCount = 4;
        public int civilianCarCount = 8;
        public int policeCarCount = 2;
        public int parkedCarCount = 4;

        public float actorVisibleDistance = 190f;
        public float actorProcessDistance = 150f;

        public Vector3 PlayerSpawn { get; private set; } = new Vector3(-125f, 2.5f, 140f);

        readonly List<Vector3> _spawnPoints = new List<Vector3>();
        readonly List<Behaviour> _actors = new List<Behaviour>();
        float _cullTimer;

        void Awake()
        {
            Instance = this;
            GameManager.Instance.ResetOpenWorldState();
            GraphicsQuality.ApplyCrowdLimits(this);
            PlayerSpawn = GroundAt(PlayerSpawn, out var y) ? new Vector3(PlayerSpawn.x, y + 0.2f, PlayerSpawn.z) : PlayerSpawn;
            BuildSpawnPoints();

            var gm = GameManager.Instance;
            gm.RoadPoints.Clear();
            gm.RoadPoints.AddRange(_spawnPoints);
            gm.RoadLines.Clear();
            for (float x = AreaMin.x; x <= AreaMax.x; x += GridStep * 3f) gm.RoadLines.Add(x);
            for (float z = AreaMin.y; z <= AreaMax.y; z += GridStep * 3f) gm.RoadLines.Add(z);
            gm.RoadWidth = GridStep * 0.6f;

            SpawnParkedCars();
            SpawnTraffic();
            SpawnPedestrians();
            SpawnAnimals();
        }

        void OnDestroy() { if (Instance == this) Instance = null; RendererCache.Clear(); }

        static bool GroundAt(Vector3 p, out float y)
        {
            if (Physics.Raycast(new Vector3(p.x, 250f, p.z), Vector3.down, out var hit, 400f, ~((1 << Layers.Vehicle) | (1 << Layers.Pedestrian) | (1 << Layers.Player)), QueryTriggerInteraction.Ignore))
            {
                y = hit.point.y;
                return true;
            }
            y = p.y;
            return false;
        }

        void BuildSpawnPoints()
        {
            int mask = ~((1 << Layers.Vehicle) | (1 << Layers.Pedestrian) | (1 << Layers.Player));
            for (float x = AreaMin.x; x <= AreaMax.x; x += GridStep)
                for (float z = AreaMin.y; z <= AreaMax.y; z += GridStep)
                {
                    if (!Physics.Raycast(new Vector3(x, 250f, z), Vector3.down, out var hit, 400f, mask, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.point.y > -2f && hit.point.y < 3f && hit.normal.y > 0.7f) _spawnPoints.Add(hit.point);
                }
            if (_spawnPoints.Count == 0) _spawnPoints.Add(PlayerSpawn);
        }

        Vector3 RandomPoint() => _spawnPoints[Random.Range(0, _spawnPoints.Count)];

        /// <summary>Finds a spawn point with room for a car (not inside a building or another car).</summary>
        bool FreeCarSpot(Vector3 near, float radius, out Vector3 pos, out Quaternion rot)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                Vector3 p = radius > 0 ? near + new Vector3(Random.Range(-radius, radius), 0, Random.Range(-radius, radius)) : RandomPoint();
                if (!GroundAt(p, out float y)) continue;
                if (y < -2f || y > 3f) continue;
                p.y = y;
                rot = BestHeading(p);
                if (Physics.CheckBox(p + Vector3.up * 1.2f, new Vector3(1.3f, 0.9f, 2.6f), rot, ~0, QueryTriggerInteraction.Ignore)) continue;
                pos = p + Vector3.up * 0.35f;
                return true;
            }
            pos = Vector3.zero;
            rot = Quaternion.identity;
            return false;
        }

        /// <summary>Faces the car along the longest free direction, i.e. down the street, not into a wall.</summary>
        static Quaternion BestHeading(Vector3 p)
        {
            float bestDist = -1f, bestYaw = 0f;
            float offset = Random.Range(0, 4) * 90f;
            for (int i = 0; i < 8; i++)
            {
                float yaw = offset + i * 45f;
                var rot = Quaternion.Euler(0, yaw, 0);
                float d = Physics.BoxCast(p + Vector3.up * 1.1f, new Vector3(1f, 0.5f, 0.5f), rot * Vector3.forward, out var hit, rot, 60f, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : 60f;
                if (i % 2 == 1) d *= 0.8f; // prefer the grid directions
                if (d > bestDist) { bestDist = d; bestYaw = yaw; }
            }
            return Quaternion.Euler(0, bestYaw, 0);
        }

        void SpawnParkedCars()
        {
            var parent = new GameObject("ParkedCars").transform;
            for (int i = 0; i < parkedCarCount; i++)
            {
                if (!FreeCarSpot(PlayerSpawn, 7f + i * 4f, out var p, out var r)) continue;
                var car = WorldCar.Create(p, r, CarFactory.CivilianColors[Random.Range(0, CarFactory.CivilianColors.Length)], false, WorldCar.Mode.Parked);
                car.transform.SetParent(parent, true);
                _actors.Add(car);
            }
        }

        void SpawnTraffic()
        {
            var parent = new GameObject("Traffic").transform;
            for (int i = 0; i < civilianCarCount + policeCarCount; i++)
            {
                bool police = i >= civilianCarCount;
                if (!FreeCarSpot(Vector3.zero, 0f, out var p, out var r)) continue;
                var car = WorldCar.Create(p, r, CarFactory.CivilianColors[Random.Range(0, CarFactory.CivilianColors.Length)], police, WorldCar.Mode.CivilianWander);
                car.transform.SetParent(parent, true);
                _actors.Add(car);
            }
        }

        void SpawnPedestrians()
        {
            var parent = new GameObject("Pedestrians").transform;
            for (int i = 0; i < pedestrianCount; i++)
            {
                // Half of them around the player's start so the street feels alive immediately.
                Vector3 p = i < pedestrianCount / 2 ? PlayerSpawn + new Vector3(Random.Range(-25f, 25f), 0, Random.Range(-25f, 25f)) : RandomPoint();
                if (!GroundAt(p, out float y) || y < -2f || y > 3f) p = RandomPoint(); else p.y = y;
                _actors.Add(Pedestrian.Spawn(p + Vector3.up * 0.1f, parent));
            }
        }

        void SpawnAnimals()
        {
            var parent = new GameObject("Animals").transform;
            for (int i = 0; i < animalCount; i++)
            {
                Vector3 p = PlayerSpawn + new Vector3(Random.Range(-30f, 30f), 0, Random.Range(-30f, 30f));
                if (!GroundAt(p, out float y) || y < -2f || y > 3f) p = RandomPoint(); else p.y = y;
                _actors.Add(Animal.Spawn(p + Vector3.up * 0.1f, parent));
            }
        }

        void Update()
        {
            _cullTimer += Time.deltaTime;
            if (_cullTimer < 0.25f) return;
            _cullTimer = 0f;

            var cam = Camera.main;
            if (cam == null) return;
            Vector3 c = cam.transform.position;
            var gm = GameManager.Instance;

            foreach (var a in _actors)
            {
                if (a == null) continue;
                float d = Vector3.Distance(a.transform.position, c);
                bool show = d <= actorVisibleDistance;
                bool sim = d <= actorProcessDistance;

                if (a is WorldCar car)
                {
                    if (car == gm.ActiveVehicle) { car.SetSimulated(true); SetVisible(car.gameObject, true); continue; }
                    // Police keep chasing while the player is wanted.
                    if (car.isPolice && gm.WantedLevel > 0) { sim = true; show = d <= actorVisibleDistance * 1.5f; }
                    car.SetSimulated(sim);
                    SetVisible(car.gameObject, show);
                }
                else if (a is Pedestrian ped)
                {
                    ped.SetSimulated(sim);
                    SetVisible(ped.gameObject, show);
                }
                else if (a is Animal animal)
                {
                    animal.SetSimulated(sim);
                    SetVisible(animal.gameObject, show);
                }
            }
        }

        static readonly Dictionary<GameObject, Renderer[]> RendererCache = new Dictionary<GameObject, Renderer[]>();

        static void SetVisible(GameObject go, bool on)
        {
            if (!RendererCache.TryGetValue(go, out var rs) || rs == null)
            {
                rs = go.GetComponentsInChildren<Renderer>(true);
                RendererCache[go] = rs;
            }
            if (rs.Length == 0 || rs[0].enabled == on) return;
            foreach (var r in rs) if (r) r.enabled = on;
        }
    }
}
