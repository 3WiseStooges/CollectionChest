# Development

## Build

```bash
dotnet build CollectionChest/CollectionChest.csproj -c Release
```

Output goes to `package/CollectionChest.dll`, which is committed. The publish workflow ships that
file rather than building on CI.

The project finds Valheim at a standard Windows Steam layout. Override it:

```bash
dotnet build CollectionChest/CollectionChest.csproj -c Release -p:ValheimFolder="D:\Steam\steamapps\common\Valheim"
```

BepInEx and HarmonyX come from NuGet (`BepInEx.Core`, `HarmonyX`), not from an r2modman
profile. `BepInEx.Core` is only on BepInEx's own feed, which is what `NuGet.config` is for.

## Where the game code lives

Valheim's own classes are in **`valheim_Data/Managed/assembly_valheim.dll`**, not
`Assembly-CSharp.dll`. `GetStableHashCode` is in `assembly_utils.dll`, and `Localize` and
`ButtonSfx` are in `assembly_guiutils.dll`.

```bash
ilspycmd -t Container "X:/SteamLibrary/steamapps/common/Valheim/valheim_Data/Managed/assembly_valheim.dll"
```

## What the mod touches

| Target | Access | Used for |
| --- | --- | --- |
| `Container.Awake()` | private, postfix | Attach a `Collector` to each eligible chest |
| `Container.GetHoverText()` | public, postfix | The "Auto-collect on" line |
| `InventoryGui.UpdateContainer(Player)` | private, postfix | Show, hide and relabel the switch every frame |
| `Container.Load()` | private, delegate | Refresh a chest from its ZDO before writing to it |
| `InventoryGui.m_currentContainer` | private field | Which chest the open menu is showing |

The three private methods would compile fine if renamed and then silently do nothing.
`Plugin.Awake` logs a `Harmony skip` line per patch class that fails, and `GameAccess` logs when
a reflection target is missing. If `Container.Load` cannot be found, chests **stop collecting**
rather than write to an inventory that might be stale.

## Ownership is the whole design

Everything that could duplicate or lose an item comes down to ZDO ownership:

- **A chest's inventory may only be written by its ZDO owner.** `Container.OnContainerChanged`
  saves only when `IsOwner()`, and another peer's writes would be overwritten. So a `Collector`
  only scans when its chest's `ZNetView.IsOwner()`.
- **The owner's copy can be up to a second stale.** Vanilla reloads in `CheckForChanges` on a
  1-second `InvokeRepeating`, and ownership moves when a player opens a chest or walks away. So
  every scan calls the private `Container.Load()` first. It is a no-op when the ZDO revision has
  not moved, and while the chest is open on this client, when this client is the only writer.
- **A drop can only be removed by its owner.** `ZNetScene.Destroy` on a non-owned ZDO destroys
  the local GameObject and leaves the ZDO, so the item comes back. Collection therefore goes
  through `ItemDrop.CanPickup()`, which is owner-and-older-than-0.5s, and otherwise calls
  `RequestOwn()`, exactly as `Player.AutoPickup` does. A drop the chest has no room for is never
  requested, so a full chest does not pull ownership of items back and forth.

Smelters and the chests beside them are normally owned by the same peer, because
`ZDOMan.ReleaseNearbyZDOS` hands a whole area to whoever arrived first, and a drop is owned by
the peer that spawned it. The `RequestOwn` path is for the exceptions.

## Filling the chest without overfilling it

`Inventory.AddItem(ItemData)` has three behaviours the mod works around (see `Intake.Store`):

1. It keeps the object it was handed when it fills a new slot, so every call gets a fresh
   `Clone()`.
2. After topping up existing stacks it puts the entire remainder into one new slot, even past
   the max stack size. Items go in at most one stack per call.
3. When it runs out of slots it returns false but keeps the stacks it already topped up, and
   leaves the unplaced part in the passed item's `m_stack`.

`RoomFor` mirrors `FindFreeStackItem` (name, quality and world level) rather than calling
`CanAddItem`, which ignores quality.

`tools/IntakeCheck` runs `RoomFor`, `AddUpTo` and `CountOf` against the game's real `Inventory`
class, outside Unity, including the refusal path in point 3 and the quality case where
`CanAddItem` over-promises. It compiles the shipped `Intake.cs` in, and needs the game installed,
so run it locally after touching any of this:

```bash
dotnet run --project tools/IntakeCheck -c Release
```

It swaps `Debug.unityLogger`'s handler for a managed one, because `Inventory` logs through
`ZLog` and Unity's default handler is native. Valheim also has a global `Console` class, hence
`System.Console` throughout. The drop is settled from a before/after count of matching
units in the chest. If anything in between misbehaves, whether another mod's `AddItem` patch or
an exception from an `m_onChanged` subscriber, the floor loses exactly what the chest gained.
A partial take writes the reduced stack with `ItemDrop.SaveToZDO` directly, because
`ItemDrop.SetStack` clamps to the max stack size and would delete the excess of an oversized
drop.

## MultiUserChest

Common on co-op servers, including ours, so its source was read closely (0.6.2):

- Its `RPC_RequestOpen` prefix lets a second player open a chest that is in use **without**
  taking ownership. So the player clicking the switch may not own the chest, and the switch
  sends `CollectionChest_SetCollecting` to the owner with `ZNetView.InvokeRPC(method, ...)`, which
  routes there. The label always shows the ZDO's value, never the requested one.
- Its `Inventory.AddItem` prefixes only intercept when the item or the inventory belongs to an
  inventory this peer does not own. Our clones belong to no inventory and the chest is ours, so
  they fall through to vanilla.
- It transpiles `InventoryGui.UpdateContainer`; a postfix on the same method is unaffected.

## The chest panel layout

Read from the game's own scene (`StreamingAssets/SoftRef/Bundles/17245031`, `main.unity`) with
UnityPy, not guessed. At the 1920x1080 reference size:

- `Container` is 570x340, anchored below the player inventory panel.
- Its 46px header holds `TakeAll` (133x40, top-left, gamepad `JoyLStick`), `container_name`
  (centre) and `StackAll` (133x40, top-right, `JoyRStick`). Everything below is `ContainerGrid`.
- `Weight` hangs outside the panel's bottom-right corner.

So the switch is a clone of `TakeAll` anchored to the panel's bottom-left, 8px below it. The
clone loses its `UIGamePad` and gamepad hint, which would otherwise fire on the same stick
click as Take all, and gets a fresh `onClick`. `ButtonSfx` re-adds its click sound in
`OnEnable`, which is why the clone is deactivated before its `onClick` is replaced.

## Testing

Install through r2modman so you exercise the real load order rather than hand-copying into a
profile. Worth checking after any change:

- A chest by a smelter, switched on: bars go in half a second or so after they land
- Fill the chest: the hover text says full, and the next bars stay on the floor
- Leave one slot and one partial stack: it tops up the stack, fills the slot, and leaves the rest
- A second chest out of range (over 1m from the first chest's sides): it takes nothing
- Log out and in: still on, with the same hover line
- Carts, ships, gravestones and the obliterator: no button
- Two players, with MultiUserChest: the second player opening the chest can flip the switch
- Two players, one without the mod owning the chest: that chest pauses, and no items duplicate

Set `Advanced / VerboseLogging = true` to log each collection, each change of the switch, and
when a chest runs out of room.

## Publish to Thunderstore

Pushes to `main` publish automatically. The workflow bumps the patch version if Thunderstore
already has the repo version.

Also available as **Actions → Publish to Thunderstore → Run workflow**, or tag `v1.x.y` and
push.

1. `thunderstore.toml` `namespace` must match the Thunderstore team (`LJIndustries`)
2. Repo secret: `THUNDERSTORE_API_KEY`
3. Commit the rebuilt `package/CollectionChest.dll`
