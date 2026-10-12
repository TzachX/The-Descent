using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.Profiling;
using UnityEditor.Compilation;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.TestTools;
using static PathfindingFixtures;
using Debug = UnityEngine.Debug;

// Measures pathfinding the way the game spends it: one enemy turn at a time, each
// enemy asking for its best move and then moving, as TurnManager.EnemyAction does.
// Scenarios: the game's two battle rooms with the game's enemy count, plus larger
// synthetic grids to show how each approach scales.
//
// Two independent measurements:
//   - Stopwatch over many turns (GetBestMove plus the tile writes around it)
//   - Unity Profiler: per-call time and GC allocation of GetBestMove alone,
//     read back from the captured frames through ProfilerMarkers
//
// [Explicit]: run on demand from the Test Runner. Results are logged.
[Category("Benchmark")]
public class PathfindingBenchmark
{
    const int Rounds = 15;

    sealed class Setup
    {
        public bool[][] Grid; // room with the player's and enemies' tiles marked occupied
        public Cell Player;
        public List<Cell> Enemies;
    }

    sealed class Scenario
    {
        public string Name;
        public bool InGame;
        public List<Setup> Setups;
        public ProfilerMarker OriginalMarker, RewriteMarker;
        public string OriginalMarkerName, RewriteMarkerName;
    }

    struct MarkerStats
    {
        public int Calls;
        public double TotalMs, GcBytes;

        public override string ToString() => Calls == 0
            ? "not captured"
            : $"{TotalMs * 1000 / Calls:F2} µs, {GcBytes / Calls:F0} B";
    }

    [UnityTest, Explicit("Benchmark - run on demand")]
    public IEnumerator EnemyTurnCost()
    {
        LegacyMapManager.LogMoves = false;
        var scenarios = BuildScenarios();

        // Stopwatch.
        var original = new Dictionary<Scenario, double>();
        var rewrite = new Dictionary<Scenario, double>();
        var shipped = new Dictionary<Scenario, double>();
        foreach (var s in scenarios)
        {
            original[s] = MedianMicrosecondsPerCall(s, legacy: true, Rounds);
            rewrite[s] = MedianMicrosecondsPerCall(s, legacy: false, Rounds);
            if (s.InGame)
            {
                LegacyMapManager.LogMoves = true; // the shipped code's two print() calls per move
                shipped[s] = MedianMicrosecondsPerCall(s, legacy: true, rounds: 3);
                LegacyMapManager.LogMoves = false;
            }
            yield return null;
        }

        // Profiler: each scenario/version has its own marker, so the totals can be
        // read back without knowing which captured frame holds which run.
        ProfilerDriver.ClearAllFrames();
        ProfilerDriver.profileEditor = true;
        ProfilerDriver.enabled = true;
        yield return null;
        yield return null;
        // Small chunks per frame: the original records a GC.Alloc sample for every
        // allocation, and a whole large-grid scenario in one frame is too big to capture.
        const int setupsPerFrame = 16;
        foreach (var s in scenarios)
        {
            using (var pair = CreatePair(s.Setups[0].Grid, 0))
            {
                for (int first = 0; first < s.Setups.Count; first += setupsPerFrame)
                {
                    var chunk = s.Setups.GetRange(first, Math.Min(setupsPerFrame, s.Setups.Count - first));
                    RunTurns(s, pair, chunk, legacy: false, profiled: true, out long rewriteChecksum, out _);
                    yield return null;
                    RunTurns(s, pair, chunk, legacy: true, profiled: true, out long originalChecksum, out _);
                    yield return null;
                    Assert.AreEqual(originalChecksum, rewriteChecksum, $"{s.Name}: the two versions made different decisions");
                }
            }
        }
        yield return null;
        yield return null;
        ProfilerDriver.enabled = false;

        var report = new StringBuilder();
        report.AppendLine($"Unity {Application.unityVersion}, code optimization: {CompilationPipeline.codeOptimization}, " +
                          $"CPU: {SystemInfo.processorType} ({SystemInfo.processorCount} threads), {SystemInfo.operatingSystem}");
        report.AppendLine();
        report.AppendLine("| Scenario | Stopwatch: original | Stopwatch: distance field | Speed-up | Profiler: original (per call) | Profiler: distance field (per call) |");
        report.AppendLine("|---|---|---|---|---|---|");
        foreach (var s in scenarios)
        {
            report.AppendLine($"| {s.Name} | {original[s]:F2} µs | {rewrite[s]:F2} µs | {original[s] / rewrite[s]:F1}x | " +
                              $"{CollectMarker(s.OriginalMarkerName)} | {CollectMarker(s.RewriteMarkerName)} |");
        }
        report.AppendLine();
        foreach (var s in scenarios.Where(x => x.InGame))
            report.AppendLine($"{s.Name}: original as shipped, including its two print() calls per move: {shipped[s]:F1} µs per move (editor console logging)");

        Debug.Log("[PathfindingBenchmark]\n" + report);
        LegacyMapManager.LogMoves = true;
    }

    // ---------------------------------------------------------------------

    static List<Scenario> BuildScenarios()
    {
        var rng = new System.Random(12345);
        var rooms = GameRooms().ToDictionary(r => r.Name, r => r.Grid);
        var scenarios = new List<Scenario>();

        foreach (var name in new[] { "Room I", "Room II" })
        {
            var room = rooms[name];
            scenarios.Add(CreateScenario($"{name} ({room.Length}x{room[0].Length}), 2 enemies [in game]", true, room, 2, 256, rng));
        }
        foreach (var (size, enemies, setups) in new[] { (10, 4, 256), (20, 8, 128), (40, 16, 64) })
            scenarios.Add(CreateScenario($"{size}x{size}, 15% walls, {enemies} enemies [synthetic]", false, RandomGrid(rng, size, size, 0.15), enemies, setups, rng));

        return scenarios;
    }

    static Scenario CreateScenario(string name, bool inGame, bool[][] room, int enemies, int setups, System.Random rng)
    {
        var s = new Scenario
        {
            Name = name,
            InGame = inGame,
            Setups = Enumerable.Range(0, setups).Select(_ =>
            {
                var grid = Copy(room);
                var (player, placed) = PlaceUnits(grid, enemies, rng);
                return new Setup { Grid = grid, Player = player, Enemies = placed };
            }).ToList(),
            OriginalMarkerName = $"Pathfinding.Original [{name}]",
            RewriteMarkerName = $"Pathfinding.DistanceField [{name}]",
        };
        s.OriginalMarker = new ProfilerMarker(s.OriginalMarkerName);
        s.RewriteMarker = new ProfilerMarker(s.RewriteMarkerName);
        return s;
    }

    // Mean time per GetBestMove call across whole enemy turns, median over rounds.
    static double MedianMicrosecondsPerCall(Scenario s, bool legacy, int rounds)
    {
        var perCall = new List<double>();
        using (var pair = CreatePair(s.Setups[0].Grid, 0))
        {
            RunTurns(s, pair, s.Setups, legacy, profiled: false, out _, out _); // warm-up / JIT
            for (int round = 0; round < rounds; round++)
            {
                int calls = RunTurns(s, pair, s.Setups, legacy, profiled: false, out _, out long ticks);
                perCall.Add(ticks * 1e6 / Stopwatch.Frequency / calls);
            }
        }
        perCall.Sort();
        return perCall[perCall.Count / 2];
    }

    // Plays one enemy turn from every setup, mirroring TurnManager.EnemyAction: a new
    // src tuple per call, then release/claim the tiles (written directly by the
    // original, through SetTileWalkable by the rewrite, as each TurnManager does).
    // Only the enemy turn itself is timed, not the reset before it. The pair is
    // reused across calls, as the game keeps one MapManager for the whole session.
    static int RunTurns(Scenario s, Pair pair, List<Setup> setups, bool legacy, bool profiled, out long checksum, out long ticks)
    {
        checksum = 17;
        ticks = 0;
        int calls = 0;
        var enemies = new List<Cell>();
        var room = legacy ? pair.LegacyRoom : pair.CurrentRoom;

        foreach (var setup in setups)
        {
            // The rewrite's grid is reset through SetTileWalkable so its cache
            // sees the change, as it would in game.
            for (int r = 0; r < setup.Grid.Length; r++)
                for (int c = 0; c < setup.Grid[r].Length; c++)
                    if (legacy) room.Tiles[r].array[c] = setup.Grid[r][c];
                    else pair.Current.SetTileWalkable(r, c, setup.Grid[r][c]);
            pair.SetPlayer(setup.Player);
            enemies.Clear();
            enemies.AddRange(setup.Enemies);
            var dest = setup.Player.ToTuple();

            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < enemies.Count; i++)
            {
                var src = new Tuple<int, int>(enemies[i].Row, enemies[i].Col);
                Tuple<Direction, Direction, bool> data;
                if (legacy)
                {
                    if (profiled) { using (s.OriginalMarker.Auto()) data = pair.Legacy.GetBestMove(src, dest); }
                    else data = pair.Legacy.GetBestMove(src, dest);
                }
                else
                {
                    if (profiled) { using (s.RewriteMarker.Auto()) data = pair.Current.GetBestMove(src, dest); }
                    else data = pair.Current.GetBestMove(src, dest);
                }

                if (!data.Item3 && data.Item2 != Direction.None)
                {
                    var to = Step(enemies[i], data.Item1);
                    if (legacy)
                    {
                        room.Tiles[enemies[i].Row].array[enemies[i].Col] = true;
                        room.Tiles[to.Row].array[to.Col] = false;
                    }
                    else
                    {
                        pair.Current.SetTileWalkable(enemies[i].Row, enemies[i].Col, true);
                        pair.Current.SetTileWalkable(to.Row, to.Col, false);
                    }
                    enemies[i] = to;
                }
                checksum = checksum * 31 + (int)data.Item1 * 2 + (data.Item3 ? 1 : 0);
                calls++;
            }
            ticks += Stopwatch.GetTimestamp() - start;
        }
        return calls;
    }

    // Sums a marker's calls, time and GC allocation over every captured frame.
    static MarkerStats CollectMarker(string markerName)
    {
        var stats = new MarkerStats();
        var children = new List<int>();
        var pending = new Stack<int>();

        for (int frame = ProfilerDriver.firstFrameIndex; frame >= 0 && frame <= ProfilerDriver.lastFrameIndex; frame++)
        {
            using (var view = ProfilerDriver.GetHierarchyFrameDataView(frame, 0,
                       HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false))
            {
                if (view == null || !view.valid) continue;
                int markerId = view.GetMarkerId(markerName);
                if (markerId < 0) continue;

                pending.Clear();
                pending.Push(view.GetRootItemID());
                while (pending.Count > 0)
                {
                    int id = pending.Pop();
                    if (view.GetItemMarkerID(id) == markerId)
                    {
                        stats.Calls += (int)view.GetItemColumnDataAsSingle(id, HierarchyFrameDataView.columnCalls);
                        stats.TotalMs += view.GetItemColumnDataAsSingle(id, HierarchyFrameDataView.columnTotalTime);
                        stats.GcBytes += view.GetItemColumnDataAsSingle(id, HierarchyFrameDataView.columnGcMemory);
                        continue; // time and GC of nested samples are already included
                    }
                    view.GetItemChildren(id, children);
                    foreach (int child in children) pending.Push(child);
                }
            }
        }
        return stats;
    }
}
