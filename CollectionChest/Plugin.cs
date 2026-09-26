using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace CollectionChest
{
    /// <summary>
    /// Runs on every client, and each client only ever works the chests its own game is
    /// managing - the ZDO owner, which Valheim hands to the first player nearby. That is the one
    /// peer allowed to write a chest's inventory, so it is the only one that can store an item
    /// without two players' copies of the chest disagreeing about what is in it.
    /// </summary>
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string ModGuid = "com.ljindustries.valheim.collectionchest";
        public const string ModName = "CollectionChest";
        public const string ModVersion = "1.0.0";

        internal static ManualLogSource Log;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            ModConfig.Bind(Config);

            // Patch per type so one method the game has renamed cannot abort every other patch.
            _harmony = new Harmony(ModGuid);
            foreach (var type in typeof(Plugin).Assembly.GetTypes())
            {
                try
                {
                    _harmony.CreateClassProcessor(type).Patch();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Harmony skip {type.FullName}: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
