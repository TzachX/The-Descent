using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class Node
{
    private List<Node> childtenNodeList;

    public List<Node> childrenNodeList { get => childtenNodeList; set => childtenNodeList = value; }

    public bool isVisited { get; set; }
    public Vector2Int bottomLeftAreaCorner { get; set; }
    public Vector2Int bottomRightAreaCorner { get; set; }
    public Vector2Int topRightAreaCorner { get; set; }
    public Vector2Int topLeftAreaCorner { get; set; }

    public Node Parent { get; set; }

    public int treeLayerIndex { get; set; }

    public Node(Node parentNode)
    {
        childtenNodeList = new List<Node>();
        this.Parent = parentNode;
        if(parentNode != null)
        {
            parentNode.AddChild(this);
        }
    }

    private void AddChild(Node node) { childtenNodeList.Add(node); }

    private void RemoveChile(Node node) { childtenNodeList.Remove(node); }
}