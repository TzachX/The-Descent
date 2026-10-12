using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LvlGen.Scripts;
using System;
using DG.Tweening;
using System.Linq;


// A Data Structure for queue used in BFS
public class queueNode
{
    // The coordinates of a cell
    public Tuple<int, int> pt;

    // cell's distance of from the source
    public int dist;

    public queueNode(Tuple<int, int> pt, int dist)
    {
        this.pt = pt;
        this.dist = dist;
    }
};


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


    // Implementing BFS
    private int GetPathLength(Tuple<int, int> src, Tuple<int, int> dest)
    {

        int[] rowNum = { -1, 0, 0, 1 };
        int[] colNum = { 0, -1, 1, 0 };
        bool[,] visited = new bool[currPlayerSection.Tiles.Length, currPlayerSection.Tiles[0].array.Length];
        visited[src.Item1, src.Item2] = true;
        Queue<queueNode> q = new Queue<queueNode>();

        queueNode s = new queueNode(src, 0);
        q.Enqueue(s); // Enqueue source cell

        // Do a BFS starting from source cell
        while (q.Count != 0)
        {
            queueNode curr = q.Peek();
            Tuple<int, int> pt = curr.pt;

            // If we have reached the destination cell,
            // we are done
            if (pt.Item1 == dest.Item1 && pt.Item2 == dest.Item2)
                return curr.dist;

            // Otherwise dequeue the front cell
            // in the queue and enqueue
            // its adjacent cells
            q.Dequeue();

            for (int i = 0; i < 4; i++)
            {
                int row = pt.Item1 + rowNum[i];
                int col = pt.Item2 + colNum[i];

                // if adjacent cell is valid, has path
                // and not visited yet, enqueue it.
                if (IsValidCoord(row, col))
                {
                    if ((!visited[row, col] && currPlayerSection.Tiles[row].array[col]) || (row == dest.Item1 && col == dest.Item2))
                    {
                        // mark cell as visited and enqueue it
                        visited[row, col] = true;
                        queueNode Adjcell = new queueNode
                                   (new Tuple<int, int>(row, col),
                                        curr.dist + 1);
                        q.Enqueue(Adjcell);
                    }
                }
            }
        }

        return 999;
    }


    public Tuple<Direction, Direction, bool> GetBestMove(Tuple<int, int> src, Tuple<int, int> dest)
    {
        int up, right, down, left;
        up = right = down = left = 999;

        // Up
        if (IsValidCoord(src.Item1 - 1, src.Item2))
        {
            if (PlayerCoords.Item1 == src.Item1 - 1 && PlayerCoords.Item2 == src.Item2) { return new Tuple<Direction, Direction, bool>(upDirs[0], currDirs[0], true); }

            if (currPlayerSection.Tiles[src.Item1 - 1].array[src.Item2])
                up = GetPathLength(new Tuple<int, int>(src.Item1 - 1, src.Item2), dest);
        }
        // Right
        if (IsValidCoord(src.Item1, src.Item2 + 1))
        {
            if (PlayerCoords.Item1 == src.Item1 && PlayerCoords.Item2 == src.Item2 + 1) { return new Tuple<Direction, Direction, bool>(upDirs[1], currDirs[1], true); }

            if (currPlayerSection.Tiles[src.Item1].array[src.Item2 + 1])
                right = GetPathLength(new Tuple<int, int>(src.Item1, src.Item2 + 1), dest);
        }

        if (IsValidCoord(src.Item1 + 1, src.Item2))
        {
            if (PlayerCoords.Item1 == src.Item1 + 1 && PlayerCoords.Item2 == src.Item2) { return new Tuple<Direction, Direction, bool>(upDirs[2], currDirs[2], true); }

            if (currPlayerSection.Tiles[src.Item1 + 1].array[src.Item2])
                down = GetPathLength(new Tuple<int, int>(src.Item1 + 1, src.Item2), dest);
        }

        if (IsValidCoord(src.Item1, src.Item2 - 1))
        {
            if (PlayerCoords.Item1 == src.Item1 && PlayerCoords.Item2 == src.Item2 - 1) { return new Tuple<Direction, Direction, bool>(upDirs[3], currDirs[3], true); }

            if (currPlayerSection.Tiles[src.Item1].array[src.Item2 - 1])
                left = GetPathLength(new Tuple<int, int>(src.Item1, src.Item2 - 1), dest);
        }

        int[] dirs = { up, right, down, left };
        int minIndex = Array.IndexOf(dirs, dirs.Min());

        if (dirs[minIndex] == 999 || dirs[minIndex] == 0)
            return new Tuple<Direction, Direction, bool>(Direction.None, Direction.None, false);

        print(upDirs[minIndex] + " " + currDirs[minIndex]);
        print(src.Item1 + " " + src.Item2);

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

    private void SetCoordsAvailability(Section room, Tuple<int, int> coords, bool isAvailable) { room.Tiles[coords.Item1].array[coords.Item2] = isAvailable; }

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
