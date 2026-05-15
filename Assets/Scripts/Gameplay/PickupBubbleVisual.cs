using UnityEngine;
using UnityEngine.Rendering;

namespace DungeonExporer.Gameplay
{
    /// <summary>
    /// Builds a translucent bubble shell and a small centre icon for world pickups.
    /// </summary>
    public static class PickupBubbleVisual
    {
        private const int IconResolution = 64;

        private static Material _bubbleMaterialTemplate;
        private static Material _iconMaterialTemplate;
        private static Texture2D _healIcon;
        private static Texture2D _pebbleIcon;

        public static void Apply(GameObject root, string itemId, float healAmount, Color accentTint)
        {
            if (root == null)
                return;

            EnsureSharedAssets();

            bool isHeal = healAmount > 0f || itemId == "trail_ration";
            Texture2D icon = isHeal ? _healIcon : _pebbleIcon;
            Color bubbleTint = accentTint;
            bubbleTint.a = 0.22f;
            Color iconTint = isHeal ? new Color(0.35f, 0.82f, 0.42f, 1f) : new Color(0.62f, 0.54f, 0.42f, 1f);

            MeshRenderer shell = root.GetComponent<MeshRenderer>();
            if (shell != null)
            {
                Material bubble = new Material(_bubbleMaterialTemplate);
                bubble.SetColor("_BaseColor", bubbleTint);
                shell.sharedMaterial = bubble;
            }

            Transform existing = root.transform.Find("PickupIcon");
            if (existing != null)
                Object.Destroy(existing.gameObject);

            var iconGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            iconGo.name = "PickupIcon";
            iconGo.transform.SetParent(root.transform, false);
            iconGo.transform.localPosition = Vector3.zero;
            iconGo.transform.localRotation = Quaternion.identity;
            iconGo.transform.localScale = Vector3.one * 0.38f;

            Object.Destroy(iconGo.GetComponent<Collider>());

            Material iconMat = new Material(_iconMaterialTemplate);
            iconMat.SetTexture("_BaseMap", icon);
            iconMat.SetColor("_BaseColor", iconTint);
            iconGo.GetComponent<MeshRenderer>().sharedMaterial = iconMat;
            iconGo.AddComponent<PickupIconBillboard>();
        }

        private static void EnsureSharedAssets()
        {
            if (_healIcon == null)
                _healIcon = CreateHealCrossIcon();
            if (_pebbleIcon == null)
                _pebbleIcon = CreatePebbleIcon();

            if (_bubbleMaterialTemplate == null)
                _bubbleMaterialTemplate = CreateBubbleMaterialTemplate();
            if (_iconMaterialTemplate == null)
                _iconMaterialTemplate = CreateIconMaterialTemplate();
        }

        private static Material CreateBubbleMaterialTemplate()
        {
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
            mat.SetFloat("_Smoothness", 0.9f);
            mat.SetFloat("_Metallic", 0.05f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetColor("_BaseColor", new Color(0.85f, 0.92f, 1f, 0.22f));
            return mat;
        }

        private static Material CreateIconMaterialTemplate()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Transparent");

            var mat = new Material(shader);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent + 1;
            return mat;
        }

        private static Texture2D CreateHealCrossIcon()
        {
            int s = IconResolution;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var clear = new Color(0f, 0f, 0f, 0f);
            var pixels = new Color[s * s];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clear;

            int arm = s / 5;
            int half = s / 2;
            FillRect(pixels, s, half - arm / 2, half - arm * 2, arm, arm * 4, Color.white);
            FillRect(pixels, s, half - arm * 2, half - arm / 2, arm * 4, arm, Color.white);

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static Texture2D CreatePebbleIcon()
        {
            int s = IconResolution;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color[s * s];
            float cx = (s - 1) * 0.5f;
            float cy = (s - 1) * 0.5f;
            float radius = s * 0.22f;

            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dx = x - cx;
                    float dy = (y - cy) * 1.15f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(1f - (dist - radius) / 2.5f);
                    pixels[y * s + x] = new Color(1f, 1f, 1f, alpha * alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static void FillRect(Color[] pixels, int size, int x0, int y0, int w, int h, Color color)
        {
            for (int y = y0; y < y0 + h; y++)
            {
                if (y < 0 || y >= size)
                    continue;
                for (int x = x0; x < x0 + w; x++)
                {
                    if (x < 0 || x >= size)
                        continue;
                    pixels[y * size + x] = color;
                }
            }
        }
    }
}
