# Enemy pathfinding: from per-direction BFS to a shared distance field

## Before

Each time an enemy moved, `MapManager.GetBestMove` ran up to four breadth-first
searches, one from each walkable neighbouring tile to the player, and stepped
toward the shortest. Every search allocated its own `bool[,]` visited grid, a
`Queue`, and a `Tuple` plus a node object per visited tile. The method also made
two `Debug.Log` calls per move.

## After

All enemies chase the same target, so the distance to the player is a property
of the grid, not of any one enemy. One BFS outward from the player labels every
reachable tile with its step count. An enemy reads its four neighbours' labels and
takes the lowest, with ties broken in the original order (up, right, down, left).
The distance and queue buffers are allocated once and reused.

The catch is that the grid changes mid-turn. Enemies act one after another, and
each one releases the tile it leaves and claims the one it enters, so an enemy
later in the turn has to see where the earlier ones went. Building the field once
per turn would silently change behaviour. Instead, every walkability write bumps
a version counter (`SetTileWalkable` for enemies, `SetCoordsAvailability` for the
player's own moves), and the field is rebuilt only when the version, the player's
tile or the current room has changed since it was built.

## Verifying behaviour is unchanged

The original `MapManager` is kept, unmodified apart from its class names and a
switch around the two log calls, as a test oracle in
[`Assets/Tests/Legacy`](../Assets/Tests/Legacy/LegacyMapManager.cs).
[`PathfindingEquivalenceTests`](../Assets/Tests/EditMode/PathfindingEquivalenceTests.cs)
drives both implementations side by side and compares the full result of every
call: real direction, room-relative direction and whether the enemy attacks.

| Test | Enemy decisions compared | Mismatches |
|---|---|---|
| Random grids (2x2 to 12x12, 0-45% walls, all four room orientations) | 10,244, including 2,468 tie-breaks | 0 |
| Full battles in the game's room prefabs, 2 enemies (as in game) | 61,497 | 0 |
| Full battles in the game's room prefabs, 2-6 enemies | 159,574 | 0 |
| Full battles on random 6-15 x 6-15 grids, 2-8 enemies | 48,134 | 0 |

The full-battle tests replay `TurnManager`'s enemy loop: move-by-move grid writes,
player moves through `CheckPlayerMove`, and enemies killed and their tiles freed.
6,031 of those decisions came out differently from what the enemy would have chosen
at the start of its turn, because another enemy had moved first. So the
blocked-mid-turn case is well covered.

Two further checks:

- **Control.** The same battles, with enemy moves written straight into the grid
  instead of through `SetTileWalkable`, produce 1,745 mismatches. A stale field
  does change behaviour, and these tests catch it.
- **End to end.** Two player builds, one with the original code and one with the
  rewrite, played the same seeded runs (3 seeds, 3 battles each, scripted inputs)
  and produced identical turn-by-turn traces of player and enemy positions and HP.
  A repeated run with the same seed confirmed the runs are deterministic.

## Measurements

[`PathfindingBenchmark`](../Assets/Tests/EditMode/PathfindingBenchmark.cs) plays one
enemy turn from each of a few hundred random positions, exactly as `TurnManager`
does, for each implementation. It measures in two independent ways:

- **Time per move:** Stopwatch, median of 15 rounds. This covers `GetBestMove` plus
  the tile writes around it.
- **GC allocation per move:** the Unity Profiler's GC Alloc column for a
  `ProfilerMarker` around `GetBestMove`, read back from the captured frames. The
  Profiler's own timings are higher than the Stopwatch ones, because recording
  every allocation adds overhead to the allocation-heavy original, so the time
  column uses Stopwatch.

Unity 2020.2.5f1 editor, Release code optimization, Intel Core i9-12900K, Windows 11:

| Scenario | Original | Distance field | Speed-up | GC per move: original | GC per move: distance field |
|---|---|---|---|---|---|
| Room I (5x7), 2 enemies **[in game]** | 6.98 µs | 0.91 µs | 7.6x | 3,136 B | 29 B |
| Room II (4x9), 2 enemies **[in game]** | 5.61 µs | 0.73 µs | 7.7x | 2,763 B | 29 B |
| 10x10, 15% walls, 4 enemies [synthetic] | 19.44 µs | 2.06 µs | 9.4x | 9,045 B | 29 B |
| 20x20, 15% walls, 8 enemies [synthetic] | 75.39 µs | 7.22 µs | 10.4x | 34,235 B | 31 B |
| 40x40, 15% walls, 16 enemies [synthetic] | 312.08 µs | 27.12 µs | 11.5x | 137,666 B | 41 B |

How to read this:

- **In this game, the absolute saving is small.** The battle rooms are 5x7 and 4x9
  tiles and every battle has two mummies, so the pathfinding goes from about 7 µs
  to under 1 µs per enemy move. That doesn't change the frame rate; enemy turns are
  paced by animations. The gains that do matter here are removing per-move
  garbage (about 3 KB down to about 30 B) and how the approach scales: the gap
  widens with grid size and enemy count, as the synthetic rows show.
- **The remaining 29 B is the `Tuple` that `GetBestMove` returns.** The slightly
  higher figures on larger grids are the one-time allocation of the distance
  buffers, spread across the calls.
- **The original's logging cost more than its search.** The shipped version's two
  `Debug.Log` calls per move took about 300 µs per move in the editor, which is
  far more than the search itself. That cost is excluded from the "Original"
  column, so the table compares the algorithms only.

To reproduce: open the Test Runner (Window > General > Test Runner), go to
EditMode, select `PathfindingBenchmark.EnemyTurnCost` (it's `[Explicit]`, so it
doesn't run with the other tests) and run it. The results table is printed to the
Console. From the command line:

```
Unity -batchmode -nographics -releaseCodeOptimization -projectPath <path> -runTests -testPlatform EditMode -testFilter PathfindingBenchmark -testResults results.xml
```
