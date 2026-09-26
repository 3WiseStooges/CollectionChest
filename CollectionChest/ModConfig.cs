using BepInEx.Configuration;

namespace CollectionChest
{
    internal static class ModConfig
    {
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> Range;
        internal static ConfigEntry<float> ScanInterval;

        internal static ConfigEntry<bool> ShowHoverStatus;

        internal static ConfigEntry<bool> VerboseLogging;

        internal static void Bind(ConfigFile config)
        {
            Enabled = config.Bind(
                "General", "Enabled", true,
                "Master switch. While off no chest collects anything and the switch is hidden from " +
                "the chest menu. Chests you have already switched on remember it and pick back up " +
                "when you turn this on again.");

            Range = config.Bind(
                "Collection", "Range", 1f,
                new ConfigDescription(
                    "How far a dropped item can be from the chest and still get pulled in, in metres. " +
                    "Measured from the chest's sides and lid, not its centre, so an item resting on the " +
                    "chest is at 0 and a bigger chest does not reach further than a small one. " +
                    "Kept tight on purpose: a chest under a smelter should catch that smelter's output " +
                    "and not the one beside it.",
                    new AcceptableValueRange<float>(0.25f, 4f)));

            ScanInterval = config.Bind(
                "Collection", "ScanInterval", 1f,
                new ConfigDescription(
                    "Seconds between each switched-on chest checking for dropped items. Machines " +
                    "produce every 30 seconds or more, so there is little to gain below 1.",
                    new AcceptableValueRange<float>(0.25f, 10f)));

            ShowHoverStatus = config.Bind(
                "Display", "ShowHoverStatus", true,
                "Add a line to a switched-on chest's hover text, and say when it is full.");

            VerboseLogging = config.Bind(
                "Advanced", "VerboseLogging", false,
                "Log every item a chest collects, and why it left one on the ground, to the BepInEx console.");
        }
    }
}
