using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public static class StructureHelper
{
    public static List<Node> ExtractLeaves(Node parentNode)
    {
        Queue<Node> nodesToCheck = new Queue<Node>();
        List<Node> listToReturn = new List<Node>();

        if (parentNode.childrenNodeList.Count == 0)
        {
            return new List<Node>() { parentNode };
        }
        foreach (var child in parentNode.childrenNodeList)
        {
            nodesToCheck.Enqueue(child);
        }
        while (nodesToCheck.Count > 0)
        {
            var currNode = nodesToCheck.Dequeue();
            if (currNode.childrenNodeList.Count == 0)
            {
                listToReturn.Add(currNode);
            }
            else
            {
                foreach (var child in currNode.childrenNodeList)
                {
                    nodesToCheck.Enqueue(child);
                }
            }
        }
        return listToReturn;
    }

    public static Vector2Int GenerateBottomLeftCorner(Vector2Int boundryLeftPoint, Vector2Int boundryRightPoint, float pointModifier, int offset)
    {
        int minX = boundryLeftPoint.x + offset;
        int maxX = boundryRightPoint.x - offset;
        int minY = boundryLeftPoint.y + offset;
        int maxY = boundryRightPoint.y - offset;
        return new Vector2Int(Random.Range(minX, (int)(minX + (maxX - minX) * pointModifier)), Random.Range(minY, (int)(minY + (maxY - minY) * pointModifier)));
    }

    public static Vector2Int GenerateTopRightCorner(Vector2Int boundryLeftPoint, Vector2Int boundryRightPoint, float pointModifier, int offset)
    {
        int minX = boundryLeftPoint.x + offset;
        int maxX = boundryRightPoint.x - offset;
        int minY = boundryLeftPoint.y + offset;
        int maxY = boundryRightPoint.y - offset;
        return new Vector2Int(Random.Range((int)(minX + (maxX - minX) * pointModifier), maxX), Random.Range((int)(minY + (maxY - minY) * pointModifier), maxY));
    }

    public static Vector2Int CalculateMiddlePoint(Vector2Int v1, Vector2Int v2)
    {
        Vector2 sum = v1 + v2;
        Vector2 tempVector = sum / 2;
        return new Vector2Int((int)tempVector.x, (int)tempVector.y);
    }

    public enum RelativePosition
    {
        Up,
        Down,
        Right,
        Left
    };
}