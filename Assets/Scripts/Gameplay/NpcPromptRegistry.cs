using UnityEngine;

namespace DungeonExporer.Gameplay
{
    /// <summary>
    /// Collects the closest in-range NPC interaction label for the HUD (one scan per frame).
    /// </summary>
    public static class NpcPromptRegistry
    {
        private static int _frame = -1;
        private static float _bestSq = float.PositiveInfinity;
        private static string _bestLabel;

        private static void BeginScanIfNeeded()
        {
            int f = Time.frameCount;
            if (f == _frame)
                return;
            _frame = f;
            _bestSq = float.PositiveInfinity;
            _bestLabel = null;
        }

        public static void OfferCandidate(float distSq, string label)
        {
            if (string.IsNullOrEmpty(label))
                return;
            BeginScanIfNeeded();
            if (distSq < _bestSq)
            {
                _bestSq = distSq;
                _bestLabel = label;
            }
        }

        public static string CurrentLabel => _bestLabel;
    }
}
