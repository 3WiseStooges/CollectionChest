using System;
using System.Collections.Generic;
using CollectionChest;
using UnityEngine;

/// <summary>
/// Drives Intake.RoomFor / AddUpTo / CountOf against Valheim's own Inventory, outside the game.
/// Every scenario also checks the invariants the mod exists to keep: what the chest gained is
/// what was counted, never more than there was room for, no slot past its stack size, and the
/// dropped item's own data left untouched.
/// </summary>
internal static class Program
{
    private static int _failures;
    private static int _warnings;

    private static readonly ItemDrop.ItemData.SharedData Copper = Shared("$item_copper", 50);
    private static readonly ItemDrop.ItemData.SharedData Tin = Shared("$item_tin", 50);
    private static readonly ItemDrop.ItemData.SharedData Mead = Shared("$item_mead_test", 20);
    private static readonly ItemDrop.ItemData.SharedData Sword = Shared("$item_sword_test", 1, ItemDrop.ItemData.ItemType.OneHandedWeapon);

    private static int Main()
    {
        // Inventory logs through ZLog -> UnityEngine.Debug, whose default handler is native.
        Debug.unityLogger.logHandler = new CountingLogHandler();

        Scenario("empty chest takes the whole drop", () =>
        {
            var chest = new Inventory("chest", null, 5, 2);
            Intake_(chest, Item(Copper, 30), expectRoom: 500, expectStored: 30);
            Expect(chest.NrOfItems() == 1, "one slot used");
        });

        Scenario("tops up a matching stack before opening a slot", () =>
        {
            var chest = new Inventory("chest", null, 5, 2);
            chest.AddItem(Item(Copper, 45));
            Intake_(chest, Item(Copper, 20), expectRoom: 5 + 9 * 50, expectStored: 20);
            Expect(chest.NrOfItems() == 2, "45 topped to 50, 15 in a new slot");
        });

        Scenario("takes only what fits and leaves the rest", () =>
        {
            var chest = new Inventory("chest", null, 1, 1);
            chest.AddItem(Item(Copper, 45));
            Intake_(chest, Item(Copper, 20), expectRoom: 5, expectStored: 5);
        });

        Scenario("a full chest has no room for anything else", () =>
        {
            var chest = new Inventory("chest", null, 1, 1);
            chest.AddItem(Item(Copper, 50));
            Expect(Intake.RoomFor(chest, Item(Tin, 1)) == 0, "no room for tin");
            Expect(Intake.RoomFor(chest, Item(Copper, 1)) == 0, "no room for more copper");
            Expect(!Intake.HasAnyRoom(chest), "HasAnyRoom is false");
        });

        Scenario("a partial stack keeps HasAnyRoom true", () =>
        {
            var chest = new Inventory("chest", null, 1, 1);
            chest.AddItem(Item(Copper, 49));
            Expect(Intake.HasAnyRoom(chest), "HasAnyRoom is true");
        });

        Scenario("different quality does not count as stack room (CanAddItem says it does)", () =>
        {
            var chest = new Inventory("chest", null, 2, 1);
            chest.AddItem(Item(Mead, 10, quality: 2));
            ItemDrop.ItemData drop = Item(Mead, 30, quality: 1);

            Expect(chest.CanAddItem(drop), "vanilla CanAddItem promises room for all 30");
            Intake_(chest, drop, expectRoom: 20, expectStored: 20);
        });

        Scenario("different world level does not count as stack room", () =>
        {
            var chest = new Inventory("chest", null, 2, 1);
            chest.AddItem(Item(Copper, 10, worldLevel: 1));
            Intake_(chest, Item(Copper, 60, worldLevel: 0), expectRoom: 50, expectStored: 50);
        });

        Scenario("an oversized drop goes in one stack per slot", () =>
        {
            var chest = new Inventory("chest", null, 2, 2);
            Intake_(chest, Item(Copper, 120), expectRoom: 200, expectStored: 120);
            Expect(chest.NrOfItems() == 3, "50 + 50 + 20");
        });

        Scenario("non-stackables need a slot each", () =>
        {
            var chest = new Inventory("chest", null, 2, 1);
            chest.AddItem(Item(Sword, 1));
            Intake_(chest, Item(Sword, 1), expectRoom: 1, expectStored: 1);
            Expect(Intake.RoomFor(chest, Item(Sword, 1)) == 0, "then none");
        });

        Scenario("asked for more than fits: AddItem refuses part way and the count still holds", () =>
        {
            // Bypasses RoomFor on purpose, to drive AddItem down its refuse-after-topping-up
            // path. The mod never asks for more than RoomFor, but the drop is settled from the
            // count either way, and this is the path that makes that necessary.
            var chest = new Inventory("chest", null, 1, 1);
            chest.AddItem(Item(Copper, 45));
            ItemDrop.ItemData drop = Item(Copper, 20);
            int warningsBefore = _warnings;

            int before = Intake.CountOf(chest, drop);
            Intake.AddUpTo(chest, drop, 20);
            int stored = Intake.CountOf(chest, drop) - before;

            Expect(stored == 5, $"stored 5 (got {stored})");
            Expect(_warnings > warningsBefore, "AddItem took its refusal path");
            Expect(drop.m_stack == 20, "drop data untouched");
            CheckStacks(chest);
        });

        System.Console.WriteLine(_failures == 0 ? "ALL PASSED" : $"{_failures} FAILED");
        return _failures == 0 ? 0 : 1;
    }

    /// <summary>What Intake.Store does, minus the ZDO: size by RoomFor, add, count.</summary>
    private static void Intake_(Inventory chest, ItemDrop.ItemData drop, int expectRoom, int expectStored)
    {
        int stackBefore = drop.m_stack;
        int room = Intake.RoomFor(chest, drop);
        Expect(room == expectRoom, $"room {expectRoom} (got {room})");

        int before = Intake.CountOf(chest, drop);
        Intake.AddUpTo(chest, drop, Math.Min(drop.m_stack, room));
        int stored = Intake.CountOf(chest, drop) - before;

        Expect(stored == expectStored, $"stored {expectStored} (got {stored})");
        Expect(stored <= room, "never more than the room it had");
        Expect(drop.m_stack == stackBefore, "drop data untouched");
        foreach (ItemDrop.ItemData held in chest.GetAllItems())
        {
            Expect(!ReferenceEquals(held, drop), "the drop's own ItemData is not in the chest");
        }

        CheckStacks(chest);
    }

    private static void CheckStacks(Inventory chest)
    {
        foreach (ItemDrop.ItemData held in chest.GetAllItems())
        {
            Expect(held.m_stack <= held.m_shared.m_maxStackSize,
                $"{held.m_shared.m_name} slot {held.m_gridPos.x},{held.m_gridPos.y} holds {held.m_stack}/{held.m_shared.m_maxStackSize}");
        }

        Expect(chest.NrOfItems() <= chest.GetWidth() * chest.GetHeight(), "no more items than slots");
    }

    private static ItemDrop.ItemData.SharedData Shared(string name, int maxStack,
        ItemDrop.ItemData.ItemType type = ItemDrop.ItemData.ItemType.Material)
    {
        return new ItemDrop.ItemData.SharedData
        {
            m_name = name,
            m_maxStackSize = maxStack,
            m_itemType = type,
            m_weight = 1f,
        };
    }

    private static ItemDrop.ItemData Item(ItemDrop.ItemData.SharedData shared, int stack, int quality = 1, int worldLevel = 0)
    {
        return new ItemDrop.ItemData
        {
            m_shared = shared,
            m_stack = stack,
            m_quality = quality,
            m_worldLevel = worldLevel,
        };
    }

    private static void Scenario(string name, Action body)
    {
        int failuresBefore = _failures;
        try
        {
            body();
        }
        catch (Exception ex)
        {
            _failures++;
            System.Console.WriteLine($"  threw {ex.GetType().Name}: {ex.Message}");
        }

        System.Console.WriteLine($"{(_failures == failuresBefore ? "PASS" : "FAIL")}  {name}");
    }

    private static void Expect(bool condition, string what)
    {
        if (condition) return;
        _failures++;
        System.Console.WriteLine($"  expected: {what}");
    }

    private sealed class CountingLogHandler : ILogHandler
    {
        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
        {
            if (logType == LogType.Warning) _warnings++;
        }

        public void LogException(Exception exception, UnityEngine.Object context)
        {
        }
    }
}
