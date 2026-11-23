using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StreetRush
{
    /// <summary>
    /// Scripted play-through used for automated testing: only active when the
    /// player is launched with "-autotest". Starts a new game, looks around,
    /// walks, gets into a car, drives, and saves screenshots plus a report
    /// to the folder given with "-shots &lt;dir&gt;", then quits.
    /// </summary>
    public class AutoTest : MonoBehaviour
    {
        string _dir;
        readonly StringBuilder _report = new StringBuilder();
        int _errors;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-autotest") < 0) return;
            var go = new GameObject("AutoTest");
            DontDestroyOnLoad(go);
            var t = go.AddComponent<AutoTest>();
            int i = Array.IndexOf(args, "-shots");
            t._dir = i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.Combine(Application.persistentDataPath, "shots");
            Directory.CreateDirectory(t._dir);
            Application.logMessageReceived += t.OnLog;
        }

        void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception)
            {
                _errors++;
                if (_errors < 30) _report.AppendLine($"[{type}] {msg}\n{stack}");
            }
        }

        void Log(string s)
        {
            Debug.Log("AUTOTEST " + s);
            _report.AppendLine(s);
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(_dir, name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
            Log($"shot {name} fps~{1f / Mathf.Max(0.0001f, Time.smoothDeltaTime):F0}");
        }

        IEnumerator Start()
        {
            Log($"device: {SystemInfo.graphicsDeviceName} / {SystemInfo.graphicsDeviceType} res {Screen.width}x{Screen.height} quality={GraphicsQuality.Current}");
            yield return new WaitForSeconds(2f);
            yield return Shot("01_main_menu");

            if (PlayerPrefs.GetString("language", "en") != "ar")
            {
                Localization.SetLanguage("ar");
                yield return new WaitForSeconds(0.5f);
                yield return Shot("02_main_menu_arabic");
                Localization.SetLanguage("en");
            }

            GameManager.Instance.PendingContinue = false;
            float t0 = Time.realtimeSinceStartup;
            yield return SceneManager.LoadSceneAsync("World");
            Log($"world loaded in {Time.realtimeSinceStartup - t0:F1}s");
            yield return new WaitForSeconds(3f);

            var player = FindFirstObjectByType<PlayerCharacter>();
            var gm = GameManager.Instance;
            Log($"player at {player.transform.position} roadPoints={gm.RoadPoints.Count} cars={WorldCar.All.Count} peds={FindObjectsByType<Pedestrian>(FindObjectsSortMode.None).Length}");
            yield return Shot("03_spawn");

            // Look around (camera orbit).
            for (int i = 0; i < 30; i++) { MobileInput.LookDelta += new Vector2(20f, 0); yield return null; }
            yield return new WaitForSeconds(0.5f);
            yield return Shot("04_look_around");

            // Walk then run forward.
            MobileInput.ExternalMove = new Vector2(0, 1);
            yield return new WaitForSeconds(1.5f);
            yield return Shot("05_walking");
            MobileInput.RunHeld = true;
            yield return new WaitForSeconds(1.5f);
            Vector3 before = player.transform.position;
            yield return new WaitForSeconds(1f);
            Log($"running moved {(player.transform.position - before).magnitude:F2} m in 1s");
            yield return Shot("06_running");
            MobileInput.ExternalMove = Vector2.zero;
            MobileInput.RunHeld = false;
            MobileInput.JumpPressed = true;
            yield return new WaitForSeconds(0.25f);
            yield return Shot("07_jump");
            yield return new WaitForSeconds(1f);

            // Enter the nearest car (teleport next to it like a player walking up).
            WorldCar nearest = null;
            float best = float.MaxValue;
            foreach (var c in WorldCar.All)
            {
                if (c.mode != WorldCar.Mode.Parked) continue;
                float d = Vector3.Distance(c.transform.position, player.transform.position);
                if (d < best) { best = d; nearest = c; }
            }
            if (nearest == null) { Log("ERROR no parked car found"); }
            else
            {
                player.Teleport(nearest.transform.position - nearest.transform.right * 2.4f);
                yield return new WaitForSeconds(0.5f);
                Log($"nearVehicle={gm.NearVehicle}");
                yield return Shot("08_near_car");
                MobileInput.InteractPressed = true;
                yield return new WaitForSeconds(0.5f);
                Log($"isDriving={gm.IsDriving}");
                yield return Shot("09_in_car");

                MobileInput.Throttle = 1f;
                yield return new WaitForSeconds(3f);
                Log($"speed after 3s full throttle: {gm.CurrentSpeedKmh:F0} km/h");
                yield return Shot("10_driving");
                MobileInput.Steer = 1f;
                yield return new WaitForSeconds(1.2f);
                yield return Shot("11_turning");
                MobileInput.Steer = 0f;
                MobileInput.Throttle = 0f;
                MobileInput.Brake = true;
                yield return new WaitForSeconds(2f);
                Log($"speed after braking 2s: {gm.CurrentSpeedKmh:F0} km/h");
                MobileInput.Brake = false;
                MobileInput.InteractPressed = true;
                yield return new WaitForSeconds(0.6f);
                Log($"after exit isDriving={gm.IsDriving}");
            }

            // Wanted level + police.
            gm.SetWantedLevel(2);
            yield return new WaitForSeconds(1f);
            yield return Shot("12_wanted");

            // Cinematic overview shots of the city.
            var cam = player.Cam;
            var pc = player.GetComponent<PlayerCharacter>();
            pc.enabled = false;
            Vector3 p = player.transform.position;
            cam.transform.SetPositionAndRotation(p + new Vector3(-60, 45, -60), Quaternion.LookRotation(new Vector3(60, -35, 60)));
            yield return new WaitForSeconds(0.5f);
            yield return Shot("13_city_aerial");
            cam.transform.SetPositionAndRotation(p + new Vector3(0, 6, -20), Quaternion.Euler(8, 30, 0));
            yield return new WaitForSeconds(0.5f);
            yield return Shot("14_street_view");
            cam.transform.SetPositionAndRotation(new Vector3(-120, 60, -300), Quaternion.Euler(12, 160, 0));
            yield return new WaitForSeconds(0.5f);
            yield return Shot("15_waterfront");

            Log($"errors logged: {_errors}");
            File.WriteAllText(Path.Combine(_dir, "report.txt"), _report.ToString());
            Application.Quit();
        }
    }
}
