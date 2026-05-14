using System;

namespace DungeonExporer.Dungeon
{
    /// <summary>
    /// HUD subscribes here so dungeon code never references UI types (breaks PlayerHealth → Dungeon → UI cycles).
    /// </summary>
    public static class DungeonFlavorHudBridge
    {
        public static Action<string, float> PublishFlavorToast;
    }
}
