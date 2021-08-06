using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DungeonCreator : MonoBehaviour
{
    [SerializeField] private int dungeonWidth, dungeonLength;
    [SerializeField] private int roomWidthMin, roomLengthMin;
    [SerializeField] private int maxIterations;
    [SerializeField] private int corridorWidth;
    [SerializeField] private Material material;
    [Range(0.0f, 0.3f)][SerializeField] private float roomBottomCornerModifier;
    [Range(0.7f, 1.0f)][SerializeField] private float roomTopCornerModifier;
    [Range(0, 2)][SerializeField] private int roomOffset;
    [SerializeField] private GameObject wall;
    List<Vector3Int> possibleDoorVerticalPosition;
    List<Vector3Int> possibleDoorHorizontalPosition;
    List<Vector3Int> possibleWallVerticalPosition;
    List<Vector3Int> possibleWallHorizontalPosition;

    // Start is called before the first frame update
    void Start()
    {
        CreateDungeon();
    }

    private void CreateMesh(Vector2 bottomLeftCorner, Vector2 topRightCorner, GameObject wallParent)
    {
        Vector3 bottomLeftV = new Vector3(bottomLeftCorner.x, 0, bottomLeftCorner.y);
        Vector3 bottomRightV = new Vector3(topRightCorner.x, 0, bottomLeftCorner.y);
        Vector3 topLeftV = new Vector3(bottomLeftCorner.x, 0, topRightCorner.y);
        Vector3 topRightV = new Vector3(topRightCorner.x, 0, topRightCorner.y);

        Vector3[] vertices = new Vector3[]
        {
            topLeftV,
            topRightV,
            bottomLeftV,
            bottomRightV
        };

        Vector2[] uvs = new Vector2[vertices.Length];

        for (int i = 0; i < uvs.Length; i++)
        {
            uvs[i] = new Vector2(vertices[i].x, vertices[i].z);
        }

        int[] triangles = new int[]
        {
            0,
            1,
            2,
            2,
            1,
            3
        };

        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        GameObject dungeonFloor = new GameObject("Mesh" + bottomLeftCorner, typeof(MeshFilter), typeof(MeshRenderer));
        dungeonFloor.transform.position = Vector3.zero;
        dungeonFloor.transform.localScale = Vector3.one;
        dungeonFloor.GetComponent<MeshFilter>().mesh = mesh;
        dungeonFloor.GetComponent<MeshRenderer>().material = material;

        for (int row = (int)bottomLeftV.x; row < (int)bottomRightV.x; row++)
        {
            var wallPos = new Vector3(row, 0, bottomLeftV.z);
            AddWallPosToList(wallPos, possibleWallHorizontalPosition, possibleDoorHorizontalPosition);
        }
        for (int row = (int)topLeftV.x; row < (int)topRightCorner.x; row++)
        {
            var wallPos = new Vector3(row, 0, topRightV.z);
            AddWallPosToList(wallPos, possibleWallHorizontalPosition, possibleDoorHorizontalPosition);
        }
        for (int col = (int)bottomLeftV.z; col < (int)topLeftV.z; col++)
        {
            var wallPos = new Vector3(bottomLeftV.x, 0, col);
            AddWallPosToList(wallPos, possibleWallVerticalPosition, possibleDoorVerticalPosition);
        }
        for (int col = (int)bottomRightV.z; col < (int)topRightV.z; col++)
        {
            var wallPos = new Vector3(bottomRightV.x, 0, col);
            AddWallPosToList(wallPos, possibleWallVerticalPosition, possibleDoorVerticalPosition);
        }
    }

    private void AddWallPosToList(Vector3 wallPos, List<Vector3Int> wallList, List<Vector3Int> doorList)
    {
        Vector3Int point = Vector3Int.CeilToInt(wallPos);
        if (wallList.Contains(point))
        {
            doorList.Add(point);
            wallList.Remove(point);
        }
        else
        {
            wallList.Add(point);
        }
    }

    private void CreateDungeon()
    {
        DungeonGenerator generator = new DungeonGenerator(dungeonWidth, dungeonLength);
        var listOfRooms = generator.CalculateDungeon(maxIterations, roomWidthMin, roomLengthMin, roomBottomCornerModifier, roomTopCornerModifier, roomOffset, corridorWidth);
        GameObject wallParent = new GameObject("WallParent");
        wallParent.transform.parent = transform;
        possibleDoorVerticalPosition = new List<Vector3Int>();
        possibleDoorHorizontalPosition = new List<Vector3Int>();
        possibleWallVerticalPosition = new List<Vector3Int>();
        possibleWallHorizontalPosition = new List<Vector3Int>();

        for (int i = 0; i < listOfRooms.Count; i++)
        {
            CreateMesh(listOfRooms[i].bottomLeftAreaCorner, listOfRooms[i].topRightAreaCorner, wallParent);
        }
        CreateWalls(wallParent);
    }

    private void CreateWalls(GameObject wallParent)
    {
        foreach (var wallPos in possibleWallHorizontalPosition)
        {
            CreateWall(wallParent, wallPos, wall, true);
        }
        foreach (var wallPos in possibleWallVerticalPosition)
        {
            CreateWall(wallParent, wallPos, wall, false);
        }
    }

    private void CreateWall(GameObject wallParent, Vector3Int wallPos, GameObject wall, bool isHorizontal)
    {
        if (isHorizontal)
            Instantiate(wall, wallPos, Quaternion.identity, wallParent.transform);
        else
            Instantiate(wall, wallPos, Quaternion.Euler(0, 90, 0), wallParent.transform);
    }
}
