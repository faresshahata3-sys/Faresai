using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace StreetRush.EditorTools
{
    public static class Probe
    {
        // unity -batchmode -executeMethod StreetRush.EditorTools.Probe.City
        public static void City()
        {
            var sb = new StringBuilder();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Art/City/level2.fbx");
            var go = (GameObject)Object.Instantiate(prefab);
            var renderers = go.GetComponentsInChildren<MeshRenderer>();
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            sb.AppendLine($"PROBE renderers={renderers.Length} bounds center={b.center} size={b.size}");
            sb.AppendLine($"PROBE root scale={go.transform.localScale} rot={go.transform.rotation.eulerAngles}");

            var mats = renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToList();
            foreach (var m in mats)
            {
                var tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.mainTexture;
                sb.AppendLine($"PROBE mat '{m.name}' shader={m.shader.name} tex={(tex ? tex.name : "NONE")} color={(m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString() : "-")}");
            }

            // Biggest pieces, for orientation.
            foreach (var r in renderers.OrderByDescending(r => r.bounds.size.x * r.bounds.size.z).Take(25))
                sb.AppendLine($"PROBE big '{r.name}' center={r.bounds.center} size={r.bounds.size}");

            // Add colliders and test the Godot spawn point in both handedness conventions.
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            Physics.SyncTransforms();
            foreach (var p in new[] { new Vector3(125, 250, 140), new Vector3(-125, 250, 140), new Vector3(125, 250, -140), new Vector3(-125, 250, -140) })
            {
                if (Physics.Raycast(p, Vector3.down, out var hit, 400f))
                    sb.AppendLine($"PROBE ray {p.x},{p.z} -> y={hit.point.y:F2} obj={hit.collider.name} normalY={hit.normal.y:F2}");
                else
                    sb.AppendLine($"PROBE ray {p.x},{p.z} -> miss");
            }

            // Ground-level grid occupancy summary of the Godot "active area" in both conventions.
            foreach (var sx in new[] { 1, -1 })
            {
                int hits = 0, ground = 0;
                for (float x = 10; x <= 240; x += 14)
                for (float z = -70; z <= 350; z += 14)
                {
                    if (Physics.Raycast(new Vector3(sx * x, 250, z), Vector3.down, out var h, 400f))
                    {
                        hits++;
                        if (h.point.y > -2 && h.point.y < 3 && h.normal.y > 0.7f) ground++;
                    }
                }
                sb.AppendLine($"PROBE area signX={sx} hits={hits} streetLevel={ground}");
            }

            Debug.Log(sb.ToString());
            System.IO.File.WriteAllText("Logs/probe.txt", sb.ToString());
        }
    }
}
