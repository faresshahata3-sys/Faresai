using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StreetRush.EditorTools
{
    /// <summary>
    /// One-click (or batch-mode) project generation: settings, materials,
    /// character prefab + animator, MainMenu/World scenes, and builds.
    ///   Unity -batchmode -executeMethod StreetRush.EditorTools.StreetRushSetup.SetupAll
    ///   Unity -batchmode -executeMethod StreetRush.EditorTools.StreetRushSetup.BuildAndroid
    ///   Unity -batchmode -executeMethod StreetRush.EditorTools.StreetRushSetup.BuildLinuxTest
    /// </summary>
    public static class StreetRushSetup
    {
        const string Root = "Assets/Game";
        const string MatDir = Root + "/Resources/Materials";
        const string PrefabDir = Root + "/Resources/Prefabs";
        const string SceneDir = Root + "/Scenes";
        const string SettingsDir = Root + "/Settings";
        const string CityFbx = Root + "/Art/City/level2.fbx";
        const string CityMatDir = Root + "/Art/City/Materials";
        const string FaresFbx = Root + "/Art/Characters/fares.fbx";
        const string AnimDir = Root + "/Art/Characters/Animations";

        [MenuItem("Street Rush/Setup Everything")]
        public static void SetupAll()
        {
            ConfigureProject();
            CreateMaterials();
            ExtractAndTuneCityMaterials();
            CreateCharacterPrefab();
            CreateMenuScene();
            CreateWorldScene();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(SceneDir + "/MainMenu.unity", true),
                new EditorBuildSettingsScene(SceneDir + "/World.unity", true),
            };
            AssetDatabase.SaveAssets();
            Debug.Log("SETUP: done");
        }

        // ---------------------------------------------------------------- project

        static void ConfigureProject()
        {
            // Layers
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            layers.GetArrayElementAtIndex(Layers.Vehicle).stringValue = "Vehicle";
            layers.GetArrayElementAtIndex(Layers.Pedestrian).stringValue = "Pedestrian";
            layers.GetArrayElementAtIndex(Layers.Props).stringValue = "Props";
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            Physics.IgnoreLayerCollision(Layers.Vehicle, Layers.Pedestrian, true);

            PlayerSettings.companyName = "Faresai";
            PlayerSettings.productName = "Street Rush";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.faresai.streetrush");
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.Android, ManagedStrippingLevel.Low);
            PlayerSettings.Android.optimizedFramePacing = true;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.runInBackground = true; // desktop test builds keep running without window focus
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

            // Use the "Mobile" quality level (URP Mobile asset) everywhere so tests match phones.
            var qs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var perPlatform = qs.FindProperty("m_PerPlatformDefaultQuality");
            for (int i = 0; i < perPlatform.arraySize; i++)
            {
                var e = perPlatform.GetArrayElementAtIndex(i);
                string key = e.FindPropertyRelative("first").stringValue;
                if (key == "Android" || key == "Standalone") e.FindPropertyRelative("second").intValue = 0;
            }
            qs.ApplyModifiedPropertiesWithoutUndo();

            // URP mobile asset: HDR, MSAA, real shadows.
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            var so = new SerializedObject(urp);
            SetProp(so, "m_SupportsHDR", true);
            SetProp(so, "m_MSAA", 4);
            SetProp(so, "m_RenderScale", 1f);
            SetProp(so, "m_ShadowDistance", 140f);
            SetProp(so, "m_ShadowCascadeCount", 2);
            SetProp(so, "m_MainLightShadowmapResolution", 2048);
            SetProp(so, "m_SoftShadowsSupported", true);
            SetProp(so, "m_AdditionalLightsPerObjectLimit", 4);
            SetProp(so, "m_SupportsCameraDepthTexture", false);
            SetProp(so, "m_SupportsCameraOpaqueTexture", false);
            SetProp(so, "m_ColorGradingMode", 1); // HDR grading
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urp);
        }

        static void SetProp(SerializedObject so, string name, object value)
        {
            var p = so.FindProperty(name);
            if (p == null) { Debug.LogWarning("SETUP: missing property " + name); return; }
            switch (value)
            {
                case bool b: p.boolValue = b; break;
                case int i:
                    if (p.propertyType == SerializedPropertyType.Enum) p.enumValueIndex = i; else p.intValue = i;
                    break;
                case float f: p.floatValue = f; break;
            }
        }

        // ---------------------------------------------------------------- materials

        static Shader Lit => Shader.Find("Universal Render Pipeline/Lit");

        static Material MakeMat(string name, Color color, float metallic, float smoothness, Color? emission = null)
        {
            Directory.CreateDirectory(MatDir);
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Lit);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = Lit;
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else m.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(m);
            return m;
        }

        static void CreateMaterials()
        {
            MakeMat("CarPaint", new Color(0.6f, 0.05f, 0.06f), 0.55f, 0.88f);
            MakeMat("PolicePaint", new Color(0.92f, 0.93f, 0.95f), 0.4f, 0.86f);
            MakeMat("PoliceStripe", new Color(0.03f, 0.03f, 0.04f), 0.4f, 0.8f);
            MakeMat("Glass", new Color(0.03f, 0.045f, 0.06f), 0.6f, 0.96f);
            MakeMat("Trim", new Color(0.04f, 0.04f, 0.045f), 0.1f, 0.45f);
            MakeMat("Tire", new Color(0.025f, 0.025f, 0.025f), 0f, 0.25f);
            MakeMat("Rim", new Color(0.72f, 0.73f, 0.75f), 0.95f, 0.8f);
            MakeMat("HeadLight", new Color(1f, 0.97f, 0.9f), 0f, 0.9f, new Color(2.2f, 2.1f, 1.9f));
            MakeMat("TailLight", new Color(0.6f, 0.02f, 0.02f), 0f, 0.9f, new Color(1.8f, 0.05f, 0.03f));
            MakeMat("SirenRed", new Color(1f, 0.1f, 0.1f), 0f, 0.9f, new Color(8f, 0.4f, 0.3f));
            MakeMat("SirenBlue", new Color(0.1f, 0.3f, 1f), 0f, 0.9f, new Color(0.3f, 1.2f, 8f));
            MakeMat("Skin", new Color(0.5f, 0.36f, 0.22f), 0f, 0.25f);
            MakeMat("Floor", new Color(0.045f, 0.05f, 0.065f), 0.1f, 0.72f);
        }

        static void ExtractAndTuneCityMaterials()
        {
            Directory.CreateDirectory(CityMatDir);
            var embedded = AssetDatabase.LoadAllAssetsAtPath(CityFbx).OfType<Material>().ToList();
            foreach (var m in embedded)
            {
                string path = $"{CityMatDir}/{m.name}.mat";
                if (!File.Exists(path))
                {
                    string err = AssetDatabase.ExtractAsset(m, path);
                    if (!string.IsNullOrEmpty(err)) Debug.LogWarning("SETUP: extract " + m.name + ": " + err);
                }
            }
            AssetDatabase.WriteImportSettingsIfDirty(CityFbx);
            AssetDatabase.ImportAsset(CityFbx, ImportAssetOptions.ForceUpdate);

            var water = Shader.Find("StreetRush/Water");
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { CityMatDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                string n = m.name.ToLowerInvariant();
                if (n.Contains("water") && water != null)
                {
                    var tex = m.GetTexture("_BaseMap");
                    m.shader = water;
                    if (tex) m.SetTexture("_BaseMap", tex);
                    m.SetTextureScale("_BaseMap", new Vector2(8, 8));
                    if (n.Contains("reflection")) { m.SetColor("_ShallowColor", new Color(0.2f, 0.75f, 0.8f)); m.SetColor("_DeepColor", new Color(0.05f, 0.45f, 0.6f)); m.SetFloat("_WaveStrength", 0.12f); }
                }
                else
                {
                    m.shader = Lit;
                    m.SetFloat("_Metallic", 0f);
                    float smooth = 0.18f;
                    if (n.Contains("ground")) smooth = 0.28f;
                    if (n.Contains("trees")) smooth = 0.08f;
                    if (n.Contains("business") || n.Contains("hotel") || n.Contains("villa")) smooth = 0.35f;
                    m.SetFloat("_Smoothness", smooth);
                    m.SetFloat("_EnvironmentReflections", 1f);
                    if (n.Contains("default")) m.SetColor("_BaseColor", new Color(0.78f, 0.78f, 0.76f));
                    if (n.Contains("trees") || n.Contains("fence"))
                    {
                        // Leaves / fences use alpha cut-outs.
                        m.SetFloat("_AlphaClip", 1f);
                        m.SetFloat("_Cutoff", 0.45f);
                        m.EnableKeyword("_ALPHATEST_ON");
                        m.SetOverrideTag("RenderType", "TransparentCutout");
                        m.renderQueue = (int)RenderQueue.AlphaTest;
                        m.SetFloat("_Cull", 0f);
                        m.doubleSidedGI = true;
                    }
                }
                EditorUtility.SetDirty(m);
            }
            AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------- character

        static AnimationClip ClipFrom(string fbx)
        {
            return AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }

        static void CreateCharacterPrefab()
        {
            Directory.CreateDirectory(PrefabDir);
            string ctrlPath = PrefabDir + "/CharacterAnimator.controller";
            AssetDatabase.DeleteAsset(ctrlPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            var sm = ctrl.layers[0].stateMachine;
            foreach (var (state, file) in new[] { ("Idle", "Idle"), ("Walk", "Walking"), ("Run", "Running"), ("Jump", "jump") })
            {
                var clip = ClipFrom($"{AnimDir}/{file}.fbx");
                var s = sm.AddState(state);
                s.motion = clip;
                s.writeDefaultValues = true;
                if (clip == null) Debug.LogWarning("SETUP: missing clip " + file);
                if (state == "Idle") sm.defaultState = s;
            }

            // Ready Player Me avatar: its textures are embedded in the FBX -> extract & remap them.
            string texDir = Root + "/Art/Characters/Textures";
            if (!Directory.Exists(texDir) || Directory.GetFiles(texDir, "*.png").Length + Directory.GetFiles(texDir, "*.jpg").Length == 0)
            {
                Directory.CreateDirectory(texDir);
                var imp = (ModelImporter)AssetImporter.GetAtPath(FaresFbx);
                bool ok = imp.ExtractTextures(texDir);
                Debug.Log("SETUP: extract fares textures " + ok);
                AssetDatabase.Refresh();
                AssetDatabase.ImportAsset(FaresFbx, ImportAssetOptions.ForceUpdate);
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FaresFbx);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var anim = inst.GetComponent<Animator>() ?? inst.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            var avatar = AssetDatabase.LoadAllAssetsAtPath(FaresFbx).OfType<Avatar>().FirstOrDefault();
            if (avatar != null) anim.avatar = avatar;
            foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                smr.shadowCastingMode = ShadowCastingMode.On;
                foreach (var m in smr.sharedMaterials)
                    if (m != null) Debug.Log($"SETUP: fares material {m.name} shader={m.shader.name} tex={(m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") ? m.GetTexture("_BaseMap").name : "NONE")}");
            }
            PrefabUtility.SaveAsPrefabAsset(inst, PrefabDir + "/Fares.prefab");
            Object.DestroyImmediate(inst);
        }

        // ---------------------------------------------------------------- post / sky

        static VolumeProfile CreatePostProfile(string name, bool world)
        {
            Directory.CreateDirectory(SettingsDir);
            string path = $"{SettingsDir}/{name}.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(world ? 1.05f : 0.9f);
            bloom.intensity.Override(world ? 0.55f : 0.9f);
            bloom.scatter.Override(0.72f);
            bloom.highQualityFiltering.Override(false);

            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(world ? 0.18f : 0.2f);
            color.contrast.Override(14f);
            color.saturation.Override(world ? 14f : 8f);

            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(world ? 9f : -6f);
            wb.tint.Override(world ? 3f : 0f);

            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.92f, 0.98f, 1.1f, 0f));      // cool shadows
            smh.highlights.Override(new Vector4(1.08f, 1.0f, 0.9f, 0f));    // warm highlights

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.24f);
            vignette.smoothness.Override(0.45f);

            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        static Material CreateSkybox()
        {
            Directory.CreateDirectory(SettingsDir);
            string path = SettingsDir + "/MiamiSky.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Skybox/Procedural")); AssetDatabase.CreateAsset(m, path); }
            m.SetFloat("_SunDisk", 2f);
            m.SetFloat("_SunSize", 0.035f);
            m.SetFloat("_SunSizeConvergence", 6f);
            m.SetFloat("_AtmosphereThickness", 0.85f);
            m.SetColor("_SkyTint", new Color(0.42f, 0.55f, 0.75f));
            m.SetColor("_GroundColor", new Color(0.36f, 0.42f, 0.48f));
            m.SetFloat("_Exposure", 1.25f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void AddVolume(VolumeProfile profile)
        {
            var go = new GameObject("PostProcessing");
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.sharedProfile = profile;
        }

        static void NoBakedLighting()
        {
            var ls = new LightingSettings { bakedGI = false, realtimeGI = false, autoGenerate = false };
            Lightmapping.lightingSettings = ls;
        }

        // ---------------------------------------------------------------- scenes

        static void CreateMenuScene()
        {
            Directory.CreateDirectory(SceneDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            NoBakedLighting();

            var camGo = new GameObject("MenuCamera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.035f, 0.04f, 0.07f);
            cam.fieldOfView = 40f;
            camGo.transform.position = new Vector3(-1.2f, 1.45f, -8.4f);
            camGo.transform.LookAt(new Vector3(1.1f, 0.75f, 0f));
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camGo.AddComponent<AudioListener>();

            var key = new GameObject("KeyLight").AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.92f, 0.82f);
            key.intensity = 1.6f;
            key.shadows = LightShadows.Soft;
            key.transform.rotation = Quaternion.Euler(42, -35, 0);

            var rim = new GameObject("RimLight").AddComponent<Light>();
            rim.type = LightType.Spot;
            rim.color = new Color(0.35f, 0.55f, 1f);
            rim.intensity = 14f;
            rim.range = 18f;
            rim.spotAngle = 70f;
            rim.transform.position = new Vector3(5.5f, 4.5f, 6f);
            rim.transform.LookAt(new Vector3(2.2f, 0.5f, 0));

            var accent = new GameObject("AccentLight").AddComponent<Light>();
            accent.type = LightType.Point;
            accent.color = new Color(1f, 0.7f, 0.3f);
            accent.intensity = 8f;
            accent.range = 9f;
            accent.transform.position = new Vector3(-1.5f, 2.2f, -2.5f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(20, 1, 20);
            floor.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Floor.mat");

            var probe = new GameObject("Reflections");
            probe.AddComponent<ReflectionProbe>();
            probe.AddComponent<SkyReflection>().includeScene = true;
            probe.transform.position = new Vector3(2.2f, 1f, 0);

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.22f, 0.25f, 0.34f);
            RenderSettings.ambientEquatorColor = new Color(0.12f, 0.12f, 0.15f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.05f, 0.06f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = cam.backgroundColor;
            RenderSettings.fogStartDistance = 9f;
            RenderSettings.fogEndDistance = 26f;

            AddVolume(CreatePostProfile("MenuPost", false));
            new GameObject("MainMenu").AddComponent<MainMenu>();
            EditorSceneManager.SaveScene(scene, SceneDir + "/MainMenu.unity");
        }

        static void CreateWorldScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            NoBakedLighting();

            var sky = CreateSkybox();
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.9f, 0.78f);
            sun.intensity = 2.1f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.shadowBias = 0.4f;
            sun.shadowNormalBias = 0.6f;
            sun.transform.rotation = Quaternion.Euler(34f, -128f, 0f);

            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.52f, 0.63f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.6f, 0.56f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.27f, 0.23f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.68f, 0.76f, 0.86f);
            RenderSettings.fogStartDistance = 160f;
            RenderSettings.fogEndDistance = 1250f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 1f;

            var probe = new GameObject("SkyReflection");
            probe.AddComponent<ReflectionProbe>();
            probe.AddComponent<SkyReflection>();

            AddVolume(CreatePostProfile("WorldPost", true));

            // City model with baked colliders.
            var cityRoot = new GameObject("City");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CityFbx);
            var city = (GameObject)PrefabUtility.InstantiatePrefab(model, cityRoot.transform);
            PrefabUtility.UnpackPrefabInstance(city, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            city.name = "MiamiCity";
            int colliders = 0, props = 0;
            foreach (var mf in city.GetComponentsInChildren<MeshFilter>())
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mf.sharedMesh == null || mr == null) continue;
                var size = mr.bounds.size;
                float footprint = Mathf.Max(size.x, size.z);
                GameObjectUtility.SetStaticEditorFlags(mf.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
                if (footprint >= 2.2f)
                {
                    var mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation | MeshColliderCookingOptions.EnableMeshCleaning | MeshColliderCookingOptions.WeldColocatedVertices | MeshColliderCookingOptions.UseFastMidphase;
                    colliders++;
                }
                if (footprint < 10f && size.y < 14f)
                {
                    mf.gameObject.layer = Layers.Props;
                    GameObjectUtility.SetStaticEditorFlags(mf.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                    props++;
                }
                string n = mf.name.ToLowerInvariant();
                if (n.Contains("sea") || n.Contains("water") || n.Contains("ground"))
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                if (n.Contains("palm") || n.Contains("tree"))
                    mr.shadowCastingMode = ShadowCastingMode.On;
            }
            Debug.Log($"SETUP: city colliders={colliders} props={props}");
            cityRoot.AddComponent<CityBuilder>();

            new GameObject("WorldBootstrap").AddComponent<WorldBootstrap>();
            EditorSceneManager.SaveScene(scene, SceneDir + "/World.unity");

            // Occlusion culling for the dense city (CPU-side Umbra bake).
            try
            {
                StaticOcclusionCulling.smallestOccluder = 8f;
                StaticOcclusionCulling.smallestHole = 0.5f;
                StaticOcclusionCulling.backfaceThreshold = 100f;
                bool ok = StaticOcclusionCulling.Compute();
                Debug.Log("SETUP: occlusion bake " + ok);
                EditorSceneManager.SaveScene(scene);
            }
            catch (System.Exception e) { Debug.LogWarning("SETUP: occlusion bake failed " + e.Message); }
        }

        // ---------------------------------------------------------------- builds

        static string[] Scenes => new[] { SceneDir + "/MainMenu.unity", SceneDir + "/World.unity" };

        public static void BuildAndroid()
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            EditorUserBuildSettings.buildAppBundle = false;
            Directory.CreateDirectory("Builds");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = "Builds/StreetRush.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });
            Summarise(report);
        }

        public static void BuildLinuxTest()
        {
            Directory.CreateDirectory("Builds/Linux");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = "Builds/Linux/StreetRush.x86_64",
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.None,
            });
            Summarise(report);
        }

        static void Summarise(BuildReport report)
        {
            var s = report.summary;
            Debug.Log($"BUILD: result={s.result} size={s.totalSize / (1024f * 1024f):F1}MB time={s.totalTime} errors={s.totalErrors} path={s.outputPath}");
            if (s.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
