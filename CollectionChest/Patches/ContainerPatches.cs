using HarmonyLib;

namespace CollectionChest.Patches
{
    /// <summary>
    /// Container.Awake is where a chest builds its inventory and registers its RPCs, and it runs
    /// for every chest instance, loaded or freshly built. A postfix there sees each one once.
    /// </summary>
    [HarmonyPatch(typeof(Container), "Awake")]
    internal static class ContainerAwakePatch
    {
        private static void Postfix(Container __instance)
        {
            Collector.TryAttach(__instance);
        }
    }

    /// <summary>
    /// One extra line on a switched-on chest, so a base full of chests shows which ones are
    /// collecting without opening each of them. Read from the ZDO, so every player sees it,
    /// not just whichever one owns the chest.
    /// </summary>
    [HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
    internal static class ContainerHoverTextPatch
    {
        private static void Postfix(Container __instance, ref string __result)
        {
            if (!ModConfig.Enabled.Value || !ModConfig.ShowHoverStatus.Value) return;

            Collector collector = __instance.GetComponent<Collector>();
            if (collector == null || !collector.IsCollecting) return;

            __result += collector.IsFull
                ? "\n<color=#E8A33D>Auto-collect on - full</color>"
                : "\n<color=#9BD86B>Auto-collect on</color>";
        }
    }
}
