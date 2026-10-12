using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LvlGen.Scripts;
using NUnit.Framework;
using UnityEngine;
using static PathfindingFixtures;

// Checks the distance-field pathfinding against the original four-probe BFS
// (LegacyMapManager): same move, same relative direction, same attack decision,
// for every enemy, including ties and enemies blocked by others mid-turn.
public class PathfindingEquivalenceTests
{
    [SetUp] public void SilenceLegacyLogging() => LegacyMapManager.LogMoves = false;
    [TearDown] public void RestoreLegacyLogging() => LegacyMapManager.LogMoves = true;

    // # wall   . floor   P player   A, B enemies (A moves first)
    //
    //   # # # # #
    //   B . A . P
    //   . . . . #
    //
    // A steps right into the only tile next to the player. B now has no route and
    // must wait. A field built before A moved would still show the old route and
    // send B right, so this only passes if A's move invalidated the field.
    [Test]
    public void EnemyBlockedMidTurn_SeesTheTileTheFirstEnemyTook()
    {
        var grid = new[]
        {
            new[] { false, false, false, false, false },
            new[] { false, true, false, true, false },
            new[] { true, true, true, true, false },
        };
        var player = new Cell(1, 4);
        var a = new Cell(1, 2);
        var b = new Cell(1, 0);

        using (var pair = CreatePair(grid, orientation: 0))
        {
            pair.SetPlayer(player);

            var aMove = pair.Current.GetBestMove(a.ToTuple(), player.ToTuple());
            Assert.AreEqual(Describe(pair.Legacy.GetBestMove(a.ToTuple(), player.ToTuple())), Describe(aMove));
            Assert.AreEqual(Direction.Right, aMove.Item1);

            var aTo = Step(a, aMove.Item1);
            pair.LegacyRoom.Tiles[a.Row].array[a.Col] = true;
            pair.LegacyRoom.Tiles[aTo.Row].array[aTo.Col] = false;
            pair.Current.SetTileWalkable(a.Row, a.Col, true);
            pair.Current.SetTileWalkable(aTo.Row, aTo.Col, false);

            var bMove = pair.Current.GetBestMove(b.ToTuple(), player.ToTuple());
            Assert.AreEqual(Describe(pair.Legacy.GetBestMove(b.ToTuple(), player.ToTuple())), Describe(bMove));
            Assert.AreEqual(Direction.None, bMove.Item1);
        }
    }

    // Two rooms of the same size with the player on the same tile and no walkability
    // writes in between, so only the room reference tells the cache it is stale.
    [Test]
    public void ChangingRoom_RebuildsTheDistanceField()
    {
        bool[][] Room(params string[] rows) => rows.Select(r => r.Select(ch => ch == '.').ToArray()).ToArray();
        var open = Room("....." , "x...x", ".....");
        var walled = Room(".#...", "x#..x", ".....");
        var player = new Cell(1, 4);
        var enemy = new Cell(1, 0);

        using (var first = CreatePair(open, orientation: 0))
        using (var second = CreatePair(walled, orientation: 0))
        {
            first.SetPlayer(player);
            second.SetPlayer(player);

            Assert.AreEqual(Direction.Right, first.Current.GetBestMove(enemy.ToTuple(), player.ToTuple()).Item1);

            first.Current.CurrPlayerSection = second.CurrentRoom;
            var afterSwitch = first.Current.GetBestMove(enemy.ToTuple(), player.ToTuple());
            Assert.AreEqual(Describe(second.Legacy.GetBestMove(enemy.ToTuple(), player.ToTuple())), Describe(afterSwitch));
            Assert.AreEqual(Direction.Down, afterSwitch.Item1);
        }
    }

    [Test]
    public void EveryEnemyGetsTheSameMove_OnRandomGrids()
    {
        var rng = new System.Random(20261012);
        var pathLength = typeof(LegacyMapManager).GetMethod("GetPathLength", BindingFlags.Instance | BindingFlags.NonPublic);
        int decisions = 0, attacks = 0, stays = 0, ties = 0;

        for (int trial = 0; trial < 3000; trial++)
        {
            var grid = RandomGrid(rng, rng.Next(2, 13), rng.Next(2, 13), new[] { 0.0, 0.15, 0.3, 0.45 }[trial % 4]);
            if (WalkableTiles(grid).Count < 2) continue;
            var (player, enemies) = PlaceUnits(grid, rng.Next(1, 7), rng);

            using (var pair = CreatePair(grid, rng.Next(4)))
            {
                pair.SetPlayer(player);
                foreach (var e in enemies)
                {
                    var expected = pair.Legacy.GetBestMove(e.ToTuple(), player.ToTuple());
                    var actual = pair.Current.GetBestMove(e.ToTuple(), player.ToTuple());
                    Assert.AreEqual(Describe(expected), Describe(actual),
                        $"trial {trial}, enemy {e}:\n{Render(pair.LegacyRoom, player, enemies)}");

                    decisions++;
                    if (expected.Item3) attacks++;
                    else if (expected.Item1 == Direction.None) stays++;
                    else if (HasTiedBestDirections(pair.Legacy, pathLength, e, player)) ties++;
                }
            }
        }

        Debug.Log($"[Pathfinding] random grids: {decisions} decisions identical " +
                  $"({decisions - attacks - stays} moves, {attacks} attacks, {stays} no-move; {ties} moves broke a tie)");
        Assert.Greater(ties, 0, "tie-breaking was never exercised");
    }

    [Test]
    public void FullBattles_OnTheGamesRooms_MatchTheOriginal()
    {
        var rooms = GameRooms().Select(r => r.Grid).ToList();
        Assert.That(rooms.Count, Is.GreaterThanOrEqualTo(5), "room prefabs not found under Assets/Rooms");

        // InitBattle spawns Random.Range(2, maxEnemiesAllowed) enemies, which is 2 for
        // every battle room today; the second run crowds the rooms to force blocking.
        var asInGame = PlayBattles(rooms, battlesPerRoom: 400, enemyCount: _ => 2, seed: 1);
        var crowded = PlayBattles(rooms, battlesPerRoom: 400, enemyCount: rng => rng.Next(2, 7), seed: 2);
        Report("game rooms, 2 enemies", asInGame);
        Report("game rooms, 2-6 enemies", crowded);

        Assert.AreEqual(0, asInGame.Mismatches, asInGame.FirstMismatch);
        Assert.AreEqual(0, crowded.Mismatches, crowded.FirstMismatch);
        Assert.Greater(crowded.ChangedByEarlierMove, 0, "no enemy was ever affected by another's move mid-turn");
    }

    [Test]
    public void FullBattles_OnLargerGrids_MatchTheOriginal()
    {
        var rng = new System.Random(3);
        var grids = Enumerable.Range(0, 60)
            .Select(_ => RandomGrid(rng, rng.Next(6, 16), rng.Next(6, 16), 0.1 + 0.3 * rng.NextDouble()))
            .ToList();

        var stats = PlayBattles(grids, battlesPerRoom: 10, enemyCount: r => r.Next(2, 9), seed: 4);
        Report("random 6-15 x 6-15 grids, 2-8 enemies", stats);

        Assert.AreEqual(0, stats.Mismatches, stats.FirstMismatch);
        Assert.Greater(stats.ChangedByEarlierMove, 0);
    }

    // Control for the two tests above: the same battles, but enemy moves are written
    // straight into the grid instead of through SetTileWalkable, so the cached field
    // is only refreshed when the player moves. If this did not diverge, matching the
    // original would say nothing about whether invalidation works.
    [Test]
    public void BypassingSetTileWalkable_WouldChangeBehaviour()
    {
        var rooms = GameRooms().Select(r => r.Grid).ToList();
        var stats = PlayBattles(rooms, battlesPerRoom: 400, enemyCount: rng => rng.Next(2, 7), seed: 2, bypassInvalidation: true);
        Report("control: invalidation bypassed", stats);
        Assert.Greater(stats.Mismatches, 0);
    }

    // ---------------------------------------------------------------------

    struct BattleStats
    {
        public int Battles, Decisions, Mismatches, ChangedByEarlierMove, PlayerMoves, Kills;
        public string FirstMismatch;
    }

    static void Report(string label, BattleStats s) =>
        Debug.Log($"[Pathfinding] {label}: {s.Battles} battles, {s.Decisions} enemy decisions, {s.Mismatches} mismatches; " +
                  $"{s.ChangedByEarlierMove} decisions changed by an enemy that moved earlier in the same turn; " +
                  $"{s.PlayerMoves} player moves, {s.Kills} kills");

    // Plays battles the way TurnManager does. Each enemy in turn asks for its best
    // move, then releases the tile it leaves and claims the one it enters, so enemies
    // later in the same turn path around the ones that already moved. The player moves
    // through CheckPlayerMove (SetCoordsAvailability, not SetTileWalkable) and sometimes
    // kills an enemy, freeing its tile as PlayerAttack does.
    static BattleStats PlayBattles(List<bool[][]> rooms, int battlesPerRoom, Func<System.Random, int> enemyCount, int seed, bool bypassInvalidation = false)
    {
        var rng = new System.Random(seed);
        var stats = new BattleStats();

        foreach (var room in rooms)
        {
            if (WalkableTiles(room).Count < 3) continue;

            for (int battle = 0; battle < battlesPerRoom; battle++)
            {
                var grid = Copy(room);
                var (player, enemies) = PlaceUnits(grid, enemyCount(rng), rng);
                int orientation = rng.Next(4);

                using (var pair = CreatePair(grid, orientation))
                using (var turnStart = CreatePair(grid, orientation))
                {
                    pair.SetPlayer(player);
                    stats.Battles++;

                    void SetWalkable(Cell t, bool walkable)
                    {
                        pair.LegacyRoom.Tiles[t.Row].array[t.Col] = walkable;
                        if (bypassInvalidation) pair.CurrentRoom.Tiles[t.Row].array[t.Col] = walkable;
                        else pair.Current.SetTileWalkable(t.Row, t.Col, walkable);
                    }

                    bool diverged = false;
                    for (int turn = 0; turn < 20 && enemies.Count > 0 && !diverged; turn++)
                    {
                        double roll = rng.NextDouble();
                        if (roll < 0.5)
                        {
                            var dir = InputDirections[rng.Next(4)];
                            bool moved = pair.Legacy.CheckPlayerMove(dir);
                            Assert.AreEqual(moved, pair.Current.CheckPlayerMove(dir), "player move");
                            if (moved) stats.PlayerMoves++;
                        }
                        else if (roll < 0.6)
                        {
                            int k = rng.Next(enemies.Count);
                            SetWalkable(enemies[k], true);
                            enemies.RemoveAt(k);
                            stats.Kills++;
                        }

                        // Snapshot the grid as the enemy turn starts, to count decisions
                        // that only differ because an earlier enemy moved this turn.
                        for (int r = 0; r < grid.Length; r++)
                            Array.Copy(pair.LegacyRoom.Tiles[r].array, turnStart.LegacyRoom.Tiles[r].array, grid[r].Length);
                        turnStart.Legacy.PlayerCoords = pair.Legacy.PlayerCoords;

                        for (int i = 0; i < enemies.Count; i++)
                        {
                            var src = enemies[i].ToTuple();
                            var expected = pair.Legacy.GetBestMove(src, pair.Legacy.PlayerCoords);
                            var actual = pair.Current.GetBestMove(src, pair.Current.PlayerCoords);
                            stats.Decisions++;

                            if (Describe(expected) != Describe(turnStart.Legacy.GetBestMove(src, turnStart.Legacy.PlayerCoords)))
                                stats.ChangedByEarlierMove++;

                            if (Describe(expected) != Describe(actual))
                            {
                                stats.Mismatches++;
                                stats.FirstMismatch = stats.FirstMismatch ??
                                    $"turn {turn}, enemy {enemies[i]}: original {Describe(expected)}, rewrite {Describe(actual)}\n" +
                                    Render(pair.LegacyRoom, Cell.From(pair.Legacy.PlayerCoords), enemies);
                                diverged = true;
                                break;
                            }

                            if (!expected.Item3 && expected.Item2 != Direction.None)
                            {
                                var to = Step(enemies[i], expected.Item1);
                                SetWalkable(enemies[i], true);
                                SetWalkable(to, false);
                                enemies[i] = to;
                            }
                        }

                        if (!diverged) Assert.IsTrue(pair.GridsMatch(), $"grids differ after turn {turn}");
                    }
                }
            }
        }
        return stats;
    }

    static bool HasTiedBestDirections(LegacyMapManager legacy, MethodInfo pathLength, Cell enemy, Cell player)
    {
        var room = legacy.CurrPlayerSection;
        var lengths = new List<int>();
        foreach (var dir in new[] { Direction.Up, Direction.Right, Direction.Down, Direction.Left })
        {
            var n = Step(enemy, dir);
            if (n.Row < 0 || n.Row >= room.Tiles.Length || n.Col < 0 || n.Col >= room.Tiles[n.Row].array.Length) continue;
            if (!room.Tiles[n.Row].array[n.Col]) continue;
            lengths.Add((int)pathLength.Invoke(legacy, new object[] { n.ToTuple(), player.ToTuple() }));
        }
        int best = lengths.DefaultIfEmpty(999).Min();
        return best < 999 && lengths.Count(l => l == best) > 1;
    }
}
