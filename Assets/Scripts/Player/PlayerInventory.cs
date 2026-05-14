using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DungeonExporer.Player
{
    /// <summary>
    /// Lightweight stack-based inventory keyed by string ids (loot, keys, quest items).
    /// </summary>
    public sealed class PlayerInventory : MonoBehaviour
    {
        public static PlayerInventory Instance { get; private set; }

        [Serializable]
        public sealed class StackEntry
        {
            public string id;
            public string displayName;
            public int count;
        }

        public event Action OnChanged;

        [SerializeField] private int _maxDistinctItems = 28;

        private readonly Dictionary<string, StackEntry> _items = new();

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public bool TryAdd(string id, string displayName, int count)
        {
            if (string.IsNullOrWhiteSpace(id) || count <= 0)
                return false;

            if (_items.TryGetValue(id, out StackEntry stack))
            {
                stack.count += count;
                if (string.IsNullOrWhiteSpace(stack.displayName) && !string.IsNullOrWhiteSpace(displayName))
                    stack.displayName = displayName;
            }
            else
            {
                if (_items.Count >= _maxDistinctItems)
                {
                    Debug.LogWarning("PlayerInventory: bag is full.");
                    return false;
                }

                _items[id] = new StackEntry
                {
                    id = id,
                    displayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName,
                    count = count
                };
            }

            OnChanged?.Invoke();
            return true;
        }

        public int GetCount(string id) =>
            string.IsNullOrWhiteSpace(id) || !_items.TryGetValue(id, out StackEntry s) ? 0 : s.count;

        public IEnumerable<StackEntry> EnumerateStacks() => _items.Values;

        public string BuildSummaryForPrompt()
        {
            if (_items.Count == 0)
                return "Inventory: empty.";

            var sb = new StringBuilder();
            sb.Append("Inventory: ");
            foreach (StackEntry e in _items.Values)
                sb.Append(e.displayName).Append(" x").Append(e.count).Append("; ");
            return sb.ToString().TrimEnd(' ', ';');
        }
    }
}
