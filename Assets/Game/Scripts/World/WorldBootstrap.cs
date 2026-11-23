using UnityEngine;

namespace StreetRush
{
    /// <summary>Entry point of the World scene (port of World.gd): player, HUD, save/continue, autosave.</summary>
    [DefaultExecutionOrder(100)]
    public class WorldBootstrap : MonoBehaviour
    {
        const float AutosaveInterval = 6f;
        float _autosaveTimer;
        PlayerCharacter _player;

        void Start()
        {
            var gm = GameManager.Instance;
            MobileInput.ResetAll();
            Time.timeScale = 1f;

            var city = CityBuilder.Instance;
            Vector3 spawn = city != null ? city.PlayerSpawn : new Vector3(-125f, 2f, 140f);
            _player = PlayerCharacter.Spawn(spawn);
            gm.PlayerRef = _player.transform;

            if (gm.PendingContinue) gm.LoadSaveInto(_player.transform);
            gm.PendingContinue = false;

            GameHUD.Create();
            GraphicsQuality.Apply();
        }

        void Update()
        {
            _autosaveTimer += Time.deltaTime;
            if (_autosaveTimer >= AutosaveInterval)
            {
                _autosaveTimer = 0f;
                GameManager.Instance.SaveGame();
            }
        }

        void OnApplicationPause(bool paused) { if (paused) GameManager.Instance.SaveGame(); }
        void OnApplicationQuit() { GameManager.Instance.SaveGame(); }
    }
}
