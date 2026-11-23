using UnityEngine;

namespace StreetRush
{
    /// <summary>Physics layers (names are registered in TagManager by the editor setup).</summary>
    public static class Layers
    {
        public const int Player = 2;      // built-in "Ignore Raycast"
        public const int Vehicle = 8;
        public const int Pedestrian = 9;
        public const int Props = 10;      // small city details, culled earlier

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            Physics.IgnoreLayerCollision(Vehicle, Pedestrian, true);
        }

        public static void SetRecursively(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }
    }
}
