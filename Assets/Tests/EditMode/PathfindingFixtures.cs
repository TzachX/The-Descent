using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using LvlGen.Scripts;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Shared setup for the pathfinding tests and benchmark: grids, the game's room
// prefabs, and a legacy/current MapManager pair over identical copies of a grid.
public static class PathfindingFixtures
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly string[] OrientationFields = { "upDirs", "rightDirs", "downDirs", "leftDirs" };

    public static readonly Direction[] InputDirections = { Direction.Up, Direction.Down, Direction.Left, Direction.Right };

    public struct Cell : IEquatable<Cell>
    {
        public readonly int Row, Col;
        public Cell(int row, int col) { Row = row; Col = col; }
        public Tuple<int, int> ToTuple() => new Tuple<int, int>(Row, Col);
        public static Cell From(Tuple<int, int> t) => new Cell(t.Item1, t.Item2);
        public bool Equals(Cell other) => Row == other.Row && Col == other.Col;
        public override bool Equals(object obj) => obj is Cell t && Equals(t);
        public override int GetHashCode() => Row * 397 ^ Col;
        public override string ToString() => $"({Row},{Col})";
    }

    // Two managers, each over its own copy of the same grid, so they can be
    // driven side by side and compared decision by decision.
    public sealed class Pair : IDisposable
    {
        public LegacyMapManager Legacy;
        public MapManager Current;
        public Section LegacyRoom, CurrentRoom;
        internal readonly List<GameObject> Objects = new List<GameObject>();

        public void SetPlayer(Cell player)
        {
            Legacy.PlayerCoords = player.ToTuple();
            Current.PlayerCoords = player.ToTuple();
        }

        public bool GridsMatch()
        {
            for (int r = 0; r < LegacyRoom.Tiles.Length; r++)
                for (int c = 0; c < LegacyRoom.Tiles[r].array.Length; c++)
                    if (LegacyRoom.Tiles[r].array[c] != CurrentRoom.Tiles[r].array[c])
                        return false;
            return true;
        }

        public void Dispose()
        {
            foreach (var go in Objects) Object.DestroyImmediate(go);
            Objects.Clear();
        }
    }

    public static Pair CreatePair(bool[][] grid, int orientation)
    {
        var pair = new Pair();
        pair.LegacyRoom = CreateRoom(grid, pair.Objects);
        pair.CurrentRoom = CreateRoom(grid, pair.Objects);
        pair.Legacy = Create<LegacyMapManager>(pair.Objects);
        pair.Current = Create<MapManager>(pair.Objects);
        pair.Legacy.CurrPlayerSection = pair.LegacyRoom;
        pair.Current.CurrPlayerSection = pair.CurrentRoom;
        SetOrientation(pair.Legacy, orientation);
        SetOrientation(pair.Current, orientation);
        return pair;
    }

    public static Section CreateRoom(bool[][] grid, List<GameObject> owner)
    {
        var room = Create<Section>(owner);
        room.Tiles = grid.Select(row => new Rows { array = (bool[])row.Clone() }).ToArray();
        return room;
    }

    static T Create<T>(List<GameObject> owner) where T : Component
    {
        var go = new GameObject(typeof(T).Name) { hideFlags = HideFlags.HideAndDontSave };
        owner.Add(go);
        return go.AddComponent<T>();
    }

    // Orientation 0..3 = the room faces Up/Right/Down/Left, which decides how
    // screen-relative input maps onto grid directions.
    static void SetOrientation(MonoBehaviour manager, int orientation)
    {
        var type = manager.GetType();
        var dirs = type.GetField(OrientationFields[orientation], Private).GetValue(manager);
        type.GetField("currDirs", Private).SetValue(manager, dirs);
    }

    public static bool[][] RandomGrid(System.Random rng, int rows, int cols, double wallChance) =>
        Enumerable.Range(0, rows)
            .Select(_ => Enumerable.Range(0, cols).Select(__ => rng.NextDouble() >= wallChance).ToArray())
            .ToArray();

    public static bool[][] Copy(bool[][] grid) => grid.Select(row => (bool[])row.Clone()).ToArray();

    public static List<Cell> WalkableTiles(bool[][] grid)
    {
        var tiles = new List<Cell>();
        for (int r = 0; r < grid.Length; r++)
            for (int c = 0; c < grid[r].Length; c++)
                if (grid[r][c]) tiles.Add(new Cell(r, c));
        return tiles;
    }

    // Every Section prefab under Assets/Rooms, i.e. the rooms the level generator places.
    public static List<(string Name, bool[][] Grid)> GameRooms() =>
        AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Rooms" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(path => (path, section: AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<Section>()))
            .Where(x => x.section != null && x.section.Tiles != null && x.section.Tiles.Length > 0)
            .Select(x => (System.IO.Path.GetFileNameWithoutExtension(x.path), x.section.Tiles.Select(r => (bool[])r.array.Clone()).ToArray()))
            .OrderBy(x => x.Item1)
            .ToList();

    // Picks a player tile and enemy tiles the way TurnManager.InitBattle does
    // (distinct walkable tiles) and marks them occupied.
    public static (Cell Player, List<Cell> Enemies) PlaceUnits(bool[][] grid, int enemyCount, System.Random rng)
    {
        var free = WalkableTiles(grid);
        Shuffle(free, rng);
        var player = free[0];
        var enemies = free.Skip(1).Take(Math.Min(enemyCount, free.Count - 1)).ToList();
        grid[player.Row][player.Col] = false;
        foreach (var e in enemies) grid[e.Row][e.Col] = false;
        return (player, enemies);
    }

    public static void Shuffle<T>(IList<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // Same mapping TurnManager applies to a move's real direction.
    public static Cell Step(Cell t, Direction realDirection)
    {
        switch (realDirection)
        {
            case Direction.Up: return new Cell(t.Row - 1, t.Col);
            case Direction.Down: return new Cell(t.Row + 1, t.Col);
            case Direction.Right: return new Cell(t.Row, t.Col + 1);
            case Direction.Left: return new Cell(t.Row, t.Col - 1);
            default: return t;
        }
    }

    public static string Describe(Tuple<Direction, Direction, bool> move) =>
        $"{move.Item1}/{move.Item2}/{(move.Item3 ? "attack" : "move")}";

    public static string Render(Section room, Cell player, IEnumerable<Cell> enemies)
    {
        var enemySet = new HashSet<Cell>(enemies);
        var sb = new StringBuilder();
        for (int r = 0; r < room.Tiles.Length; r++)
        {
            for (int c = 0; c < room.Tiles[r].array.Length; c++)
            {
                var t = new Cell(r, c);
                sb.Append(t.Equals(player) ? 'P' : enemySet.Contains(t) ? 'E' : room.Tiles[r].array[c] ? '.' : '#');
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }
}
