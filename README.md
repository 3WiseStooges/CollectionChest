# CollectionChest

A switch inside the chest's menu that allows you to enable auto collection! No more wandering
around smelters, furnaces, windmills and other appliances manually collecting random stuff.

Every chest you build gets the switch. Turn it on, and the chest pulls in any item dropped right
beside it. Put a chest where a smelter spits out its bars and the bars store themselves.

## How it works

Open a chest. Just below the chest's panel, on the left, there is a new button:
**Auto-collect: OFF**. Click it to turn it **ON**.

From then on the chest checks once a second for dropped items within **1 metre** of it, and
moves them in. That covers anything that ends up on the ground: bars from a smelter or blast
furnace, coal from a charcoal kiln, flour from a windmill, thread from a spinning wheel, eitr
from a refinery, honey from a beehive, and things you drop yourself.

The switch is saved with the chest, in the world. It stays on through restarts, every player
sees the same setting, and hovering over a collecting chest says so:

- **Auto-collect on**: collecting.
- **Auto-collect on - full**: no empty slots left. It still tops up stacks it already has,
  but anything else stays on the ground.

## It will not overfill a chest

A chest only ever takes what it has room for. It fills stacks of the same item first, then
empty slots, and stops there:

- If only part of a stack fits, it takes that part and **leaves the rest on the ground**,
  still one pickup, for you or another chest.
- A full chest leaves items where they are and does not keep grabbing at them.
- The drop on the floor is reduced by exactly what the chest gained, counted after the move
  rather than assumed, so an item cannot end up both in the chest and on the ground.

## Range, and tightly packed bases

The 1 metre is measured from the chest's **sides and lid**, not its centre. An item resting on
the chest is at 0, and a long chest reaches no further past its ends than a small one does.

It is tight on purpose. A chest under one smelter should catch that smelter's bars, not the
next one along the wall. If your layout needs more reach, raise `Range` a little at a time;
it goes up to 4 metres.

## What it leaves alone

- Storage that is not a chest you built: carts, ship storage, gravestones and loot bags get no
  switch.
- The **obliterator**. It has an inventory too, but a self-filling obliterator is one lever pull
  from destroying whatever wandered into it.
- Food set out on a table, fish still in the water, items stuck in tar, quest items, and
  anything the game only lets you pick up by hand (the same items your own auto-pickup
  ignores).
- Items that have only just dropped: like your own auto-pickup, it waits half a second.

## Configuration

`BepInEx/config/com.ljindustries.valheim.collectionchest.cfg`, written on first run.

| Setting | Default | What it does |
| --- | --- | --- |
| `Enabled` | `true` | Master switch. While off, no chest collects and the button is hidden. Chests you switched on remember it and resume when you turn this back on. |
| `Range` | `1` | How far from the chest's sides and lid an item can be and still get pulled in, in metres. 0.25–4. |
| `ScanInterval` | `1` | Seconds between checks. Machines produce every 30 seconds or more, so there is little to gain below 1. 0.25–10. |
| `ShowHoverStatus` | `true` | The "Auto-collect on" line on a collecting chest's hover text. |
| `VerboseLogging` | `false` | Log every item collected, and when a chest runs out of room, to the BepInEx console. |

BepInEx only reads the file at startup, so edit it with the game closed or use a
configuration manager in game.

## Multiplayer

Install it on **every player's game**. The server does not need it.

Valheim gives each chest to one player's game to run, normally whoever got near it first, and
only that game is allowed to change what is in the chest. That game is the one that does the
collecting, which is what keeps two players from each storing the same bar. If the player
running a chest does not have the mod, that chest simply pauses until someone who does takes
it over. It does not break, and nothing is lost.

Works alongside **MultiUserChest**. With it, a second player can have a chest open at the same
time, and flipping the switch from their menu asks the player running the chest to do it.

## Install

Through a mod manager (r2modman, Thunderstore Mod Manager), or by hand: drop
`CollectionChest.dll` into `BepInEx/plugins/`.

Requires [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/).
