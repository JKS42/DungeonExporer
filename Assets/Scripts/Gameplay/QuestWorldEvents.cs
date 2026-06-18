namespace DungeonExporer.Gameplay
{
    /// <summary>
    /// Authoritative world-event ids that quests (including AI-generated side quests) may reference.
    /// </summary>
    public static class QuestWorldEvents
    {
        public const string DefeatedDungeonFoe = "defeated_dungeon_foe";
        public const string EnteredEncounterZone = "entered_encounter_zone";
        public const string CollectedPebble = "collected_pebble";
        public const string CollectedTrailRation = "collected_trail_ration";
        public const string EnteredSafeRoom = "entered_safe_room";

        public static readonly string[] All =
        {
            DefeatedDungeonFoe,
            EnteredEncounterZone,
            CollectedPebble,
            CollectedTrailRation,
            EnteredSafeRoom
        };

        public static string BuildCatalogForPrompt()
        {
            return
                "- defeated_dungeon_foe — defeat one foe on a crimson encounter (E) floor\n" +
                "- entered_encounter_zone — stand on a crimson encounter (E) floor once\n" +
                "- collected_pebble — pick up a wobbly pebble loot bubble\n" +
                "- collected_trail_ration — pick up a trail ration loot bubble\n" +
                "- entered_safe_room — visit a mint-green safe hub (S) floor";
        }

        public static bool IsAllowed(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId))
                return false;

            string trimmed = eventId.Trim();
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i] == trimmed)
                    return true;
            }

            return false;
        }

        public static void NotifyPickup(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return;

            if (itemId == "dungeon_pebble")
                QuestManager.Instance?.NotifyWorldEvent(CollectedPebble);
            else if (itemId == "trail_ration")
                QuestManager.Instance?.NotifyWorldEvent(CollectedTrailRation);
        }
    }
}
