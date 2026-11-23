using UnityEngine;

namespace StreetRush
{
    /// <summary>
    /// Spawns the animated "fares" character model (Resources/Prefabs/Fares),
    /// scaled to a target height, inside a "Visual" container that can be
    /// rotated independently of the physics body.
    /// </summary>
    public static class CharacterModel
    {
        static GameObject _prefab;

        public static Transform Create(Transform parent, float height, out Animator animator, Color? tint = null)
        {
            var visual = new GameObject("Visual").transform;
            visual.SetParent(parent, false);
            animator = null;

            if (_prefab == null) _prefab = Resources.Load<GameObject>("Prefabs/Fares");
            if (_prefab == null)
            {
                // Fallback capsule so the game still works without the model.
                var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Object.Destroy(cap.GetComponent<Collider>());
                cap.transform.SetParent(visual, false);
                cap.transform.localPosition = Vector3.up * height * 0.5f;
                cap.transform.localScale = new Vector3(0.6f, height * 0.5f, 0.6f);
                return visual;
            }

            var model = Object.Instantiate(_prefab, visual, false);
            model.name = "Model";
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = parent.gameObject.layer;

            // Normalise height (Mixamo exports vary between cm and m).
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                if (b.size.y > 0.01f)
                {
                    float s = height / b.size.y;
                    // Scale about the model origin, then drop the feet onto the parent's origin.
                    Vector3 origin = model.transform.position;
                    float minY = origin.y + (b.min.y - origin.y) * s;
                    model.transform.localScale *= s;
                    model.transform.position += Vector3.up * (parent.position.y - minY);
                }
            }

            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                smr.updateWhenOffscreen = false;
                smr.quality = SkinQuality.Bone2;
                if (tint.HasValue)
                {
                    var block = new MaterialPropertyBlock();
                    smr.GetPropertyBlock(block);
                    block.SetColor("_BaseColor", tint.Value);
                    smr.SetPropertyBlock(block);
                }
            }

            animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            }
            return visual;
        }
    }
}
