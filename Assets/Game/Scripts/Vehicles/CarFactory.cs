using System.Collections.Generic;
using UnityEngine;

namespace StreetRush
{
    /// <summary>
    /// Builds good-looking cars procedurally (smooth lofted body + glass
    /// cabin, detailed wheels, emissive head/tail lights, optional police
    /// light bar). No external car model is needed, and every car shares
    /// cached meshes so traffic is cheap to spawn.
    /// </summary>
    public static class CarFactory
    {
        public const float Length = 4.5f;
        public const float Width = 1.88f;
        public const float WheelRadius = 0.36f;
        public const float WheelBaseFront = 1.42f;
        public const float WheelBaseRear = -1.38f;
        public const float TrackHalf = 0.84f;

        static Mesh _body, _cabin, _roof, _tire, _rim, _lightBar;
        static readonly Dictionary<string, Material> MatCache = new Dictionary<string, Material>();

        public static readonly Color[] CivilianColors =
        {
            new Color(0.62f, 0.05f, 0.06f), new Color(0.05f, 0.16f, 0.42f), new Color(0.85f, 0.72f, 0.12f),
            new Color(0.06f, 0.06f, 0.07f), new Color(0.86f, 0.87f, 0.88f), new Color(0.07f, 0.32f, 0.18f),
            new Color(0.45f, 0.46f, 0.48f), new Color(0.95f, 0.42f, 0.05f), new Color(0.32f, 0.08f, 0.38f),
        };

        public static Material Mat(string name)
        {
            if (MatCache.TryGetValue(name, out var m) && m) return m;
            m = Resources.Load<Material>("Materials/" + name);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.name = name;
            }
            MatCache[name] = m;
            return m;
        }

        static Material Paint(Color c)
        {
            string key = "paint_" + ColorUtility.ToHtmlStringRGB(c);
            if (MatCache.TryGetValue(key, out var m) && m) return m;
            m = new Material(Mat("CarPaint")) { name = key };
            m.SetColor("_BaseColor", c);
            MatCache[key] = m;
            return m;
        }

        /// <summary>Creates the visual hierarchy of a car (no physics). Front of the car is +Z.</summary>
        public static GameObject BuildVisual(Color paint, bool police, out Transform[] wheelVisuals, out GameObject[] sirens)
        {
            EnsureMeshes();
            var root = new GameObject(police ? "PoliceCar" : "Car");
            var bodyMat = police ? Mat("PolicePaint") : Paint(paint);

            AddPart(root.transform, "Body", _body, bodyMat, Vector3.zero);
            AddPart(root.transform, "Cabin", _cabin, Mat("Glass"), Vector3.zero);
            AddPart(root.transform, "Roof", _roof, bodyMat, Vector3.zero);

            // Bumpers / trim
            AddCube(root.transform, "BumperF", new Vector3(0, 0.42f, Length * 0.5f - 0.02f), new Vector3(Width * 0.9f, 0.16f, 0.12f), Mat("Trim"));
            AddCube(root.transform, "BumperR", new Vector3(0, 0.42f, -Length * 0.5f + 0.02f), new Vector3(Width * 0.9f, 0.16f, 0.12f), Mat("Trim"));
            AddCube(root.transform, "Grille", new Vector3(0, 0.62f, Length * 0.5f - 0.03f), new Vector3(Width * 0.45f, 0.14f, 0.06f), Mat("Trim"));

            // Lights
            foreach (float sx in new[] { -1f, 1f })
            {
                AddCube(root.transform, "HeadLight", new Vector3(sx * 0.62f, 0.72f, Length * 0.5f - 0.06f), new Vector3(0.36f, 0.12f, 0.08f), Mat("HeadLight"));
                AddCube(root.transform, "TailLight", new Vector3(sx * 0.66f, 0.80f, -Length * 0.5f + 0.06f), new Vector3(0.34f, 0.11f, 0.08f), Mat("TailLight"));
                AddCube(root.transform, "Mirror", new Vector3(sx * (Width * 0.5f + 0.05f), 1.02f, 0.72f), new Vector3(0.12f, 0.09f, 0.16f), bodyMat);
            }

            // Wheels
            wheelVisuals = new Transform[4];
            var positions = WheelPositions();
            for (int i = 0; i < 4; i++)
            {
                var pivot = new GameObject("Wheel" + i).transform;
                pivot.SetParent(root.transform, false);
                pivot.localPosition = positions[i];
                bool left = positions[i].x < 0;
                var tire = AddPart(pivot, "Tire", _tire, Mat("Tire"), Vector3.zero);
                var rim = AddPart(pivot, "Rim", _rim, Mat("Rim"), new Vector3(left ? -0.005f : 0.005f, 0, 0));
                if (left) { tire.transform.localScale = new Vector3(-1, 1, 1); rim.transform.localScale = new Vector3(-1, 1, 1); }
                wheelVisuals[i] = pivot;
            }

            sirens = null;
            if (police)
            {
                AddPart(root.transform, "LightBar", _lightBar, Mat("Trim"), new Vector3(0, 1.47f, -0.35f));
                var red = AddCube(root.transform, "SirenRed", new Vector3(-0.32f, 1.53f, -0.35f), new Vector3(0.46f, 0.1f, 0.2f), Mat("SirenRed"));
                var blue = AddCube(root.transform, "SirenBlue", new Vector3(0.32f, 1.53f, -0.35f), new Vector3(0.46f, 0.1f, 0.2f), Mat("SirenBlue"));
                AddSirenLight(red.transform, new Color(1f, 0.1f, 0.08f));
                AddSirenLight(blue.transform, new Color(0.1f, 0.3f, 1f));
                // Black lower doors stripe.
                AddCube(root.transform, "Stripe", new Vector3(0, 0.62f, -0.1f), new Vector3(Width + 0.012f, 0.22f, 2.4f), Mat("PoliceStripe"));
                sirens = new[] { red, blue };
            }
            return root;
        }

        static void AddSirenLight(Transform parent, Color c)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, 0.6f, 0);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.range = 9f;
            l.intensity = 6f;
            l.shadows = LightShadows.None;
        }

        public static Vector3[] WheelPositions() => new[]
        {
            new Vector3(-TrackHalf, WheelRadius, WheelBaseFront),
            new Vector3(TrackHalf, WheelRadius, WheelBaseFront),
            new Vector3(-TrackHalf, WheelRadius, WheelBaseRear),
            new Vector3(TrackHalf, WheelRadius, WheelBaseRear),
        };

        static GameObject AddPart(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            return go;
        }

        static GameObject AddCube(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        // ---------------- Procedural meshes ----------------

        static void EnsureMeshes()
        {
            if (_body != null) return;
            _body = Loft("CarBody", 40, 28, -Length * 0.5f, Length * 0.5f, BodySection, true);
            _cabin = Loft("CarCabin", 24, 24, -1.42f, 0.86f, CabinSection, true);
            _roof = Loft("CarRoof", 12, 20, -0.95f, -0.02f, RoofSection, true);
            _tire = Wheel("Tire", WheelRadius, 0.26f, 0.22f, 28);
            _rim = Wheel("Rim", WheelRadius * 0.62f, 0.27f, 0.0f, 10);
            _lightBar = Box("LightBar", new Vector3(1.2f, 0.06f, 0.26f));
        }

        // Section = (centerY, halfHeight, bottomHalfWidth, topHalfWidth, exponent)
        struct Section { public float cy, hh, wb, wt, n; }

        static Section BodySection(float z)
        {
            float t = Mathf.InverseLerp(-Length * 0.5f, Length * 0.5f, z); // 0 rear .. 1 front
            float end = Mathf.Abs(t - 0.5f) * 2f;                             // 0 middle .. 1 ends
            float bottom = 0.40f + Mathf.Pow(end, 4) * 0.06f;
            float top;
            if (t > 0.70f) top = Mathf.Lerp(1.0f, 0.80f, Mathf.SmoothStep(0, 1, (t - 0.70f) / 0.30f));   // hood slopes to nose
            else if (t < 0.18f) top = Mathf.Lerp(0.88f, 1.03f, Mathf.SmoothStep(0, 1, t / 0.18f));       // trunk lid
            else top = 1.03f;
            float nose = Mathf.Pow(end, 8);
            top = Mathf.Lerp(top, (top + bottom) * 0.5f + 0.12f, nose * 0.5f);
            bottom = Mathf.Lerp(bottom, (top + bottom) * 0.5f - 0.1f, nose * 0.4f);
            float hw = Width * 0.5f * (1f - Mathf.Pow(end, 5) * 0.22f);
            return new Section { cy = (top + bottom) * 0.5f, hh = (top - bottom) * 0.5f, wb = hw, wt = hw * 0.93f, n = 5.5f };
        }

        static Section CabinSection(float z)
        {
            // windshield from z=0.86 (y 0.98) up to z=0.05 (y 1.46); roof to -0.85; rear glass down to -1.42
            float top;
            if (z > 0.05f) top = Mathf.Lerp(1.46f, 0.98f, Mathf.SmoothStep(0, 1, (z - 0.05f) / 0.81f));
            else if (z < -0.85f) top = Mathf.Lerp(1.46f, 1.0f, Mathf.SmoothStep(0, 1, (-0.85f - z) / 0.57f));
            else top = 1.46f;
            float bottom = 0.98f;
            float hh = Mathf.Max(0.01f, (top - bottom) * 0.5f);
            return new Section { cy = bottom + hh, hh = hh, wb = Width * 0.5f - 0.05f, wt = Width * 0.5f - 0.24f, n = 4.5f };
        }

        static Section RoofSection(float z)
        {
            return new Section { cy = 1.465f, hh = 0.022f, wb = Width * 0.5f - 0.23f, wt = Width * 0.5f - 0.26f, n = 6f };
        }

        delegate Section SectionFn(float z);

        static Mesh Loft(string name, int stations, int ring, float z0, float z1, SectionFn fn, bool caps)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int s = 0; s <= stations; s++)
            {
                float z = Mathf.Lerp(z0, z1, s / (float)stations);
                var sec = fn(z);
                for (int r = 0; r < ring; r++)
                {
                    float a = r / (float)ring * Mathf.PI * 2f;
                    float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                    float py = Mathf.Sign(sn) * Mathf.Pow(Mathf.Abs(sn), 2f / sec.n);
                    float hw = Mathf.Lerp(sec.wb, sec.wt, (py + 1f) * 0.5f);
                    float px = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 2f / sec.n) * hw;
                    verts.Add(new Vector3(px, sec.cy + py * sec.hh, z));
                }
            }
            for (int s = 0; s < stations; s++)
            {
                for (int r = 0; r < ring; r++)
                {
                    int a = s * ring + r, b = s * ring + (r + 1) % ring;
                    int c = (s + 1) * ring + r, d = (s + 1) * ring + (r + 1) % ring;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }
            }
            if (caps)
            {
                foreach (int s in new[] { 0, stations })
                {
                    var sec = fn(Mathf.Lerp(z0, z1, s / (float)stations));
                    int center = verts.Count;
                    verts.Add(new Vector3(0, sec.cy, Mathf.Lerp(z0, z1, s / (float)stations)));
                    for (int r = 0; r < ring; r++)
                    {
                        int a = s * ring + r, b = s * ring + (r + 1) % ring;
                        if (s == 0) { tris.Add(center); tris.Add(b); tris.Add(a); }
                        else { tris.Add(center); tris.Add(a); tris.Add(b); }
                    }
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetTriangles(FixWinding(verts, tris), 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Ensures every triangle faces away from the mesh's centre line (robust against ring direction).</summary>
        static List<int> FixWinding(List<Vector3> v, List<int> t)
        {
            for (int i = 0; i < t.Count; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                Vector3 centroid = (a + b + c) / 3f;
                Vector3 axisPoint = new Vector3(0, centroid.y, centroid.z);
                Vector3 outward = centroid - axisPoint;
                if (outward.sqrMagnitude < 1e-6f) outward = new Vector3(0, 0, Mathf.Sign(centroid.z));
                // caps: outward along z
                if (Mathf.Abs(n.normalized.z) > 0.95f) outward = new Vector3(0, 0, Mathf.Sign(centroid.z));
                if (Vector3.Dot(n, outward) < 0) { int tmp = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = tmp; }
            }
            return t;
        }

        static Mesh Wheel(string name, float radius, float width, float bevel, int segs)
        {
            // Cylinder along X, outer face at +X. Profile rings: inner face, tread, outer face.
            var verts = new List<Vector3>();
            var tris = new List<int>();
            float hw = width * 0.5f;
            float[] xs = { -hw, -hw + bevel * 0.3f, hw - bevel * 0.3f, hw };
            float[] rs = { radius - bevel * 0.35f, radius, radius, radius - bevel * 0.35f };
            int rings = xs.Length;
            for (int k = 0; k < rings; k++)
                for (int s = 0; s < segs; s++)
                {
                    float a = s / (float)segs * Mathf.PI * 2f;
                    verts.Add(new Vector3(xs[k], Mathf.Cos(a) * rs[k], Mathf.Sin(a) * rs[k]));
                }
            for (int k = 0; k < rings - 1; k++)
                for (int s = 0; s < segs; s++)
                {
                    int a = k * segs + s, b = k * segs + (s + 1) % segs;
                    int c = (k + 1) * segs + s, d = (k + 1) * segs + (s + 1) % segs;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            foreach (int k in new[] { 0, rings - 1 })
            {
                int center = verts.Count;
                verts.Add(new Vector3(xs[k], 0, 0));
                for (int s = 0; s < segs; s++)
                {
                    int a = k * segs + s, b = k * segs + (s + 1) % segs;
                    if (k == 0) { tris.Add(center); tris.Add(a); tris.Add(b); }
                    else { tris.Add(center); tris.Add(b); tris.Add(a); }
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            // make sure faces point outward from the wheel axis / caps outward on X
            var t = tris;
            for (int i = 0; i < t.Count; i += 3)
            {
                Vector3 a = verts[t[i]], b = verts[t[i + 1]], c = verts[t[i + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                Vector3 centroid = (a + b + c) / 3f;
                Vector3 outward = Mathf.Abs(n.normalized.x) > 0.9f ? new Vector3(Mathf.Sign(centroid.x), 0, 0) : new Vector3(0, centroid.y, centroid.z);
                if (Vector3.Dot(n, outward) < 0) { int tmp = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = tmp; }
            }
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh Box(string name, Vector3 size)
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mesh = Object.Instantiate(tmp.GetComponent<MeshFilter>().sharedMesh);
            Object.Destroy(tmp);
            var v = mesh.vertices;
            for (int i = 0; i < v.Length; i++) v[i] = Vector3.Scale(v[i], size);
            mesh.vertices = v;
            mesh.RecalculateBounds();
            mesh.name = name;
            return mesh;
        }
    }
}
