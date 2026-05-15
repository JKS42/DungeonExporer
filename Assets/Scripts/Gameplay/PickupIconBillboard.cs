using UnityEngine;

namespace DungeonExporer.Gameplay
{
    /// <summary>
    /// Keeps a pickup icon facing the active camera.
    /// </summary>
    public sealed class PickupIconBillboard : MonoBehaviour
    {
        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;

            transform.forward = cam.transform.forward;
        }
    }
}
