using UnityEditor;
using UnityEngine;

namespace StreetRush.EditorTools
{
    /// <summary>
    /// Import rules for the game's art so a fresh checkout imports correctly
    /// without anyone touching the Inspector.
    /// </summary>
    public class StreetRushImport : AssetPostprocessor
    {
        const string CharacterDir = "Assets/Game/Art/Characters";
        const string CityDir = "Assets/Game/Art/City";

        void OnPreprocessModel()
        {
            var importer = (ModelImporter)assetImporter;
            string path = assetPath.Replace('\\', '/');

            if (path.StartsWith(CharacterDir))
            {
                // Mixamo rigs: Humanoid so every clip retargets onto fares.fbx.
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.importCameras = false;
                importer.importLights = false;
                bool isClipFile = path.Contains("/Animations/");
                importer.importAnimation = isClipFile;
                if (isClipFile)
                {
                    // Clip-only files don't need their own mesh materials.
                    importer.materialImportMode = ModelImporterMaterialImportMode.None;
                }
            }
            else if (path.StartsWith(CityDir))
            {
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.isReadable = false;          // colliders are baked into the scene at build time
                importer.meshCompression = ModelImporterMeshCompression.Off;
                importer.optimizeMeshPolygons = true;
                importer.optimizeMeshVertices = true;
                importer.importNormals = ModelImporterNormals.Import;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                importer.materialSearch = ModelImporterMaterialSearch.Local;
            }
        }

        void OnPreprocessAnimation()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(CharacterDir + "/Animations/")) return;

            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;

            string lower = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            bool loop = !lower.Contains("jump");
            foreach (var clip in clips)
            {
                clip.loopTime = loop;
                clip.loopPose = loop;
                // In-place locomotion: the CharacterController moves the body.
                clip.lockRootRotation = true;
                clip.keepOriginalOrientation = true;
                clip.lockRootHeightY = true;
                clip.keepOriginalPositionY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            var importer = (TextureImporter)assetImporter;
            if (path.StartsWith("Assets/Game/Resources/UI"))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
            }
            else if (path.StartsWith(CityDir) || path.StartsWith(CharacterDir))
            {
                importer.mipmapEnabled = true;
                importer.anisoLevel = 4;
                importer.filterMode = FilterMode.Trilinear;
                importer.maxTextureSize = 2048;
            }
        }
    }
}
