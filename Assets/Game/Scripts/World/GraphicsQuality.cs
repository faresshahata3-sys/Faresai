using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StreetRush
{
    /// <summary>
    /// Three graphics presets (Low / High / Ultra) chosen from Settings and
    /// saved in PlayerPrefs. Ultra is the default on capable phones.
    /// </summary>
    public static class GraphicsQuality
    {
        public enum Tier { Low = 0, High = 1, Ultra = 2 }
        const string PrefKey = "gfx_tier";

        public static Tier Current
        {
            get
            {
                if (PlayerPrefs.HasKey(PrefKey)) return (Tier)Mathf.Clamp(PlayerPrefs.GetInt(PrefKey), 0, 2);
                // First launch: pick from the device's memory size.
                int mem = SystemInfo.systemMemorySize;
                return mem >= 6000 ? Tier.Ultra : mem >= 3500 ? Tier.High : Tier.Low;
            }
            set
            {
                PlayerPrefs.SetInt(PrefKey, (int)value);
                PlayerPrefs.Save();
                Apply();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Apply()
        {
            var tier = Current;
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
            {
                switch (tier)
                {
                    case Tier.Low:
                        urp.renderScale = 0.75f;
                        urp.shadowDistance = 45f;
                        urp.msaaSampleCount = 1;
                        break;
                    case Tier.High:
                        urp.renderScale = 0.9f;
                        urp.shadowDistance = 90f;
                        urp.msaaSampleCount = 2;
                        break;
                    default:
                        urp.renderScale = 1f;
                        urp.shadowDistance = 140f;
                        urp.msaaSampleCount = 4;
                        break;
                }
            }
            QualitySettings.lodBias = tier == Tier.Low ? 0.7f : tier == Tier.High ? 1.2f : 2f;

            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (cam.targetTexture == null) ConfigureCamera(cam);
        }

        public static void ConfigureCamera(Camera cam)
        {
            var tier = Current;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = tier != Tier.Low;
            data.antialiasing = tier == Tier.Ultra ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.FastApproximateAntialiasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderShadows = true;
            data.dithering = true;

            float propsCull = tier == Tier.Low ? 140f : tier == Tier.High ? 220f : 320f;
            var dists = new float[32];
            dists[Layers.Props] = propsCull;
            dists[Layers.Pedestrian] = tier == Tier.Low ? 90f : 160f;
            cam.layerCullDistances = dists;
            cam.farClipPlane = tier == Tier.Low ? 700f : 1400f;
        }

        public static void ApplyCrowdLimits(CityBuilder city)
        {
            switch (Current)
            {
                case Tier.Low:
                    city.pedestrianCount = 8; city.civilianCarCount = 5; city.animalCount = 2;
                    break;
                case Tier.High:
                    city.pedestrianCount = 14; city.civilianCarCount = 8;
                    break;
                default:
                    city.pedestrianCount = 22; city.civilianCarCount = 11;
                    break;
            }
        }
    }
}
