using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace StreetRush
{
    /// <summary>
    /// Global open-world state (port of the Godot GameManager autoload).
    /// Created automatically before the first scene loads and kept alive
    /// across scene changes.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public event Action<int> WantedLevelChanged;
        public event Action<WorldCar> EnteredVehicle;
        public event Action<WorldCar> ExitedVehicle;
        public event Action Busted;

        public bool IsDriving { get; private set; }
        public WorldCar ActiveVehicle { get; private set; }
        public bool NearVehicle { get; set; }
        public Transform PlayerRef { get; set; }

        public float Health { get; private set; } = 100f;
        public float MaxHealth => 100f;
        public int WantedLevel { get; private set; }
        float _wantedCooldown;

        /// <summary>Street-level points inside the active play area (filled by CityBuilder).</summary>
        public readonly List<Vector3> RoadPoints = new List<Vector3>();
        /// <summary>World X/Z coordinate of every approximate road centre line.</summary>
        public readonly List<float> RoadLines = new List<float>();
        public float RoadWidth = 9f;

        public bool PendingContinue { get; set; }
        public float CurrentSpeedKmh { get; set; }

        static string SavePath => Path.Combine(Application.persistentDataPath, "savegame.json");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("GameManager");
            Instance = go.AddComponent<GameManager>();
            DontDestroyOnLoad(go);

            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            Input.multiTouchEnabled = true;
            Localization.Load();
        }

        void Update()
        {
            if (WantedLevel > 0)
            {
                _wantedCooldown += Time.deltaTime;
                if (_wantedCooldown > 8f)
                {
                    _wantedCooldown = 0f;
                    SetWantedLevel(WantedLevel - 1);
                }
            }
        }

        // ---------- Open world API ----------

        public void OnEnterVehicle(WorldCar car)
        {
            IsDriving = true;
            ActiveVehicle = car;
            EnteredVehicle?.Invoke(car);
        }

        public void OnExitVehicle()
        {
            var car = ActiveVehicle;
            IsDriving = false;
            ActiveVehicle = null;
            CurrentSpeedKmh = 0f;
            ExitedVehicle?.Invoke(car);
        }

        public void AddWanted(int amount = 1) => SetWantedLevel(WantedLevel + amount);

        public void SetWantedLevel(int level)
        {
            level = Mathf.Clamp(level, 0, 5);
            if (level == WantedLevel) return;
            WantedLevel = level;
            _wantedCooldown = 0f;
            WantedLevelChanged?.Invoke(WantedLevel);
        }

        public void BustPlayer()
        {
            SetWantedLevel(0);
            Busted?.Invoke();
        }

        public void TakeDamage(float amount) => Health = Mathf.Clamp(Health - amount, 0f, MaxHealth);
        public void Heal(float amount) => Health = Mathf.Clamp(Health + amount, 0f, MaxHealth);

        public void ResetOpenWorldState()
        {
            IsDriving = false;
            ActiveVehicle = null;
            NearVehicle = false;
            Health = MaxHealth;
            WantedLevel = 0;
            _wantedCooldown = 0f;
            CurrentSpeedKmh = 0f;
            RoadPoints.Clear();
            RoadLines.Clear();
        }

        // ---------- Save / Continue ----------

        [Serializable]
        class SaveData
        {
            public float[] position;
            public float health;
            public int wanted_level;
        }

        public bool HasSave() => File.Exists(SavePath);

        public void SaveGame()
        {
            if (PlayerRef == null) return;
            var p = PlayerRef.position;
            var data = new SaveData { position = new[] { p.x, p.y, p.z }, health = Health, wanted_level = WantedLevel };
            try { File.WriteAllText(SavePath, JsonUtility.ToJson(data)); }
            catch (Exception e) { Debug.LogWarning($"GameManager: could not write save file ({e.Message})."); }
        }

        /// <summary>Applies the saved game to the given player; returns false when there is no usable save.</summary>
        public bool LoadSaveInto(Transform player)
        {
            if (!HasSave()) return false;
            try
            {
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                if (data == null) return false;
                if (data.position != null && data.position.Length == 3 && player != null)
                {
                    var cc = player.GetComponent<CharacterController>();
                    if (cc) cc.enabled = false;
                    player.position = new Vector3(data.position[0], data.position[1], data.position[2]);
                    if (cc) cc.enabled = true;
                }
                Health = Mathf.Clamp(data.health, 0f, MaxHealth);
                SetWantedLevel(data.wanted_level);
                return true;
            }
            catch (Exception) { return false; }
        }
    }
}
