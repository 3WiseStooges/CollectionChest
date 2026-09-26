using System;
using System.Collections.Generic;
using UnityEngine;

namespace CollectionChest
{
    /// <summary>
    /// Added to every chest a player can build. Holds that chest's switch and, while the switch
    /// is on and this client owns the chest, pulls nearby dropped items into it.
    ///
    /// The switch is a flag in the chest's ZDO, so it is saved with the world and every player
    /// sees the same setting. Only the ZDO owner writes it - toggling from the menu sends an RPC
    /// to the owner - which matters with MultiUserChest, where a second player can have the chest
    /// open without owning it.
    /// </summary>
    internal class Collector : MonoBehaviour
    {
        private const string RpcSetCollecting = "CollectionChest_SetCollecting";
        private static readonly int CollectingKey = "CollectionChest_Collecting".GetStableHashCode();

        // A pile of drops - a wall knocked down beside the chest, say - is worked through over a
        // few scans instead of all on one frame. Counts requests for ownership as well as
        // stores, so it also caps how many RPCs one scan can send.
        private const int MaxDropsPerScan = 16;

        private static readonly Collider[] Hits = new Collider[128];
        private static readonly HashSet<ItemDrop> Seen = new HashSet<ItemDrop>();
        private static int _itemMask;

        private Container _container;
        private ZNetView _nview;
        private Collider[] _colliders = Array.Empty<Collider>();
        private float _scheduledInterval;
        private bool _wasLeavingItems;
        private bool _loggedError;

        /// <summary>
        /// Attaches a collector to a chest that qualifies. Called from <c>Container.Awake</c>, which
        /// runs for the build-mode placement ghost too - that instance has no ZDO, and
        /// Container.Awake skips it for the same reason.
        /// </summary>
        internal static void TryAttach(Container container)
        {
            ZNetView nview = container.GetComponent<ZNetView>();
            if (nview == null || nview.GetZDO() == null) return;
            if (!IsEligible(container)) return;
            if (container.GetComponent<Collector>() != null) return;

            container.gameObject.AddComponent<Collector>();
        }

        /// <summary>
        /// Chests a player builds and stands in one place: not graves, carts, ship holds or loot
        /// bags. The obliterator is buildable and has an inventory too, but a chest that fills
        /// itself is one lever pull from destroying whatever wandered in, so it is left out.
        /// </summary>
        internal static bool IsEligible(Container container)
        {
            // Cart and ship storage hang their inventory off another object's ZNetView.
            if (container.m_rootObjectOverride != null || container.m_wagon != null) return false;

            // Loot bags that remove themselves once emptied.
            if (container.m_autoDestroyEmpty) return false;

            if (container.GetComponent<Piece>() == null) return false;

            if (container.GetComponentInParent<Incinerator>() != null) return false;
            if (container.GetComponentInParent<TombStone>() != null) return false;
            if (container.GetComponentInParent<Corpse>() != null) return false;
            if (container.GetComponentInParent<Ship>() != null) return false;
            if (container.GetComponentInParent<Vagon>() != null) return false;

            return true;
        }

        internal bool IsCollecting
        {
            get { return _nview != null && _nview.IsValid() && _nview.GetZDO().GetBool(CollectingKey); }
        }

        /// <summary>No empty slot left. Stacks already in the chest can still be topped up.</summary>
        internal bool IsFull
        {
            get
            {
                Inventory inventory = _container != null ? _container.GetInventory() : null;
                return inventory != null && inventory.GetEmptySlots() <= 0;
            }
        }

        /// <summary>
        /// Asks whoever owns the chest to flip the switch. Handled on the spot when that is this
        /// client, which without MultiUserChest it always is: opening a chest hands you it.
        /// </summary>
        internal void RequestCollecting(bool on)
        {
            if (_nview == null || !_nview.IsValid()) return;
            _nview.InvokeRPC(RpcSetCollecting, on);
        }

        private void Awake()
        {
            _container = GetComponent<Container>();
            _nview = GetComponent<ZNetView>();

            // Inactive ones too: a chest swaps between an open and a closed model, and whichever
            // is showing is checked at scan time.
            var colliders = new List<Collider>();
            foreach (Collider collider in GetComponentsInChildren<Collider>(true))
            {
                if (!collider.isTrigger) colliders.Add(collider);
            }
            _colliders = colliders.ToArray();

            try
            {
                _nview.Register<bool>(RpcSetCollecting, RPC_SetCollecting);
            }
            catch (ArgumentException)
            {
                // Already registered on this ZNetView; the first registration serves both.
            }

            Schedule();
        }

        private void RPC_SetCollecting(long sender, bool on)
        {
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner()) return;

            _nview.GetZDO().Set(CollectingKey, on);

            if (ModConfig.VerboseLogging.Value)
            {
                Plugin.Log.LogInfo($"{Describe()}: auto-collect {(on ? "on" : "off")}.");
            }

            // Start on whatever is already lying there rather than on the next tick.
            if (on) Scan();
        }

        private void Schedule()
        {
            _scheduledInterval = ModConfig.ScanInterval.Value;
            CancelInvoke(nameof(Scan));

            // A random start spreads a base full of chests across the interval instead of
            // having them all scan on the same frame.
            InvokeRepeating(nameof(Scan), UnityEngine.Random.Range(0f, _scheduledInterval), _scheduledInterval);
        }

        private void Scan()
        {
            // BepInEx config can change at runtime through ConfigurationManager.
            if (!Mathf.Approximately(_scheduledInterval, ModConfig.ScanInterval.Value))
            {
                Schedule();
                return;
            }

            if (!ModConfig.Enabled.Value || !IsCollecting || !_nview.IsOwner()) return;

            try
            {
                Collect();
            }
            catch (Exception ex)
            {
                if (_loggedError) return;
                _loggedError = true;
                Plugin.Log.LogError($"{Describe()}: collecting failed: {ex}");
            }
        }

        private void Collect()
        {
            if (ZNetScene.instance == null) return;

            Inventory inventory = _container.GetInventory();
            if (inventory == null) return;

            // Load anything another player wrote while they held the chest before writing to it.
            if (!GameAccess.TryRefresh(_container)) return;

            if (!Intake.HasAnyRoom(inventory))
            {
                NoteLeavingItems(true);
                return;
            }

            if (_itemMask == 0) _itemMask = LayerMask.GetMask("item");

            // A sphere that contains every point within range of the chest's bounds; the exact
            // distance to the chest itself is checked per item below.
            Bounds bounds = ChestBounds();
            float range = ModConfig.Range.Value;
            int count = Physics.OverlapSphereNonAlloc(bounds.center, bounds.extents.magnitude + range, Hits, _itemMask);

            int handled = 0;
            bool leftItems = false;
            try
            {
                for (int i = 0; i < count && handled < MaxDropsPerScan; i++)
                {
                    ItemDrop drop = Intake.DropFor(Hits[i]);

                    // An item with several colliders is hit once per collider.
                    if (drop == null || !Seen.Add(drop)) continue;
                    if (DistanceTo(drop.transform.position) > range) continue;

                    string name = drop.m_itemData?.m_shared?.m_name;
                    switch (Intake.TryStore(drop, inventory, out int stored))
                    {
                        case Intake.Outcome.Stored:
                            handled++;
                            if (ModConfig.VerboseLogging.Value)
                            {
                                Plugin.Log.LogInfo($"{Describe()}: stored {stored}x {name}.");
                            }
                            break;
                        case Intake.Outcome.Waiting:
                            handled++;
                            break;
                        case Intake.Outcome.NoRoom:
                            leftItems = true;
                            break;
                    }
                }
            }
            finally
            {
                Array.Clear(Hits, 0, count);
                Seen.Clear();
            }

            NoteLeavingItems(leftItems);
        }

        /// <summary>Union of the chest's solid colliders in world space.</summary>
        private Bounds ChestBounds()
        {
            bool any = false;
            Bounds bounds = default;
            foreach (Collider collider in _colliders)
            {
                if (!Usable(collider)) continue;
                if (any)
                {
                    bounds.Encapsulate(collider.bounds);
                }
                else
                {
                    bounds = collider.bounds;
                    any = true;
                }
            }

            // A chest with no collider of its own: treat it as a one-metre box on its pivot.
            return any ? bounds : new Bounds(transform.position + Vector3.up * 0.5f, Vector3.one);
        }

        /// <summary>
        /// Distance from the chest's own surface, so an item resting on the lid is at 0 and a
        /// long chest reaches no further past its ends than a small one does.
        /// </summary>
        private float DistanceTo(Vector3 point)
        {
            float best = float.MaxValue;
            foreach (Collider collider in _colliders)
            {
                if (!Usable(collider)) continue;

                Vector3 closest = HasExactClosestPoint(collider)
                    ? collider.ClosestPoint(point)
                    : collider.bounds.ClosestPoint(point);
                best = Mathf.Min(best, (closest - point).sqrMagnitude);
            }

            if (best == float.MaxValue) best = ChestBounds().SqrDistance(point);
            return Mathf.Sqrt(best);
        }

        private static bool Usable(Collider collider)
        {
            return collider != null && collider.enabled && collider.gameObject.activeInHierarchy;
        }

        /// <summary>
        /// Collider.ClosestPoint only supports these; anything else, a concave mesh collider
        /// in particular, is measured to its bounding box instead.
        /// </summary>
        private static bool HasExactClosestPoint(Collider collider)
        {
            return collider is BoxCollider
                   || collider is SphereCollider
                   || collider is CapsuleCollider
                   || (collider is MeshCollider mesh && mesh.convex);
        }

        private void NoteLeavingItems(bool leaving)
        {
            if (leaving == _wasLeavingItems) return;
            _wasLeavingItems = leaving;

            if (ModConfig.VerboseLogging.Value)
            {
                Plugin.Log.LogInfo(leaving
                    ? $"{Describe()}: out of room, leaving items on the ground."
                    : $"{Describe()}: has room again.");
            }
        }

        private string Describe()
        {
            Vector3 p = transform.position;
            return $"{_container.m_name} at ({p.x:F0}, {p.y:F0}, {p.z:F0})";
        }
    }
}
