using UnityEngine;
using UnityEngine.Rendering;

namespace DungeonExporer.Gameplay
{
    /// <summary>
    /// Builds a translucent bubble shell and a small 3D centre icon for world pickups.
    /// </summary>
    public static class PickupBubbleVisual
    {
        private static Material _bubbleMaterialTemplate;

        public static void Apply(GameObject root, string itemId, float healAmount, Color accentTint)
        {
            if (root == null)
                return;

            EnsureSharedAssets();

            bool isHeal = healAmount > 0f || itemId == "trail_ration";
            Color bubbleTint = accentTint;
            bubbleTint.a = 0.28f;
            Color iconColor = isHeal
                ? new Color(0.28f, 0.88f, 0.38f, 1f)
                : new Color(0.72f, 0.58f, 0.4f, 1f);

            MeshRenderer shell = root.GetComponent<MeshRenderer>();
            if (shell != null)
            {
                Material bubble = new Material(_bubbleMaterialTemplate);
                bubble.SetColor("_BaseColor", bubbleTint);
                shell.sharedMaterial = bubble;
                shell.shadowCastingMode = ShadowCastingMode.Off;
            }

            Transform existing = root.transform.Find("PickupIcon");
            if (existing != null)
                Object.Destroy(existing.gameObject);

            var iconRoot = new GameObject("PickupIcon");
            iconRoot.transform.SetParent(root.transform, false);
            iconRoot.transform.localPosition = Vector3.zero;
            iconRoot.transform.localRotation = Quaternion.identity;
            iconRoot.transform.localScale = Vector3.one;

            if (isHeal)
                BuildCrossIcon(iconRoot.transform, iconColor);
            else
                BuildPebbleIcon(iconRoot.transform, iconColor);

            iconRoot.AddComponent<PickupIconBillboard>();
        }

        private static void BuildCrossIcon(Transform parent, Color color)
        {
            CreateIconBar(parent, new Vector3(0f, 0f, 0f), new Vector3(0.14f, 0.44f, 0.14f), color);
            CreateIconBar(parent, new Vector3(0f, 0f, 0f), new Vector3(0.44f, 0.14f, 0.14f), color);
        }

        private static void BuildPebbleIcon(Transform parent, Color color)
        {
            var pebble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pebble.name = "Pebble";
            pebble.transform.SetParent(parent, false);
            pebble.transform.localPosition = Vector3.zero;
            pebble.transform.localScale = Vector3.one * 0.38f;
            Object.Destroy(pebble.GetComponent<Collider>());
            ApplyOpaqueColor(pebble.GetComponent<Renderer>(), color);
        }

        private static void CreateIconBar(Transform parent, Vector3 localPos, Vector3 localScale, Color color)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = localPos;
            bar.transform.localScale = localScale;
            Object.Destroy(bar.GetComponent<Collider>());
            ApplyOpaqueColor(bar.GetComponent<Renderer>(), color);
        }

        private static void ApplyOpaqueColor(Renderer renderer, Color color)
        {
            if (renderer == null)
                return;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            var mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.45f);
            mat.SetFloat("_Metallic", 0f);
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void EnsureSharedAssets()
        {
            if (_bubbleMaterialTemplate != null)
                return;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            var mat = new Material(shader);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_AlphaClip", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Smoothness", 0.88f);
            mat.SetFloat("_Metallic", 0.04f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetColor("_BaseColor", new Color(0.85f, 0.92f, 1f, 0.28f));
            _bubbleMaterialTemplate = mat;
        }
    }
}
