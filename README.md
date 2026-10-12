# The Descent

A turn-based dungeon crawler made in Unity as a university project. You explore a
procedurally generated Egyptian tomb; walking into certain rooms seals the doors
and raises mummies, and the fight plays out turn by turn on the room's tile grid.
I've since come back to it to rework the enemy pathfinding (details below).

![Gameplay](docs/images/gameplay.gif)

| | |
|---|---|
| ![A sealed battle room](docs/images/battle.jpg) | ![Enemy turn](docs/images/enemy-turn.jpg) |

## What's in it

**Runtime level generation.** Each run assembles the tomb from room and corridor
prefabs ("sections"). Every section declares its exits, a bounding collider and
tags saying which kinds of section may attach to it. The generator grows the level
outward from the spawn room, rejects candidates that would overlap existing ones,
and caps unused exits with dead ends. Generation is seeded, so a layout can be
reproduced. The section/exit/tag core (`Scripts/LevelGenerator`, `Structure`,
`Helpers`, `Exceptions`) is adapted from the free *Procedural Level Generator*
package on the Unity Asset Store. On top of it I added a walkable tile grid per
room, enemy spawn chances, transfer tiles that link one room's exit to the next
room's entrance, and per-room orientation, so that screen-relative input maps
onto each room's grid (`MapManager`).

**Turn-based combat.** Entering an uncleared room locks it and spawns mummies
(`TurnManager`). Player and enemies then alternate turns: the player moves, shoots
along a row or column at the first mummy in line, or drinks a potion. Each mummy
in turn walks toward the player, or attacks if it's adjacent, while the camera
follows whoever is acting. Spawning and dying play a dissolve effect, done by
tweening a dissolve shader's parameter with DOTween.

**Enemy pathfinding.** This is what I reworked after the course. Originally every
enemy move ran four breadth-first searches, one per direction, each allocating
its own visited grid, queue and per-tile objects. It now runs one BFS outward from
the player and shares the resulting distance field: each enemy just steps to its
lowest-labelled neighbour, with the same tie-break order as before.

The subtle part is that enemies move one at a time, and every move changes which
tiles are free. So the field is versioned: every walkability write bumps a
counter, and the field is rebuilt only when the grid, the player's tile or the
room has changed.

| Per enemy move | Original | Distance field |
|---|---|---|
| Time, in-game rooms (5x7 and 4x9, 2 mummies) | 5.6-7.0 µs | 0.7-0.9 µs (**7.6-7.7x faster**) |
| Time, 40x40 synthetic grid, 16 enemies | 312 µs | 27 µs (11.5x) |
| GC allocation, in-game rooms (Unity Profiler) | ~3 KB | 29 B (just the returned tuple) |

These are microseconds. In this game they don't move the frame rate, since enemy
turns are paced by animations. The more meaningful gains are the removed per-move
garbage and the way the approach scales.

The rewrite is checked against the original, which is kept verbatim as a test
oracle. 279,449 enemy decisions were identical across random grids and simulated
battles in the game's own rooms, including tie-breaks and enemies blocked mid-turn
by another enemy that had already moved. A control test shows that a stale field
would diverge, so the tests can tell the difference. Seeded play-throughs of an
original-code build and a rewrite build also produced identical turn-by-turn
traces. Method and full results: [docs/pathfinding.md](docs/pathfinding.md).

## Third-party assets (not included)

The art, audio and some plugins come from the Unity Asset Store and other sources
whose licences don't allow redistribution, so they're excluded from this
repository. Without them the code still compiles (once DOTween is imported), but
most models, effects and UI will be missing. To see the game as intended, import
the following into the folders listed:

| Folder under `Assets/` | What it provides |
|---|---|
| `Plugins/Demigiant/DOTween` | **DOTween** (free; Asset Store or dotween.demigiant.com). Required to compile. |
| `TextMesh Pro` | TMP Essential Resources (Window > TextMeshPro > Import TMP Essential Resources) |
| `Egypt_UI` | Menu and HUD art |
| `MedievalTombEgypt` | Tomb walls, floors and props |
| `PortalsPackage` | The glowing gates that seal a room during a battle |
| `Dissolve`, `DissolveEmissionShader` | The mummies' spawn and death dissolve |
| `Sand Effects pack` | Sandstorm particles |
| `Cartoon Fire Particles` | Torches and braziers |
| `Low Poly Guns`, `Low-Poly Weapons` | The player's pistol |
| `AS_Casual_Island_Game_Sounds_FREE`, `Attack Jump & Hit Damage Human Sounds`, `Classic Footstep SFX`, `Weapons of Choice FREE - Komposite Sound` | Sound effects |

Also not included: the Mixamo character animations (`Characters/**/*.fbx` and the
clips extracted from them), the mummy model (`Mummy/`) and its textures, and the
music (`Audio/`). The prefabs, animator controllers and materials that use them
are included.

## Opening the project

1. Install **Unity 2020.2.5f1**, the version the project was made with. Newer
   versions will offer to upgrade it.
2. Clone the repository and add the folder in Unity Hub.
3. Import DOTween, then whichever of the assets above you have.
4. Open `Assets/Scenes/MenuScene.unity` and press Play.

Controls: the arrow keys or the on-screen pad move you one tile. With the attack
toggle on, a direction shoots along that row or column. The potion heals, and
both it and the skip button use up your turn.

## Tests

Window > General > Test Runner > EditMode runs the pathfinding equivalence tests
(about 15 seconds). The benchmark behind the numbers above is
`PathfindingBenchmark.EnemyTurnCost`. It's marked `[Explicit]`, so select and run
it on its own; it prints its results table to the Console.

## Layout

```
Assets/
  Scripts/          game code (LevelGenerator/, Structure/, Helpers/: the section generator)
  Tests/EditMode/   pathfinding equivalence tests and benchmark
  Tests/Legacy/     the original pathfinding, kept as the tests' reference
  Scenes/           MenuScene, MainScene (SampleScene is an earlier BSP dungeon experiment)
  Rooms/            room and corridor section prefabs
  Characters/       player and mummy prefabs and animator controllers
docs/               pathfinding write-up, images
```
