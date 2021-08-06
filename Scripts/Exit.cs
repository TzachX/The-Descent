using UnityEngine;

public class Exit : MonoBehaviour
{
    [SerializeField] private int row;
    [SerializeField] private int col;

    public int Row { get => row; }
    public int Col { get => col; }
}
