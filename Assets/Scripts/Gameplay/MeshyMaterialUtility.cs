using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DungeonExporer.Gameplay
{
    /// <summary>
    /// Applies URP Lit materials using Meshy-exported PNG albedos when FBX embedded materials fail to render.
    /// </summary>
    public static class MeshyMaterialUtility
    {
        public static void ApplyUrpLitAlbedo(GameObject visualRoot, Texture2D albedo)
        {
            if (visualRoot == null || albedo == null)
                return;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                return;

            Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var mat = new Material(shader);
                mat.SetTexture("_BaseMap", albedo);
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Smoothness", 0.38f);
                mat.SetFloat("_Metallic", 0.05f);
                renderers[i].sharedMaterial = mat;
            }
        }

#if UNITY_EDITOR
        public static Texture2D LoadAlbedoFromFbxPath(string fbxAssetPath)
        {
            if (string.IsNullOrEmpty(fbxAssetPath))
                return null;

            string pngPath = fbxAssetPath.Replace(".fbx", ".png");
            return AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
        }
#endif
    }
}
