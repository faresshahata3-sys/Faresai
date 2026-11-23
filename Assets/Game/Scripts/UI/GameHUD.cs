using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

namespace StreetRush
{
    /// <summary>
    /// In-game UI: radar minimap, health, wanted stars, speedometer, touch
    /// controls (joystick/run/jump on foot; steer/gas/brake/reverse driving),
    /// enter/exit button, pause menu and the "BUSTED" banner.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        Canvas _canvas;
        RectTransform _onFoot, _driving, _pausePanel;
        TouchButton _interact;
        Image _interactIcon;
        Sprite _iconEnter, _iconExit;
        VirtualJoystick _joystick;
        Text _speedText, _unitText, _wantedText, _healthLabel, _bustedText, _pauseTitle, _resumeLabel, _menuLabel;
        Image _healthFill, _speedFill;
        RectTransform _speedPanel;
        bool _leftHeld, _rightHeld;
        float _bustedTimer;

        Camera _radarCam;
        RenderTexture _radarRT;
        RectTransform _radarArrow, _radarMap;
        int _radarFrame;
        PlayerCharacter _player;

        public static GameHUD Create()
        {
            var go = new GameObject("GameHUD");
            return go.AddComponent<GameHUD>();
        }

        void Start()
        {
            _canvas = UIFactory.CreateCanvas("HUDCanvas", 10);
            _canvas.transform.SetParent(transform, false);
            var root = _canvas.transform;

            // Camera look surface goes first so every control sits on top of it.
            var look = UIFactory.Stretch("LookArea", root);
            UIFactory.Image(look, new Color(0, 0, 0, 0), null, true);
            look.gameObject.AddComponent<CameraLookArea>();

            BuildRadar(root);
            BuildStatus(root);
            BuildSpeedometer(root);
            BuildOnFoot(root);
            BuildDriving(root);

            _iconEnter = UIFactory.Icon("btn_door_enter");
            _iconExit = UIFactory.Icon("btn_door_exit");
            _interact = UIFactory.RoundButton(root, "Interact", new Vector2(1, 0.5f), new Vector2(-90, 120), 112, _iconEnter, "E");
            _interactIcon = _interact.transform.Find("Icon")?.GetComponent<Image>();
            _interact.Down += () => MobileInput.InteractPressed = true;

            var pause = UIFactory.RoundButton(root, "Pause", new Vector2(1, 1), new Vector2(-48, -48), 64, null, "II");
            pause.Down += () => SetPaused(true);

            BuildPauseMenu(root);

            _bustedText = UIFactory.Label("Busted", root, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(900, 140), "", 92, new Color(1f, 0.25f, 0.2f));
            _bustedText.gameObject.SetActive(false);

            var gm = GameManager.Instance;
            gm.WantedLevelChanged += OnWantedChanged;
            gm.Busted += OnBusted;
            Localization.LanguageChanged += OnLanguage;
            OnWantedChanged(gm.WantedLevel);
            OnLanguage(Localization.Current);
        }

        void OnDestroy()
        {
            var gm = GameManager.Instance;
            if (gm != null) { gm.WantedLevelChanged -= OnWantedChanged; gm.Busted -= OnBusted; }
            Localization.LanguageChanged -= OnLanguage;
            if (_radarRT) _radarRT.Release();
            Time.timeScale = 1f;
        }

        // ---------------- Builders ----------------

        void BuildRadar(Transform root)
        {
            const float size = 210f;
            var frame = UIFactory.Rect("Radar", root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -22), new Vector2(size + 8, size + 8));
            UIFactory.Image(frame, new Color(0, 0, 0, 0.65f), UIFactory.RoundSprite);
            var maskRt = UIFactory.Rect("Mask", frame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            var maskImg = UIFactory.Image(maskRt, Color.white, UIFactory.RoundSprite);
            maskRt.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            _radarMap = UIFactory.Rect("Map", maskRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 1.45f, size * 1.45f));
            var raw = _radarMap.gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            raw.color = new Color(0.85f, 0.9f, 1f, 1f);

            _radarArrow = UIFactory.Rect("Player", maskRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26, 26));
            var arrow = UIFactory.Text(_radarArrow, "▲", 26, UIFactory.Gold);
            arrow.GetComponent<Shadow>().effectDistance = new Vector2(1, -1);
            UIFactory.Label("N", frame, new Vector2(0.5f, 1f), new Vector2(0, -14), new Vector2(30, 24), "N", 16, Color.white);

            _radarRT = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32) { name = "Radar" };
            raw.texture = _radarRT;
            var camGo = new GameObject("RadarCamera");
            camGo.transform.SetParent(transform, false);
            _radarCam = camGo.AddComponent<Camera>();
            _radarCam.orthographic = true;
            _radarCam.orthographicSize = 85f;
            _radarCam.nearClipPlane = 1f;
            _radarCam.farClipPlane = 400f;
            _radarCam.cullingMask = ~((1 << Layers.Props) | (1 << Layers.Pedestrian) | (1 << Layers.Player));
            _radarCam.targetTexture = _radarRT;
            _radarCam.clearFlags = CameraClearFlags.SolidColor;
            _radarCam.backgroundColor = new Color(0.1f, 0.25f, 0.4f);
            _radarCam.enabled = false;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.requiresDepthTexture = false;
            data.requiresColorTexture = false;
            data.antialiasing = AntialiasingMode.None;
        }

        void BuildStatus(Transform root)
        {
            // Health bar under the radar.
            var hp = UIFactory.Rect("Health", root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -246), new Vector2(210, 18));
            UIFactory.Image(hp, new Color(0, 0, 0, 0.6f), UIFactory.RoundedSprite);
            var fillRt = UIFactory.Rect("Fill", hp, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(-6, -6));
            fillRt.anchoredPosition = new Vector2(3, 0);
            _healthFill = UIFactory.Image(fillRt, new Color(0.3f, 0.85f, 0.35f), UIFactory.RoundedSprite);
            _healthLabel = UIFactory.Label("HealthLabel", hp, new Vector2(0, 0.5f), new Vector2(105, -20), new Vector2(210, 20), "", 14, new Color(1, 1, 1, 0.85f));

            _wantedText = UIFactory.Label("Wanted", root, new Vector2(1, 1), new Vector2(-260, -46), new Vector2(330, 60), "", 44, Color.white, TextAnchor.MiddleRight);
        }

        void BuildSpeedometer(Transform root)
        {
            _speedPanel = UIFactory.Rect("Speed", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(230, 100));
            UIFactory.Image(_speedPanel, UIFactory.Panel, UIFactory.RoundedSprite);
            _speedText = UIFactory.Label("Value", _speedPanel, new Vector2(0.5f, 0.6f), new Vector2(-30, 0), new Vector2(150, 70), "0", 56, Color.white, TextAnchor.MiddleRight);
            _unitText = UIFactory.Label("Unit", _speedPanel, new Vector2(0.5f, 0.6f), new Vector2(96, -12), new Vector2(80, 30), "km/h", 18, UIFactory.Gold, TextAnchor.MiddleLeft);
            var bar = UIFactory.Rect("Bar", _speedPanel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(190, 8));
            UIFactory.Image(bar, new Color(1, 1, 1, 0.15f), UIFactory.RoundedSprite);
            var fill = UIFactory.Rect("Fill", bar, Vector2.zero, new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            _speedFill = UIFactory.Image(fill, UIFactory.Gold);
        }

        void BuildOnFoot(Transform root)
        {
            _onFoot = UIFactory.Stretch("OnFoot", root);

            // Joystick zone: lower-left 40% of the screen.
            var zone = UIFactory.Rect("JoystickZone", _onFoot, new Vector2(0, 0), new Vector2(0.4f, 0.62f), new Vector2(0, 0), Vector2.zero, Vector2.zero);
            UIFactory.Image(zone, new Color(0, 0, 0, 0), null, true);
            var ring = UIFactory.Rect("Base", zone, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0.5f, 0.5f), new Vector2(170, 160), new Vector2(200, 200));
            UIFactory.Image(ring, new Color(0, 0, 0, 0.35f), UIFactory.RoundSprite).gameObject.AddComponent<Outline>().effectColor = new Color(1, 1, 1, 0.3f);
            var knob = UIFactory.Rect("Knob", zone, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0.5f, 0.5f), new Vector2(170, 160), new Vector2(88, 88));
            UIFactory.Image(knob, new Color(1, 1, 1, 0.75f), UIFactory.RoundSprite);
            _joystick = zone.gameObject.AddComponent<VirtualJoystick>();
            _joystick.baseRing = ring;
            _joystick.knob = knob;

            var run = UIFactory.RoundButton(_onFoot, "Run", new Vector2(1, 0), new Vector2(-230, 110), 118, null, "RUN");
            run.Down += () => MobileInput.RunHeld = true;
            run.Up += () => MobileInput.RunHeld = false;
            var jump = UIFactory.RoundButton(_onFoot, "Jump", new Vector2(1, 0), new Vector2(-100, 210), 104, null, "JUMP");
            jump.Down += () => MobileInput.JumpPressed = true;
        }

        void BuildDriving(Transform root)
        {
            _driving = UIFactory.Stretch("Driving", root);
            var left = UIFactory.RoundButton(_driving, "Left", new Vector2(0, 0), new Vector2(100, 110), 128, UIFactory.Icon("btn_arrow_left"), "<");
            var right = UIFactory.RoundButton(_driving, "Right", new Vector2(0, 0), new Vector2(250, 110), 128, UIFactory.Icon("btn_arrow_right"), ">");
            left.Down += () => { _leftHeld = true; UpdateSteer(); };
            left.Up += () => { _leftHeld = false; UpdateSteer(); };
            right.Down += () => { _rightHeld = true; UpdateSteer(); };
            right.Up += () => { _rightHeld = false; UpdateSteer(); };

            var gas = UIFactory.RoundButton(_driving, "Gas", new Vector2(1, 0), new Vector2(-100, 120), 140, UIFactory.Icon("btn_gas"), "GAS");
            gas.Down += () => MobileInput.Throttle = 1f;
            gas.Up += () => MobileInput.Throttle = 0f;
            var brake = UIFactory.RoundButton(_driving, "Brake", new Vector2(1, 0), new Vector2(-250, 90), 116, UIFactory.Icon("btn_brake"), "BRK");
            brake.Down += () => MobileInput.Brake = true;
            brake.Up += () => MobileInput.Brake = false;
            var rev = UIFactory.RoundButton(_driving, "Reverse", new Vector2(1, 0), new Vector2(-250, 220), 96, UIFactory.Icon("btn_reverse"), "R");
            rev.Down += () => MobileInput.Reverse = true;
            rev.Up += () => MobileInput.Reverse = false;
        }

        void BuildPauseMenu(Transform root)
        {
            _pausePanel = UIFactory.Stretch("Pause", root);
            UIFactory.Image(_pausePanel, new Color(0, 0, 0, 0.7f), null, true);
            _pauseTitle = UIFactory.Label("Title", _pausePanel, new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(600, 90), "", 60, UIFactory.Gold);
            UIFactory.MenuButton(_pausePanel, "Resume", new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(360, 70), "", () => SetPaused(false), out _resumeLabel);
            UIFactory.MenuButton(_pausePanel, "Menu", new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(360, 70), "", () =>
            {
                GameManager.Instance.SaveGame();
                Time.timeScale = 1f;
                SceneManager.LoadScene("MainMenu");
            }, out _menuLabel);
            _pausePanel.gameObject.SetActive(false);
        }

        // ---------------- Runtime ----------------

        void UpdateSteer()
        {
            MobileInput.Steer = _leftHeld && !_rightHeld ? -1f : _rightHeld && !_leftHeld ? 1f : 0f;
        }

        void SetPaused(bool paused)
        {
            _pausePanel.gameObject.SetActive(paused);
            Time.timeScale = paused ? 0f : 1f;
            if (paused) GameManager.Instance.SaveGame();
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || _canvas == null) return;
            bool driving = gm.IsDriving;

            if (_onFoot.gameObject.activeSelf == driving) _onFoot.gameObject.SetActive(!driving);
            if (_driving.gameObject.activeSelf != driving) _driving.gameObject.SetActive(driving);
            MobileInput.MoveVector = driving ? Vector2.zero : (_joystick.Output != Vector2.zero ? _joystick.Output : MobileInput.ExternalMove);

            bool showInteract = driving || gm.NearVehicle;
            if (_interact.gameObject.activeSelf != showInteract) _interact.gameObject.SetActive(showInteract);
            if (_interactIcon) _interactIcon.sprite = driving ? _iconExit : _iconEnter;

            _speedPanel.gameObject.SetActive(driving);
            if (driving)
            {
                float s = gm.CurrentSpeedKmh;
                _speedText.text = Mathf.RoundToInt(s).ToString();
                _speedFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(s / 180f), 1);
            }

            float h = gm.Health / gm.MaxHealth;
            _healthFill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.001f, h), 1);
            _healthFill.color = Color.Lerp(new Color(0.9f, 0.2f, 0.15f), new Color(0.3f, 0.85f, 0.35f), h);

            if (_bustedTimer > 0f)
            {
                _bustedTimer -= Time.unscaledDeltaTime;
                if (_bustedTimer <= 0f) _bustedText.gameObject.SetActive(false);
            }

            if (Input.GetKeyDown(KeyCode.Escape)) SetPaused(!_pausePanel.gameObject.activeSelf);

            UpdateRadar(gm);
        }

        void UpdateRadar(GameManager gm)
        {
            if (_radarCam == null || gm.PlayerRef == null) return;
            if (_player == null) _player = FindFirstObjectByType<PlayerCharacter>();
            var player = _player;
            Transform focus = gm.IsDriving && gm.ActiveVehicle != null ? gm.ActiveVehicle.transform : gm.PlayerRef;
            float heading = gm.IsDriving && gm.ActiveVehicle != null ? gm.ActiveVehicle.transform.eulerAngles.y
                : (player != null ? player.Visual.eulerAngles.y : 0f);
            _radarCam.orthographicSize = gm.IsDriving ? 120f : 70f;
            _radarCam.transform.SetPositionAndRotation(focus.position + Vector3.up * 250f, Quaternion.Euler(90f, 0f, 0f));
            _radarArrow.localRotation = Quaternion.Euler(0, 0, -heading);
            // North-up radar rendered every 3rd frame (cheap on phones).
            if (++_radarFrame % 3 == 0) _radarCam.Render();
        }

        void OnWantedChanged(int level)
        {
            _wantedText.text = new string('★', level) + "<color=#ffffff40>" + new string('★', 5 - level) + "</color>";
            _wantedText.supportRichText = true;
            _wantedText.color = level > 0 ? UIFactory.Gold : new Color(1, 1, 1, 0.25f);
        }

        void OnBusted()
        {
            _bustedText.text = Localization.T("hud_busted");
            _bustedText.gameObject.SetActive(true);
            _bustedTimer = 3f;
            var player = FindFirstObjectByType<PlayerCharacter>();
            if (player != null && CityBuilder.Instance != null) player.Teleport(CityBuilder.Instance.PlayerSpawn);
        }

        void OnLanguage(string lang)
        {
            _unitText.text = Localization.T("hud_speed_unit");
            _healthLabel.text = Localization.T("hud_health");
            _pauseTitle.text = Localization.T("hud_pause");
            _resumeLabel.text = Localization.T("hud_resume");
            _menuLabel.text = Localization.T("hud_main_menu");
        }
    }
}
