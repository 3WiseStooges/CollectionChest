using HarmonyLib;

namespace CollectionChest.Patches
{
    /// <summary>
    /// UpdateContainer is where vanilla shows or hides the chest panel each frame, so the switch
    /// is kept in step with it there. It is private; if an update renames it, Plugin.Awake logs
    /// a "Harmony skip" line and the switch never appears, while chests already switched on
    /// carry on collecting. MultiUserChest transpiles the same method, which a postfix does not
    /// disturb.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), "UpdateContainer")]
    internal static class InventoryGuiUpdateContainerPatch
    {
        private static void Postfix(InventoryGui __instance)
        {
            CollectToggle.Refresh(__instance);
        }
    }
}
