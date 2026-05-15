using UnityEngine;

namespace DungeonExporer.Gameplay
{
    /// <summary>
    /// Instantiates a Meshy (or other) character mesh under a gameplay root and fits it to a target height on the floor.
    /// </summary>
    public static class CharacterVisualUtility
    {
        public static GameObject CreateRoot(string name, Vector3 feetWorldPosition, float facingYawDegrees)
        {
            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(
                feetWorldPosition,
                Quaternion.Euler(0f, facingYawDegrees, 0f));
            return root;
        }

        public static bool TryAttachModel(
            GameObject root,
            GameObject modelAsset,
            float targetHeight,
            float visualYawOffset,
            bool combatCapsule,
            Texture2D albedoOverride = null)
        {
            if (root == null || modelAsset == null || targetHeight <= 0.01f)
                return false;

            GameObject visual = Object.Instantiate(modelAsset, root.transform);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(0f, visualYawOffset, 0f);
            visual.transform.localScale = Vector3.one;

            StripColliders(visual);
            FitVisualToHeight(visual, targetHeight);

            if (albedoOverride != null)
                MeshyMaterialUtility.ApplyUrpLitAlbedo(visual, albedoOverride);

            if (combatCapsule)
                AddCombatCapsule(root, targetHeight);

            return true;
        }

        /// <summary>
        /// Picks 0° or 180° local Y on the mesh so its forward axis faces <paramref name="worldTarget"/> (Meshy exports vary).
        /// </summary>
        public static void AlignVisualToward(GameObject visual, Vector3 worldTarget, float extraYawDegrees = 0f)
        {
            if (visual == null)
                return;

            Vector3 flat = worldTarget - visual.transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f)
                return;

            flat.Normalize();
            float dotForward = Vector3.Dot(visual.transform.forward, flat);
            float dotBack = Vector3.Dot(-visual.transform.forward, flat);
            float flip = dotBack > dotForward ? 180f : 0f;
            visual.transform.localRotation = Quaternion.Euler(0f, flip + extraYawDegrees, 0f);
        }

        /// <summary>Y rotation (degrees) so root +Z points at <paramref name="worldTarget"/> on the XZ plane.</summary>
        public static float YawToward(Vector3 fromWorld, Vector3 worldTarget)
        {
            Vector3 dir = worldTarget - fromWorld;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
                return 0f;
            return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }

        public static void AddFallbackCapsule(GameObject root, float height, Color color, bool combatCapsule)
        {
            if (root == null)
                return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "FallbackVisual";
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            go.transform.localScale = new Vector3(height * 0.45f, height * 0.5f, height * 0.45f);

            Object.Destroy(go.GetComponent<Collider>());

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
                rend.material.color = color;

            if (combatCapsule)
                AddCombatCapsule(root, height);
        }

        private static void StripColliders(GameObject visual)
        {
            Collider[] colliders = visual.GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
                Object.Destroy(colliders[i]);
        }

        private static void FitVisualToHeight(GameObject visual, float targetHeight)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            float height = bounds.size.y;
            if (height < 0.001f)
                return;

            float scale = targetHeight / height;
            visual.transform.localScale *= scale;

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            Transform parent = visual.transform.parent;
            float lift = parent != null ? parent.position.y - bounds.min.y : -bounds.min.y;
            visual.transform.position += new Vector3(0f, lift, 0f);
        }

        private static void AddCombatCapsule(GameObject root, float height)
        {
            if (root.GetComponent<CapsuleCollider>() != null)
                return;

            var cap = root.AddComponent<CapsuleCollider>();
            cap.height = Mathf.Max(0.5f, height);
            cap.radius = height * 0.28f;
            cap.center = new Vector3(0f, height * 0.5f, 0f);
        }
    }
}
