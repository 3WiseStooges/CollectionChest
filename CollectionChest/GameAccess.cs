using System;
using HarmonyLib;

namespace CollectionChest
{
    /// <summary>
    /// The two private game members the mod needs, resolved once on first use. If a game update
    /// renames one, the failure is logged and only the feature that needs it switches off -
    /// never a guess that could put an item in two places.
    /// </summary>
    internal static class GameAccess
    {
        private static Func<Container, bool> _containerLoad;
        private static bool _containerLoadResolved;

        private static AccessTools.FieldRef<InventoryGui, Container> _currentContainer;
        private static bool _currentContainerResolved;

        /// <summary>
        /// Loads whatever another player last wrote to this chest, if its ZDO has moved on since
        /// this client read it. Vanilla only does that once a second, in
        /// <c>Container.CheckForChanges</c>, so a client that has just been handed the chest can
        /// be holding a copy up to a second old - and writing on top of that would silently
        /// undo the other player's changes. Returns false when the reload is unavailable, in
        /// which case the caller must leave the inventory alone.
        /// </summary>
        internal static bool TryRefresh(Container container)
        {
            if (!_containerLoadResolved)
            {
                _containerLoadResolved = true;
                try
                {
                    _containerLoad = AccessTools.MethodDelegate<Func<Container, bool>>(
                        AccessTools.Method(typeof(Container), "Load", Type.EmptyTypes));
                }
                catch (Exception ex)
                {
                    _containerLoad = null;
                    Plugin.Log.LogError(
                        $"Container.Load not found ({ex.GetType().Name}); chests will not collect, " +
                        "because a chest this client cannot refresh might overwrite another player's changes.");
                }
            }

            if (_containerLoad == null) return false;

            // A no-op when nothing has changed, or while the chest is open here - and while it
            // is open here, this client is the only one that can have written to it.
            _containerLoad(container);
            return true;
        }

        /// <summary>The chest whose inventory the open menu is showing, or null.</summary>
        internal static Container CurrentContainer(InventoryGui gui)
        {
            if (gui == null) return null;

            if (!_currentContainerResolved)
            {
                _currentContainerResolved = true;
                try
                {
                    _currentContainer = AccessTools.FieldRefAccess<InventoryGui, Container>("m_currentContainer");
                }
                catch (Exception ex)
                {
                    _currentContainer = null;
                    Plugin.Log.LogError(
                        $"InventoryGui.m_currentContainer not found ({ex.GetType().Name}); the chest " +
                        "menu switch is disabled. Chests already switched on keep collecting.");
                }
            }

            return _currentContainer?.Invoke(gui);
        }
    }
}
