using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LvlGen.Scripts;
using System;
using DG.Tweening;
using System.Linq;


public class MapManager : MonoBehaviour
{
    [SerializeField] private Material wallMat;
    private Section currPlayerSection;
    private Tuple<int, int> playerCoords;
    private Direction[] upDirs = { Direction.Up, Direction.Right, Direction.Down, Direction.Left };
    private Direction[] rightDirs = { Direction.Right, Direction.Down, Direction.Left, Direction.Up };
    private Direction[] downDirs = { Direction.Down, Direction.Left, Direction.Up, Direction.Right };
    private Direction[] leftDirs = { Direction.Left, Direction.Up, Direction.Right, Direction.Down };
    private Direction[] currDirs;
    private bool isBattleInit = false;
    private const float TILE_SIZE = 2.5f;

    public bool IsBattleInit { get => isBattleInit; set => isBattleInit = value; }
    public Section CurrPlayerSection { get => currPlayerSection; set => currPlayerSection = value; }
    public Tuple<int, int> PlayerCoords { get => playerCoords; set => playerCoords = value; }

    private int mod(int x, int m)
    {
        return (x % m + m) % m;
    }

    private Direction GetRelativeDirection(Direction dir)
    {
        int index = Array.FindIndex(currDirs, x => x == dir);

        switch (index)
        {
            case 0: return Direction.Up;
            case 1: return Direction.Right;
            case 2: return Direction.Down;
            case 3: return Direction.Left;
        }
        return Direction.None;
    }


    private bool IsValidCoord(int row, int col)
    {
        return (row >= 0 && row < currPlayerSection.Tiles.Length) && (col >= 0 && col < currPlayerSection.Tiles[row].array.Length);
    }


    // ------------------------------------------------------------------
    //  Pathfinding
    //
    //  Every enemy paths toward the same destination (the player), so the
    //  distance to that destination is a property of the grid rather than of
    //  any one enemy. One breadth-first sweep outward from the destination
    //  labels each tile with its step count; an enemy then simply reads the
    //  neighbouring label and walks downhill.
    //
    //  This replaces four traversals per enemy move -- one probe per
    //  direction -- with one sweep, reused for as long as the grid and the
    //  player's tile stay the same.
    //
    //  The grid is mutated mid-turn: an enemy releases the tile it leaves and
    //  occupies the one it enters. So the field cannot just be built once per
    //  turn without changing behaviour -- later enemies would stop seeing the
    //  ones that already moved. Every walkability write bumps gridVersion
    //  (enemies write through SetTileWalkable, the player's own moves through
    //  SetCoordsAvailability), which invalidates the field, so it is rebuilt
    //  exactly when the grid it was derived from no longer holds, and not
    //  otherwise.
    //
    //  Buffers are allocated once and reused, so rebuilding the field
    //  allocates nothing; the only allocation left per move is the returned
    //  tuple.
    // ------------------------------------------------------------------

    private const int UNREACHABLE = 999;

    private static readonly int[] NEIGHBOUR_ROW = { -1, 0, 1, 0 };
    private static readonly int[] NEIGHBOUR_COL = { 0, 1, 0, -1 };

    private int[] distField;   // row-major: steps from each tile to the cached destination
    private int[] bfsQueue;    // preallocated FIFO of packed tile indices
    private int fieldRows, fieldCols;
    private int gridVersion;
    private int fieldVersion = -1;
    private int fieldDestRow = int.MinValue, fieldDestCol = int.MinValue;
    private Section fieldSection;

    // Walkability changes in the current room. Bumping gridVersion means the
    // cached field cannot go stale behind our back.
    public void SetTileWalkable(int row, int col, bool walkable)
    {
        currPlayerSection.Tiles[row].array[col] = walkable;
        gridVersion++;
    }

    private bool IsWalkable(int row, int col)
    {
        return currPlayerSection.Tiles[row].array[col];
    }

    private void EnsureDistanceField(Tuple<int, int> dest)
    {
        if (distField != null
            && fieldSection == currPlayerSection
            && fieldVersion == gridVersion
            && fieldDestRow == dest.Item1
            && fieldDestCol == dest.Item2)
            return;

        BuildDistanceField(dest);
    }

    private void BuildDistanceField(Tuple<int, int> dest)
    {
        int rows = currPlayerSection.Tiles.Length;
        int cols = currPlayerSection.Tiles[0].array.Length;

        if (distField == null || fieldRows != rows || fieldCols != cols)
        {
            distField = new int[rows * cols];
            bfsQueue = new int[rows * cols];
            fieldRows = rows;
            fieldCols = cols;
        }

        for (int i = 0; i < distField.Length; i++)
            distField[i] = UNREACHABLE;

        int head = 0, tail = 0;

        // The destination is always enterable, whether or not it is walkable.
        int destIndex = dest.Item1 * cols + dest.Item2;
        distField[destIndex] = 0;
        bfsQueue[tail++] = destIndex;

        while (head < tail)
        {
            int current = bfsQueue[head++];
            int row = current / cols;
            int col = current - row * cols;
            int step = distField[current] + 1;

            for (int i = 0; i < 4; i++)
            {
                int r = row + NEIGHBOUR_ROW[i];
                int c = col + NEIGHBOUR_COL[i];

                if (!IsValidCoord(r, c))
                    continue;

                int index = r * cols + c;

                if (distField[index] != UNREACHABLE)
                    continue;              // already labelled by a shorter path
                if (!IsWalkable(r, c))
                    continue;              // wall, or a tile another enemy holds

                distField[index] = step;
                bfsQueue[tail++] = index;
            }
        }

        fieldSection = currPlayerSection;
        fieldVersion = gridVersion;
        fieldDestRow = dest.Item1;
        fieldDestCol = dest.Item2;
    }

    private int StepsToDest(int row, int col)
    {
        return distField[row * fieldCols + col];
    }


    public Tuple<Direction, Direction, bool> GetBestMove(Tuple<int, int> src, Tuple<int, int> dest)
    {
        EnsureDistanceField(dest);

        int up, right, down, left;
        up = right = down = left = UNREACHABLE;

        // Up
        if (IsValidCoord(src.Item1 - 1, src.Item2))
        {
            if (PlayerCoords.Item1 == src.Item1 - 1 && PlayerCoords.Item2 == src.Item2)
                return new Tuple<Direction, Direction, bool>(upDirs[0], currDirs[0], true);

            if (IsWalkable(src.Item1 - 1, src.Item2))
                up = StepsToDest(src.Item1 - 1, src.Item2);
        }
        // Right
        if (IsValidCoord(src.Item1, src.Item2 + 1))
        {
            if (PlayerCoords.Item1 == src.Item1 && PlayerCoords.Item2 == src.Item2 + 1)
                return new Tuple<Direction, Direction, bool>(upDirs[1], currDirs[1], true);

            if (IsWalkable(src.Item1, src.Item2 + 1))
                right = StepsToDest(src.Item1, src.Item2 + 1);
        }
        // Down
        if (IsValidCoord(src.Item1 + 1, src.Item2))
        {
            if (PlayerCoords.Item1 == src.Item1 + 1 && PlayerCoords.Item2 == src.Item2)
                return new Tuple<Direction, Direction, bool>(upDirs[2], currDirs[2], true);

            if (IsWalkable(src.Item1 + 1, src.Item2))
                down = StepsToDest(src.Item1 + 1, src.Item2);
        }
        // Left
        if (IsValidCoord(src.Item1, src.Item2 - 1))
        {
            if (PlayerCoords.Item1 == src.Item1 && PlayerCoords.Item2 == src.Item2 - 1)
                return new Tuple<Direction, Direction, bool>(upDirs[3], currDirs[3], true);

            if (IsWalkable(src.Item1, src.Item2 - 1))
                left = StepsToDest(src.Item1, src.Item2 - 1);
        }

        // Lowest label wins; ties resolve to the earliest direction, as before.
        int minIndex = 0;
        int minSteps = up;
        if (right < minSteps) { minSteps = right; minIndex = 1; }
        if (down < minSteps) { minSteps = down; minIndex = 2; }
        if (left < minSteps) { minSteps = left; minIndex = 3; }

        if (minSteps == UNREACHABLE || minSteps == 0)
            return new Tuple<Direction, Direction, bool>(Direction.None, Direction.None, false);

        // (Real direction, Relative direction, Will the enemy attack?)
        return new Tuple<Direction, Direction, bool>(upDirs[minIndex], currDirs[minIndex], false);
    }


    private Mummy IsMummyOnTile(List<Mummy> mummies, int row, int col)
    {
        if (!currPlayerSection.Tiles[row].array[col])
        {
            for (int i = 0; i < mummies.Count; i++)
            {
                if (mummies[i].Row == row && mummies[i].Col == col)
                    return mummies[i];
            }
        }
        return null;
    }


    public Tuple<Mummy, bool> IsMummyInRange(List<Mummy> mummies, Direction dir)
    {
        Direction relDir = GetRelativeDirection(dir);

        switch (relDir)
        {
            case Direction.Up:
                for (int row = playerCoords.Item1 - 1; row >= 0; row--)
                {
                    Mummy target = IsMummyOnTile(mummies, row, playerCoords.Item2);
                    if (target != null)
                    {
                        if (row == playerCoords.Item1 - 1)
                            return new Tuple<Mummy, bool>(target, true);

                        return new Tuple<Mummy, bool>(target, false);
                    }
                }
                break;
            case Direction.Down:
                for (int row = playerCoords.Item1 + 1; row < currPlayerSection.Tiles.Length; row++)
                {
                    Mummy target = IsMummyOnTile(mummies, row, playerCoords.Item2);
                    if (target != null)
                    {
                        if (row == playerCoords.Item1 + 1)
                            return new Tuple<Mummy, bool>(target, true);

                        return new Tuple<Mummy, bool>(target, false);
                    }
                }
                break;
            case Direction.Left:
                for (int col = playerCoords.Item2 - 1; col >= 0; col--)
                {
                    Mummy target = IsMummyOnTile(mummies, playerCoords.Item1, col);
                    if (target != null)
                    {
                        if (col == playerCoords.Item2 - 1)
                            return new Tuple<Mummy, bool>(target, true);

                        return new Tuple<Mummy, bool>(target, false);
                    }
                }
                break;
            case Direction.Right:
                for (int col = playerCoords.Item2 + 1; col < currPlayerSection.Tiles[playerCoords.Item1].array.Length; col++)
                {
                    Mummy target = IsMummyOnTile(mummies, playerCoords.Item1, col);
                    if (target != null)
                    {
                        if (col == playerCoords.Item2 + 1)
                            return new Tuple<Mummy, bool>(target, true);

                        return new Tuple<Mummy, bool>(target, false);
                    }
                }
                break;
        }

        return new Tuple<Mummy, bool>(null, false);
    }


    public bool AddPlayerToRoom(Direction trueDir)
    {
        if (currPlayerSection.Transfers.ContainsKey(PlayerCoords))
        {
            Section room = currPlayerSection.Transfers[PlayerCoords].Item1;
            if (!currPlayerSection.IsLocked && !room.IsLocked)
            {
                Tuple<int, int> coords = currPlayerSection.Transfers[PlayerCoords].Item2;
                Direction prevOrientation = currPlayerSection.Orientation;
                SetCoordsAvailability(currPlayerSection, PlayerCoords, true);
                currPlayerSection = room;
                PlayerCoords = coords;
                SetCoordsAvailability(currPlayerSection, PlayerCoords, false);
                ChangeOrientation(prevOrientation, room, trueDir);
                if (currPlayerSection.HasEnemies && !currPlayerSection.IsCleared) IsBattleInit = true;
                return true;
            }
        }

        return false;
    }


    public Vector3 FindCoordPos(int row, int col)
    {
        print(row + " " + col);
        Vector3 entrancePos = currPlayerSection.transform.position;
        float addRow = TILE_SIZE / 2 + TILE_SIZE * (currPlayerSection.EntranceRow - row);
        float addCol = TILE_SIZE * (currPlayerSection.EntranceCol - col);

        switch (currPlayerSection.Orientation)
        {
            case Direction.Up:
                return new Vector3(entrancePos.x + addRow, 0, entrancePos.z + addCol);
            case Direction.Right:// V
                return new Vector3(entrancePos.x + addCol, 0, entrancePos.z - addRow);
            case Direction.Down:
                return new Vector3(entrancePos.x - addRow, 0, entrancePos.z - addCol);
            case Direction.Left: // V
                return new Vector3(entrancePos.x - addCol, 0, entrancePos.z + addRow);
        }

        return Vector3.zero;
    }

    public void ToggleWalls(bool isActive) 
    { 
        if (isActive) 
        {
            wallMat.DOFade(1, "_TintColor", 1).SetEase(Ease.Linear);
        }
        else
        {
            wallMat.DOFade(0, "_TintColor", 1).SetEase(Ease.Linear);
        }
    }

    private void ChangeOrientation(Direction prevOrientation, Section room, Direction trueDir)
    {
        Direction nextOrientation = room.Orientation;

        if (nextOrientation == Direction.None)
        {
            if (trueDir == Direction.Up)
            {
                nextOrientation = prevOrientation;
                room.Orientation = nextOrientation;
            }
            else
            {
                int index = Array.FindIndex(upDirs, x => x == prevOrientation);
                if (trueDir == Direction.Right) index++;
                else if (trueDir == Direction.Left) index--;
                nextOrientation = upDirs[mod(index, upDirs.Length)];
                room.Orientation = nextOrientation;
            }
        }

        switch (nextOrientation)
        {
            case Direction.Up:
                currDirs = upDirs;
                break;
            case Direction.Right:
                currDirs = rightDirs;
                break;
            case Direction.Down:
                currDirs = downDirs;
                break;
            case Direction.Left:
                currDirs = leftDirs;
                break;
        }
    }

    public void AddPlayerToSpawnRoom(Section spawnRoom)
    {
        CurrPlayerSection = spawnRoom;
        PlayerCoords = new Tuple<int, int>(2, 2);
        SetCoordsAvailability(currPlayerSection, PlayerCoords, false);
        spawnRoom.Orientation = Direction.Up;
        currDirs = upDirs;
    }

    private void SetCoordsAvailability(Section room, Tuple<int, int> coords, bool isAvailable)
    {
        room.Tiles[coords.Item1].array[coords.Item2] = isAvailable;
        gridVersion++;
    }

    public bool CheckPlayerMove(Direction dir)
    {
        Direction relDir = GetRelativeDirection(dir);
        IsBattleInit = false;

        switch (relDir)
        {
            case Direction.Up:
                if (PlayerCoords.Item1 > 0)
                {
                    if (CurrPlayerSection.Tiles[PlayerCoords.Item1 - 1].array[PlayerCoords.Item2])
                    {
                        SetCoordsAvailability(currPlayerSection, PlayerCoords, true);
                        PlayerCoords = new Tuple<int, int>(PlayerCoords.Item1 - 1, PlayerCoords.Item2);
                        SetCoordsAvailability(currPlayerSection, PlayerCoords, false);
                        return true;
                    }
                }
                else return AddPlayerToRoom(relDir);
                break;
            case Direction.Down:
                if (PlayerCoords.Item1 < CurrPlayerSection.Tiles.Length - 1)
                {
                    if (CurrPlayerSection.Tiles[PlayerCoords.Item1 + 1].array[PlayerCoords.Item2])
                    {
                        SetCoordsAvailability(currPlayerSection, PlayerCoords, true);
                        PlayerCoords = new Tuple<int, int>(PlayerCoords.Item1 + 1, PlayerCoords.Item2);
                        SetCoordsAvailability(currPlayerSection, PlayerCoords, false);
                        return true;
                    }
                }
                else return AddPlayerToRoom(relDir);
                break;
            case Direction.Left:
                if (PlayerCoords.Item2 > 0)
                {
                    if (CurrPlayerSection.Tiles[PlayerCoords.Item1].array[PlayerCoords.Item2 - 1])
                    {
                        SetCoordsAvailability(currPlayerSection, PlayerCoords, true);
                        PlayerCoords = new Tuple<int, int>(PlayerCoords.Item1, PlayerCoords.Item2 - 1);
                        SetCoordsAvailability(currPlayerSection, PlayerCoords, false);
                        return true;
                    }
                }
                else return AddPlayerToRoom(relDir);
                break;
            case Direction.Right:
                if (PlayerCoords.Item2 < CurrPlayerSection.Tiles[0].array.Length - 1)
                {
                    if (CurrPlayerSection.Tiles[PlayerCoords.Item1].array[PlayerCoords.Item2 + 1])
                    {
                        SetCoordsAvailability(currPlayerSection, PlayerCoords, true);
                        PlayerCoords = new Tuple<int, int>(PlayerCoords.Item1, PlayerCoords.Item2 + 1);
                        SetCoordsAvailability(currPlayerSection, PlayerCoords, false);
                        return true;
                    }
                }
                else return AddPlayerToRoom(relDir);
                break;
        }

        return false;
    }
}
