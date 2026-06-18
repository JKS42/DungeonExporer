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
        public SaveQuestDefinition[] dynamicQuestDefinitions;
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

    [Serializable]
    public struct SaveQuestDefinition
    {
        public string id;
        public string title;
        public string briefing;
        public string completionSummary;
        public string prerequisiteQuestIdCompleted;
        public string[] objectiveEvents;
        public string[] objectiveHudHints;
    }
}
