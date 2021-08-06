using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

internal class BinarySpacePartitioner
{
    private RoomNode rootNode;
    private int dungeonWidth;
    private int dungeonLength;

    public BinarySpacePartitioner(int dungeonWidth, int dungeonLength)
    {
        this.rootNode = new RoomNode(new Vector2Int(0, 0), new Vector2Int(dungeonWidth, dungeonLength), null, 0);
    }

    public RoomNode RootNode { get => rootNode; }

    public List<RoomNode> PrepareNodesList(int maxIterations, int roomWidthMin, int roomLengthMin)
    {
        Queue<RoomNode> graph = new Queue<RoomNode>();
        List<RoomNode> listToReturn = new List<RoomNode>();
        graph.Enqueue(this.rootNode);
        listToReturn.Add(this.rootNode);
        int iterations = 0;
        while (iterations < maxIterations && graph.Count > 0)
        {
            iterations++;
            RoomNode currNode = graph.Dequeue();
            if (currNode.Width >= roomWidthMin * 2 || currNode.Length <= roomLengthMin * 2)
            {
                SpaceSplit(currNode, listToReturn, roomLengthMin, roomWidthMin, graph);
            }
        }

        return listToReturn;
    }

    private void SpaceSplit(RoomNode currNode, List<RoomNode> listToReturn, int roomLengthMin, int roomWidthMin, Queue<RoomNode> graph)
    {
        Line line = GetDividingLine(currNode.bottomLeftAreaCorner, currNode.topRightAreaCorner, roomWidthMin, roomLengthMin);
        RoomNode fNode, sNode;
        if (line.Orientation == Orientation.Horizontal)
        {
            fNode = new RoomNode(currNode.bottomLeftAreaCorner, new Vector2Int(currNode.topRightAreaCorner.x, line.Coordinates.y), currNode, currNode.treeLayerIndex + 1);
            sNode = new RoomNode(new Vector2Int(currNode.bottomLeftAreaCorner.x, line.Coordinates.y), currNode.topRightAreaCorner, currNode, currNode.treeLayerIndex + 1);
        }
        else
        {
            fNode = new RoomNode(currNode.bottomLeftAreaCorner, new Vector2Int(line.Coordinates.x, currNode.topRightAreaCorner.y), currNode, currNode.treeLayerIndex + 1);
            sNode = new RoomNode(new Vector2Int(line.Coordinates.x, currNode.bottomLeftAreaCorner.y), currNode.topRightAreaCorner, currNode, currNode.treeLayerIndex + 1);
        }
        AddNode(listToReturn, graph, fNode);
        AddNode(listToReturn, graph, sNode);
    }

    private void AddNode(List<RoomNode> listToReturn, Queue<RoomNode> graph, RoomNode node)
    {
        listToReturn.Add(node);
        graph.Enqueue(node);

    }

    private Line GetDividingLine(Vector2Int bottomLeftAreaCorner, Vector2Int topRightAreaCorner, int roomWidthMin, int roomLengthMin)
    {
        Orientation orientation;
        bool lengthStatus = (topRightAreaCorner.y - bottomLeftAreaCorner.y) >= 2 * roomLengthMin;
        bool widthStatus = (topRightAreaCorner.x - bottomLeftAreaCorner.x) >= 2 * roomWidthMin;

        if (lengthStatus && widthStatus)
        {
            orientation = (Orientation)(Random.Range(0, 2));
        }
        else if (widthStatus)
        {
            orientation = Orientation.Vertical;
        }
        else
        {
            orientation = Orientation.Horizontal;
        }
        return new Line(orientation, GetCoordinates(orientation, bottomLeftAreaCorner, topRightAreaCorner, roomWidthMin, roomLengthMin));
    }

    private Vector2Int GetCoordinates(Orientation orientation, Vector2Int bottomLeftAreaCorner, Vector2Int topRightAreaCorner, int roomWidthMin, int roomLengthMin)
    {
        Vector2Int coordinates = Vector2Int.zero;
        if (orientation == Orientation.Horizontal)
        {
            coordinates = new Vector2Int(0, Random.Range((bottomLeftAreaCorner.y + roomLengthMin), (topRightAreaCorner.y - roomLengthMin)));
        }
        else
        {
            coordinates = new Vector2Int(Random.Range((bottomLeftAreaCorner.x + roomWidthMin), (topRightAreaCorner.x - roomWidthMin)), 0);
        }
        return coordinates;
    }
}