using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StreetRush
{
    /// <summary>
    /// Main menu (port of MainMenu.gd): title, New Game / Continue / Settings /
    /// Exit, language + graphics settings, and a slowly turning showcase car.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        RectTransform _main, _settings, _loading;
        Text _subtitle, _newLbl, _contLbl, _setLbl, _exitLbl, _setTitle, _langLbl, _gfxLbl, _backLbl, _loadingLbl;
        Button _continue;
        Text[] _gfxLabels = new Text[3];
        Image[] _gfxImages = new Image[3];
        Image _enImg, _arImg;
        Transform _showcase;

        void Start()
        {
            Time.timeScale = 1f;
            BuildShowcase();
            var canvas = UIFactory.CreateCanvas("MenuCanvas", 0);
            var root = canvas.transform;

            // Readability gradient on the left.
            var shade = UIFactory.Rect("Shade", root, new Vector2(0, 0), new Vector2(0.55f, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            var shadeImg = UIFactory.Image(shade, new Color(0, 0, 0, 0.55f));
            shadeImg.sprite = GradientSprite();

            _main = UIFactory.Stretch("Main", root);
            var title = UIFactory.Label("Title", _main, new Vector2(0.27f, 0.8f), Vector2.zero, new Vector2(640, 110), "STREET RUSH", 78, UIFactory.Gold);
            title.GetComponent<Shadow>().effectDistance = new Vector2(3, -3);
            _subtitle = UIFactory.Label("Subtitle", _main, new Vector2(0.27f, 0.8f), new Vector2(0, -64), new Vector2(640, 40), "", 22, new Color(0.85f, 0.87f, 0.92f));

            Vector2 a = new Vector2(0.27f, 0.5f), s = new Vector2(400, 70);
            UIFactory.MenuButton(_main, "NewGame", a, new Vector2(0, 40), s, "", OnNewGame, out _newLbl);
            _continue = UIFactory.MenuButton(_main, "Continue", a, new Vector2(0, -45), s, "", OnContinue, out _contLbl);
            UIFactory.MenuButton(_main, "Settings", a, new Vector2(0, -130), s, "", () => Show(_settings), out _setLbl);
            UIFactory.MenuButton(_main, "Exit", a, new Vector2(0, -215), s, "", Application.Quit, out _exitLbl);

            BuildSettings(root);

            _loading = UIFactory.Stretch("Loading", root);
            UIFactory.Image(_loading, new Color(0, 0, 0, 0.85f), null, true);
            _loadingLbl = UIFactory.Label("Text", _loading, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 80), "", 40, UIFactory.Gold);
            _loading.gameObject.SetActive(false);

            Localization.LanguageChanged += OnLanguage;
            OnLanguage(Localization.Current);
            Show(_main);
        }

        void OnDestroy() { Localization.LanguageChanged -= OnLanguage; }

        void BuildSettings(Transform root)
        {
            _settings = UIFactory.Stretch("Settings", root);
            var panel = UIFactory.Rect("Panel", _settings, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 520));
            UIFactory.Image(panel, new Color(0.07f, 0.08f, 0.12f, 0.95f), UIFactory.RoundedSprite, true);
            panel.gameObject.AddComponent<Outline>().effectColor = new Color(1f, 0.76f, 0.12f, 0.5f);
            _setTitle = UIFactory.Label("Title", panel, new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(560, 60), "", 42, UIFactory.Gold);

            _langLbl = UIFactory.Label("LangLabel", panel, new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(560, 40), "", 24, Color.white);
            var en = UIFactory.MenuButton(panel, "English", new Vector2(0.5f, 1), new Vector2(-130, -175), new Vector2(240, 60), "English", () => Localization.SetLanguage("en"), out _);
            var ar = UIFactory.MenuButton(panel, "Arabic", new Vector2(0.5f, 1), new Vector2(130, -175), new Vector2(240, 60), Localization.Display("العربية"), () => Localization.SetLanguage("ar"), out _);
            _enImg = en.GetComponent<Image>();
            _arImg = ar.GetComponent<Image>();

            _gfxLbl = UIFactory.Label("GfxLabel", panel, new Vector2(0.5f, 1), new Vector2(0, -250), new Vector2(560, 40), "", 24, Color.white);
            for (int i = 0; i < 3; i++)
            {
                int tier = i;
                var b = UIFactory.MenuButton(panel, "Gfx" + i, new Vector2(0.5f, 1), new Vector2(-185 + i * 185, -305), new Vector2(170, 60), "", () =>
                {
                    GraphicsQuality.Current = (GraphicsQuality.Tier)tier;
                    RefreshToggles();
                }, out _gfxLabels[i]);
                _gfxImages[i] = b.GetComponent<Image>();
            }

            UIFactory.MenuButton(panel, "Back", new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(300, 64), "", () => Show(_main), out _backLbl);
        }

        void RefreshToggles()
        {
            Color on = new Color(0.45f, 0.33f, 0.05f, 0.98f), off = new Color(0.12f, 0.13f, 0.18f, 0.92f);
            _enImg.color = Localization.IsRtl ? off : on;
            _arImg.color = Localization.IsRtl ? on : off;
            for (int i = 0; i < 3; i++) _gfxImages[i].color = (int)GraphicsQuality.Current == i ? on : off;
        }

        void Show(RectTransform panel)
        {
            _main.gameObject.SetActive(panel == _main);
            _settings.gameObject.SetActive(panel == _settings);
            RefreshContinue();
            RefreshToggles();
        }

        void RefreshContinue()
        {
            bool has = GameManager.Instance.HasSave();
            _continue.interactable = has;
            _contLbl.text = has ? Localization.T("btn_continue") : Localization.T("continue_no_save");
        }

        void OnLanguage(string _)
        {
            _subtitle.text = Localization.T("menu_subtitle");
            _newLbl.text = Localization.T("btn_new_game");
            _setLbl.text = Localization.T("btn_settings");
            _exitLbl.text = Localization.T("btn_exit");
            _setTitle.text = Localization.T("settings_title");
            _langLbl.text = Localization.T("settings_language");
            _gfxLbl.text = Localization.T("settings_graphics");
            _gfxLabels[0].text = Localization.T("gfx_low");
            _gfxLabels[1].text = Localization.T("gfx_high");
            _gfxLabels[2].text = Localization.T("gfx_ultra");
            _backLbl.text = Localization.T("settings_back");
            _loadingLbl.text = Localization.T("loading");
            RefreshContinue();
            if (_settings != null) RefreshToggles();
        }

        void OnNewGame()
        {
            GameManager.Instance.PendingContinue = false;
            StartCoroutine(LoadWorld());
        }

        void OnContinue()
        {
            if (!GameManager.Instance.HasSave()) return;
            GameManager.Instance.PendingContinue = true;
            StartCoroutine(LoadWorld());
        }

        IEnumerator LoadWorld()
        {
            _loading.gameObject.SetActive(true);
            yield return null;
            var op = SceneManager.LoadSceneAsync("World");
            while (!op.isDone) yield return null;
        }

        // ---------------- Showcase ----------------

        void BuildShowcase()
        {
            var car = CarFactory.BuildVisual(new Color(0.62f, 0.05f, 0.06f), false, out _, out _);
            car.transform.position = new Vector3(3.7f, 0, 1.2f);
            _showcase = car.transform;
        }

        void Update()
        {
            if (_showcase) _showcase.Rotate(0, 18f * Time.deltaTime, 0, Space.World);
        }

        static Sprite GradientSprite()
        {
            var tex = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < 64; x++) tex.SetPixel(x, 0, new Color(1, 1, 1, 1f - x / 63f));
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 64, 1), new Vector2(0.5f, 0.5f));
        }
    }
}
