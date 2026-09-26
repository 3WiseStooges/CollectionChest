using System;
using UnityEngine;

namespace CollectionChest
{
    /// <summary>
    /// Moving one dropped item into one chest inventory, and the rules for what counts.
    ///
    /// Every rule here exists so a chest can never hold more than it has room for, and an item
    /// can never end up both in the chest and on the floor. The room check mirrors exactly what
    /// <c>Inventory.AddItem</c> will accept; the move is sized to that room; and the drop is then
    /// shrunk by what the chest actually gained, counted afterwards, rather than by what the
    /// arithmetic expected.
    /// </summary>
    internal static class Intake
    {
        internal enum Outcome
        {
            /// <summary>Not something a chest should take, or no longer there.</summary>
            Ignored,

            /// <summary>Collectable, but the chest has no room for any of it.</summary>
            NoRoom,

            /// <summary>Another peer owns the drop, or it has only just spawned; asked for it.</summary>
            Waiting,

            /// <summary>All or part of the stack is now in the chest.</summary>
            Stored,
        }

        /// <summary>The item a physics hit belongs to, following vanilla's auto-pickup.</summary>
        internal static ItemDrop DropFor(Collider hit)
        {
            Rigidbody body = hit.attachedRigidbody;
            if (body == null) return null;

            ItemDrop drop = body.GetComponent<ItemDrop>();
            if (drop != null) return drop;

            // An item floating in water collides through a dummy body; Player.AutoPickup follows
            // it back to the item the same way.
            FloatingTerrainDummy dummy = body.GetComponent<FloatingTerrainDummy>();
            if (dummy == null || dummy.m_parent == null) return null;
            return dummy.m_parent.GetComponent<ItemDrop>();
        }

        internal static Outcome TryStore(ItemDrop drop, Inventory inventory, out int stored)
        {
            stored = 0;

            ZNetView nview = drop.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return Outcome.Ignored;

            // Whoever owned the drop last may have changed its stack or variant; read the
            // current ones before judging it.
            drop.Load();
            if (!IsCollectable(drop)) return Outcome.Ignored;

            ItemDrop.ItemData item = drop.m_itemData;
            if (item.m_stack <= 0) return Outcome.Ignored;

            int room = RoomFor(inventory, item);
            if (room <= 0) return Outcome.NoRoom;

            // Only the owner of a drop can remove it for good. ZNetScene.Destroy on anyone else's
            // copy deletes the local object but leaves the ZDO, so the item would come back while
            // also sitting in the chest. CanPickup is also false for the first half second after
            // a drop spawns, the same grace vanilla gives its own auto-pickup; RequestOwn is a
            // no-op when this client already owns it.
            if (!drop.CanPickup())
            {
                drop.RequestOwn();
                return Outcome.Waiting;
            }

            stored = Store(drop, nview, inventory, room);
            return stored > 0 ? Outcome.Stored : Outcome.NoRoom;
        }

        /// <summary>
        /// How many of this item the inventory can still take. Mirrors <c>Inventory.AddItem</c>:
        /// it tops up any stack with the same name, quality and world level first
        /// (<c>FindFreeStackItem</c>), then needs an empty slot for the rest.
        /// <c>Inventory.CanAddItem</c> is close but ignores quality, so it can promise room that
        /// <c>AddItem</c> will not give.
        /// </summary>
        internal static int RoomFor(Inventory inventory, ItemDrop.ItemData item)
        {
            int emptySlots = Mathf.Max(0, inventory.GetEmptySlots());
            int maxStack = item.m_shared.m_maxStackSize;
            if (maxStack <= 1) return emptySlots;

            long room = (long)emptySlots * maxStack;
            foreach (ItemDrop.ItemData held in inventory.GetAllItems())
            {
                if (SameStack(held, item) && held.m_stack < held.m_shared.m_maxStackSize)
                {
                    room += held.m_shared.m_maxStackSize - held.m_stack;
                }
            }

            return (int)Math.Min(room, int.MaxValue);
        }

        /// <summary>False only when the chest could not take a single unit of anything.</summary>
        internal static bool HasAnyRoom(Inventory inventory)
        {
            if (inventory.GetEmptySlots() > 0) return true;

            foreach (ItemDrop.ItemData held in inventory.GetAllItems())
            {
                if (held.m_stack < held.m_shared.m_maxStackSize) return true;
            }

            return false;
        }

        private static bool IsCollectable(ItemDrop drop)
        {
            // Food set out on a table is an ItemDrop turned into a building piece.
            if (drop.IsPiece()) return false;

            ItemDrop.ItemData item = drop.m_itemData;
            if (item == null || item.m_shared == null) return false;

            // A chest saves each item by its prefab and drops anything it cannot find on load,
            // so an item with no prefab would be stored and then quietly vanish.
            if (item.m_dropPrefab == null) return false;

            // Items the game wants picked up by hand, not vacuumed up by walking past - the same
            // prefab flag vanilla's auto-pickup respects. Read from the prefab, not the drop:
            // a drop the player threw down has the flag cleared on their client only, and the
            // chest should not behave differently depending on who happens to own it.
            ItemDrop prefab = item.m_dropPrefab.GetComponent<ItemDrop>();
            if (prefab != null && !prefab.m_autoPickup) return false;

            if (item.m_shared.m_questItem) return false;

            // The same guard Humanoid.Pickup has: an item the inventory grid cannot draw.
            Sprite[] icons = item.m_shared.m_icons;
            if (icons == null || icons.Length == 0 || item.m_variant >= icons.Length) return false;

            // Vanilla will not let anyone pick up an item stuck in tar.
            if (drop.InTar()) return false;

            // Fish are ItemDrops even while swimming; only a landed one is a dropped item.
            Fish fish = drop.GetComponent<Fish>();
            if (fish != null && !fish.IsOutOfWater()) return false;

            return true;
        }

        private static int Store(ItemDrop drop, ZNetView nview, Inventory inventory, int room)
        {
            ItemDrop.ItemData item = drop.m_itemData;
            int stack = item.m_stack;
            int before = CountOf(inventory, item);
            int stored = 0;

            try
            {
                AddUpTo(inventory, item, Mathf.Min(stack, room));
            }
            finally
            {
                // Settle the drop from what the chest actually gained, not from the loop above.
                // Whatever happened in between - another mod patching AddItem, an exception out
                // of an inventory-changed callback - the floor loses exactly what the chest got.
                stored = CountOf(inventory, item) - before;
                if (stored >= stack)
                {
                    ZNetScene.instance.Destroy(drop.gameObject);
                }
                else if (stored > 0)
                {
                    // What ItemDrop.Save does, minus SetStack's clamp to the max stack size:
                    // a drop that somehow holds more than a stack must keep every unit it had.
                    item.m_stack = stack - stored;
                    ItemDrop.SaveToZDO(item, nview.GetZDO());
                }
            }

            return Mathf.Max(0, stored);
        }

        /// <summary>
        /// Adds up to <paramref name="want"/> of <paramref name="item"/> without touching the item
        /// itself. Callers size <paramref name="want"/> with <see cref="RoomFor"/> and count the
        /// result with <see cref="CountOf"/>; this makes no promise about how much went in.
        /// </summary>
        internal static void AddUpTo(Inventory inventory, ItemDrop.ItemData item, int want)
        {
            int maxStack = Mathf.Max(1, item.m_shared.m_maxStackSize);
            int left = want;
            while (left > 0)
            {
                // At most one stack per call. AddItem tops up existing stacks first and then puts
                // whatever is left into a single new slot, so a bigger remainder would leave that
                // slot over its stack size. A fresh clone each time, because AddItem keeps the
                // object it was handed when it fills a new slot with it.
                ItemDrop.ItemData portion = item.Clone();
                portion.m_stack = Mathf.Min(left, maxStack);
                portion.m_equipped = false;

                int asked = portion.m_stack;
                bool added = inventory.AddItem(portion);

                // A refused AddItem may still have topped up some stacks before running out of
                // slots, and leaves the part it could not place in portion.m_stack.
                int placed = added ? asked : asked - portion.m_stack;
                if (placed <= 0) break;

                left -= placed;
                if (!added) break;
            }
        }

        internal static int CountOf(Inventory inventory, ItemDrop.ItemData item)
        {
            int count = 0;
            foreach (ItemDrop.ItemData held in inventory.GetAllItems())
            {
                if (SameStack(held, item)) count += held.m_stack;
            }

            return count;
        }

        /// <summary>The match <c>Inventory.FindFreeStackItem</c> uses to decide what stacks together.</summary>
        private static bool SameStack(ItemDrop.ItemData a, ItemDrop.ItemData b)
        {
            return a.m_shared.m_name == b.m_shared.m_name
                   && a.m_quality == b.m_quality
                   && a.m_worldLevel == b.m_worldLevel;
        }
    }
}
