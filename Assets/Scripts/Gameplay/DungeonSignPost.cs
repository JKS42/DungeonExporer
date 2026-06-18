using DungeonExporer.UI;
using TMPro;
using UnityEngine;

namespace DungeonExporer.Gameplay
{
    /// <summary>
    /// World-space sign text that billboards toward the camera.
    /// </summary>
    public sealed class DungeonSignPost : MonoBehaviour
    {
        [SerializeField] private float _height = 1.35f;

        public static DungeonSignPost Create(Transform parent, Vector3 floorPosition, string text, float cellSize)
        {
            var root = new GameObject("SignPost");
            root.transform.SetParent(parent, false);
            root.transform.position = floorPosition + new Vector3(0f, 0f, 0f);

            float postW = Mathf.Max(0.35f, cellSize * 0.22f);
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Board";
            board.transform.SetParent(root.transform, false);
            board.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            board.transform.localScale = new Vector3(postW * 3.2f, postW * 1.6f, 0.08f);
            Object.Destroy(board.GetComponent<Collider>());

            var rend = board.GetComponent<Renderer>();
            if (rend != null)
                rend.material.color = new Color(0.58f, 0.42f, 0.28f, 1f);

            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "Post";
            post.transform.SetParent(root.transform, false);
            post.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            post.transform.localScale = new Vector3(postW * 0.35f, 0.55f, postW * 0.35f);
            Object.Destroy(post.GetComponent<Collider>());
            var postRend = post.GetComponent<Renderer>();
            if (postRend != null)
                postRend.material.color = new Color(0.45f, 0.32f, 0.2f, 1f);

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(board.transform, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, -0.55f);
            textGo.transform.localRotation = Quaternion.identity;
            textGo.transform.localScale = Vector3.one * 0.024f;

            var tmp = textGo.AddComponent<TextMeshPro>();
            tmp.text = text ?? string.Empty;
            tmp.fontSize = 48f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.16f, 0.11f, 0.07f, 1f);
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.rectTransform.sizeDelta = new Vector2(320f, 96f);
            TmpTextUtility.ApplyReadableDefaults(tmp);

            var sign = root.AddComponent<DungeonSignPost>();
            sign._height = 1.35f;
            return sign;
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;

            Transform label = transform.Find("Board/Label");
            if (label == null)
                return;

            label.rotation = Quaternion.LookRotation(label.position - cam.transform.position);
        }
    }
}
