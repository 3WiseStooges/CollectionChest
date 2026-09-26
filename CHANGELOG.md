# Changelog

## 1.0.0

First release.

- **An auto-collect switch in every built chest's menu.** A copy of the menu's own Take all
  button, hung below the chest panel's bottom-left corner. The panel's header already holds
  Take all, the chest name and Place stacks, and everything below the header is the item grid.
  The switch is a flag in the chest's ZDO, so it is saved with the world and every player sees
  the same setting.
- **A switched-on chest pulls in items dropped within 1 metre of it**, checked once a second.
  The distance is to the chest's own colliders, not its pivot, so an item on the lid is at 0 and
  a long chest reaches no further past its ends than a short one.
- **It never takes more than it has room for.** Room is counted the way `Inventory.AddItem`
  counts it: matching stacks by name, quality and world level first, then empty slots.
  `CanAddItem` ignores quality and can promise room `AddItem` will not give. Items go in at most
  one stack per call, since `AddItem` puts its whole remainder into a single new slot. Whatever
  does not fit stays on the ground as a smaller stack, and a full chest leaves drops alone
  instead of repeatedly claiming them.
- **An item cannot end up in the chest and on the ground at once.** The drop is shrunk by what
  the chest actually gained, counted afterwards, not by what the code expected to move. The
  chest only takes a drop once this client owns it, since `ZNetScene.Destroy` on anyone else's
  copy leaves the ZDO behind and the item would reappear.
- **Only the chest's owner collects.** That is the one peer allowed to write its inventory. It
  reloads the chest from its ZDO first, in case another player changed it within the last
  second. Built with MultiUserChest in mind: the menu switch asks the owner by RPC rather than
  writing the flag itself.
- Leaves alone: carts, ship storage, gravestones, loot bags, the obliterator, food set out on a
  table, fish still in the water, items in tar, quest items, and anything whose prefab opts
  out of auto-pickup.
