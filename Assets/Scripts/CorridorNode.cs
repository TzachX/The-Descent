using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static StructureHelper;

public class CorridorNode : Node
{
    private Node firstStruct;
    private Node secondStruct;
    private int corridorWidth;
    private int modifierDistanceFromWall = 1;

    public CorridorNode(Node firstStruct, Node secondStruct, int corridorWidth) : base(null)
    {
        this.firstStruct = firstStruct;
        this.secondStruct = secondStruct;
        this.corridorWidth = corridorWidth;
        GenerateCorridor();
    }

    private void GenerateCorridor()
    {
        var relativePosition = CheckPosition();
        switch (relativePosition)
        {
            case RelativePosition.Up:
                ProcessRelationUpDown(this.firstStruct, this.secondStruct);
                break;
            case RelativePosition.Down:
                ProcessRelationUpDown(this.secondStruct, this.firstStruct);
                break;
            case RelativePosition.Right:
                ProcessRelationRightLeft(this.firstStruct, this.secondStruct);
                break;
            case RelativePosition.Left:
                ProcessRelationRightLeft(this.secondStruct, this.firstStruct);
                break;
            default:
                break;
        }
    }

    private void ProcessRelationRightLeft(Node fStruct, Node sStruct)
    {
        Node leftStructure = null;
        List<Node> leftStructureChildren = StructureHelper.ExtractLeaves(fStruct);
        Node rightStructure = null;
        List<Node> rightStructureChildren = StructureHelper.ExtractLeaves(sStruct);

        var sortedLeftStructure = leftStructureChildren.OrderByDescending(child => child.topRightAreaCorner.x).ToList();
        if (sortedLeftStructure.Count == 1)
        {
            leftStructure = sortedLeftStructure[0];
        }
        else
        {
            int maxX = sortedLeftStructure[0].topRightAreaCorner.x;
            sortedLeftStructure = sortedLeftStructure.Where(children => Math.Abs(maxX - children.topRightAreaCorner.x) < 10).ToList();
            int index = UnityEngine.Random.Range(0, sortedLeftStructure.Count);
            leftStructure = sortedLeftStructure[index];
        }

        var possibleNeighboursInRightStructureList = rightStructureChildren.Where(
            child => GetValidYForNeighourLeftRight(
                leftStructure.topRightAreaCorner,
                leftStructure.bottomRightAreaCorner,
                child.topLeftAreaCorner,
                child.bottomLeftAreaCorner
                ) != -1
            ).OrderBy(child => child.bottomRightAreaCorner.x).ToList();

        if (possibleNeighboursInRightStructureList.Count <= 0)
        {
            rightStructure = sStruct;
        }
        else
        {
            rightStructure = possibleNeighboursInRightStructureList[0];
        }
        int y = GetValidYForNeighourLeftRight(leftStructure.topLeftAreaCorner, leftStructure.bottomRightAreaCorner,
            rightStructure.topLeftAreaCorner,
            rightStructure.bottomLeftAreaCorner);
        while (y == -1 && sortedLeftStructure.Count > 1)
        {
            sortedLeftStructure = sortedLeftStructure.Where(
                child => child.topLeftAreaCorner.y != leftStructure.topLeftAreaCorner.y).ToList();
            leftStructure = sortedLeftStructure[0];
            y = GetValidYForNeighourLeftRight(leftStructure.topLeftAreaCorner, leftStructure.bottomRightAreaCorner,
            rightStructure.topLeftAreaCorner,
            rightStructure.bottomLeftAreaCorner);
        }
        bottomLeftAreaCorner = new Vector2Int(leftStructure.bottomRightAreaCorner.x, y);
        topRightAreaCorner = new Vector2Int(rightStructure.topLeftAreaCorner.x, y + this.corridorWidth);
    }

    private void ProcessRelationUpDown(Node fStruct, Node sStruct)
    {
        Node bottomStructure = null;
        List<Node> structureBottmChildren = StructureHelper.ExtractLeaves(fStruct);
        Node topStructure = null;
        List<Node> structureAboveChildren = StructureHelper.ExtractLeaves(sStruct);

        var sortedBottomStructure = structureBottmChildren.OrderByDescending(child => child.topRightAreaCorner.y).ToList();

        if (sortedBottomStructure.Count == 1)
        {
            bottomStructure = structureBottmChildren[0];
        }
        else
        {
            int maxY = sortedBottomStructure[0].topLeftAreaCorner.y;
            sortedBottomStructure = sortedBottomStructure.Where(child => Mathf.Abs(maxY - child.topLeftAreaCorner.y) < 10).ToList();
            int index = UnityEngine.Random.Range(0, sortedBottomStructure.Count);
            bottomStructure = sortedBottomStructure[index];
        }

        var possibleNeighboursInTopStructure = structureAboveChildren.Where(
            child => GetValidXForNeighbourUpDown(
                bottomStructure.topLeftAreaCorner,
                bottomStructure.topRightAreaCorner,
                child.bottomLeftAreaCorner,
                child.bottomRightAreaCorner)
            != -1).OrderBy(child => child.bottomRightAreaCorner.y).ToList();
        if (possibleNeighboursInTopStructure.Count == 0)
        {
            topStructure = sStruct;
        }
        else
        {
            topStructure = possibleNeighboursInTopStructure[0];
        }
        int x = GetValidXForNeighbourUpDown(
                bottomStructure.topLeftAreaCorner,
                bottomStructure.topRightAreaCorner,
                topStructure.bottomLeftAreaCorner,
                topStructure.bottomRightAreaCorner);
        while (x == -1 && sortedBottomStructure.Count > 1)
        {
            sortedBottomStructure = sortedBottomStructure.Where(child => child.topLeftAreaCorner.x != topStructure.topLeftAreaCorner.x).ToList();
            bottomStructure = sortedBottomStructure[0];
            x = GetValidXForNeighbourUpDown(
                bottomStructure.topLeftAreaCorner,
                bottomStructure.topRightAreaCorner,
                topStructure.bottomLeftAreaCorner,
                topStructure.bottomRightAreaCorner);
        }
        bottomLeftAreaCorner = new Vector2Int(x, bottomStructure.topLeftAreaCorner.y);
        topRightAreaCorner = new Vector2Int(x + this.corridorWidth, topStructure.bottomLeftAreaCorner.y);
    }

    private int GetValidYForNeighourLeftRight(Vector2Int leftNodeUp, Vector2Int leftNodeDown, Vector2Int rightNodeUp, Vector2Int rightNodeDown)
    {
        if (rightNodeUp.y >= leftNodeUp.y && leftNodeDown.y >= rightNodeDown.y)
        {
            return StructureHelper.CalculateMiddlePoint(
                leftNodeDown + new Vector2Int(0, modifierDistanceFromWall),
                leftNodeUp - new Vector2Int(0, modifierDistanceFromWall + this.corridorWidth)
                ).y;
        }
        if (rightNodeUp.y <= leftNodeUp.y && leftNodeDown.y <= rightNodeDown.y)
        {
            return StructureHelper.CalculateMiddlePoint(
                rightNodeDown + new Vector2Int(0, modifierDistanceFromWall),
                rightNodeUp - new Vector2Int(0, modifierDistanceFromWall + this.corridorWidth)
                ).y;
        }
        if (leftNodeUp.y >= rightNodeDown.y && leftNodeUp.y <= rightNodeUp.y)
        {
            return StructureHelper.CalculateMiddlePoint(
                rightNodeDown + new Vector2Int(0, modifierDistanceFromWall),
                leftNodeUp - new Vector2Int(0, modifierDistanceFromWall)
                ).y;
        }
        if (leftNodeDown.y >= rightNodeDown.y && leftNodeDown.y <= rightNodeUp.y)
        {
            return StructureHelper.CalculateMiddlePoint(
                leftNodeDown + new Vector2Int(0, modifierDistanceFromWall),
                rightNodeUp - new Vector2Int(0, modifierDistanceFromWall + this.corridorWidth)
                ).y;
        }
        return -1;
    }

    private int GetValidXForNeighbourUpDown(Vector2Int bottomNodeLeft,
        Vector2Int bottomNodeRight, Vector2Int topNodeLeft, Vector2Int topNodeRight)
    {
        if (topNodeLeft.x < bottomNodeLeft.x && bottomNodeRight.x < topNodeRight.x)
        {
            return StructureHelper.CalculateMiddlePoint(
                bottomNodeLeft + new Vector2Int(modifierDistanceFromWall, 0),
                bottomNodeRight - new Vector2Int(this.corridorWidth + modifierDistanceFromWall, 0)
                ).x;
        }
        if (topNodeLeft.x >= bottomNodeLeft.x && bottomNodeRight.x >= topNodeRight.x)
        {
            return StructureHelper.CalculateMiddlePoint(
                topNodeLeft + new Vector2Int(modifierDistanceFromWall, 0),
                topNodeRight - new Vector2Int(this.corridorWidth + modifierDistanceFromWall, 0)
                ).x;
        }
        if (bottomNodeLeft.x >= (topNodeLeft.x) && bottomNodeLeft.x <= topNodeRight.x)
        {
            return StructureHelper.CalculateMiddlePoint(
                bottomNodeLeft + new Vector2Int(modifierDistanceFromWall, 0),
                topNodeRight - new Vector2Int(this.corridorWidth + modifierDistanceFromWall, 0)

                ).x;
        }
        if (bottomNodeRight.x <= topNodeRight.x && bottomNodeRight.x >= topNodeLeft.x)
        {
            return StructureHelper.CalculateMiddlePoint(
                topNodeLeft + new Vector2Int(modifierDistanceFromWall, 0),
                bottomNodeRight - new Vector2Int(this.corridorWidth + modifierDistanceFromWall, 0)

                ).x;
        }
        return -1;
    }

    private RelativePosition CheckPosition()
    {
        Vector2 middlePointFirstStructure = ((Vector2)firstStruct.topRightAreaCorner + firstStruct.bottomLeftAreaCorner) / 2;
        Vector2 middlePointSecondStructure = ((Vector2)secondStruct.topRightAreaCorner + secondStruct.bottomLeftAreaCorner) / 2;
        float angle = CalcAngle(middlePointFirstStructure, middlePointSecondStructure);
        if ((angle < 45 && angle >= 0) || (angle > -45 && angle < 0))
        {
            return RelativePosition.Right;
        }
        else if (angle > 45 && angle < 135)
        {
            return RelativePosition.Up;
        }
        else if (angle > -135 && angle < -45)
        {
            return RelativePosition.Down;
        }

        return RelativePosition.Left;
    }

    private float CalcAngle(Vector2 middlePointFirstStructure, Vector2 middlePointSecondStructure) { return Mathf.Atan2(middlePointSecondStructure.y - middlePointFirstStructure.y, middlePointSecondStructure.x - middlePointFirstStructure.x) * Mathf.Rad2Deg; }
}