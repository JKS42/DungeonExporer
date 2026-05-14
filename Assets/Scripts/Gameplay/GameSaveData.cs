using System;
using UnityEngine;

namespace DungeonExporer.Gameplay
{
    [Serializable]
    public sealed class GameSaveData
    {
        public int version = 1;
        public float px, py, pz;
        public string[] completedQuestIds;
        public SaveQuestProgress[] activeQuests;
        public SaveInventoryStack[] inventory;
    }

    [Serializable]
    public struct SaveQuestProgress
    {
        public string questId;
        public int objectiveIndex;
    }

    [Serializable]
    public struct SaveInventoryStack
    {
        public string id;
        public string displayName;
        public int count;
    }
}
