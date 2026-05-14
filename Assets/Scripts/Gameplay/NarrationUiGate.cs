namespace DungeonExporer.Gameplay
{
    /// <summary>
    /// Lightweight flags so dungeon narration code does not reference UI assemblies/types (avoids fragile compile cycles).
    /// </summary>
    public static class NarrationUiGate
    {
        public static bool DialogueOpen;
        public static bool PauseOpen;
    }
}
