using System;
using System.Collections.Generic;
using UnityEngine;

public class RoomGenerator
{
    private int maxIterations;
    private int roomLengthMin;
    private int roomWidthMin;

    public RoomGenerator(int maxIterations, int roomLengthMin, int roomWidthMin)
    {
        this.maxIterations = maxIterations;
        this.roomLengthMin = roomLengthMin;
        this.roomWidthMin = roomWidthMin;
    }

    public List<RoomNode> GenerateRoom(List<Node> nodes, float roomBottomCornerModifier, float roomTopCornerModifier, int roomOffset)
    {
        List<RoomNode> roomList = new List<RoomNode>();

        foreach (var node in nodes)
        {
            Vector2Int newBottomLeftPoint = StructureHelper.GenerateBottomLeftCorner(node.bottomLeftAreaCorner, node.topRightAreaCorner, roomBottomCornerModifier, roomOffset);
            Vector2Int newTopRightPoint = StructureHelper.GenerateTopRightCorner(node.bottomLeftAreaCorner, node.topRightAreaCorner, roomTopCornerModifier, roomOffset);
            node.bottomLeftAreaCorner = newBottomLeftPoint;
            node.topRightAreaCorner = newTopRightPoint;
            node.bottomRightAreaCorner = new Vector2Int(newTopRightPoint.x, newBottomLeftPoint.y);
            node.topLeftAreaCorner = new Vector2Int(newBottomLeftPoint.x, newTopRightPoint.y);
            roomList.Add((RoomNode)node);
        }
        return roomList;
    }
}