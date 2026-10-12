using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Mummy : Character
{
    private int row;
    private int col;

    public int Row { get => row; set => row = value; }
    public int Col { get => col; set => col = value; }

    // Start is called before the first frame update
    void Start()
    {
        MAX_HP = Hp = 15;
        hitp = 4;
        animTime = 2f;
    }

    public new void Move(Direction dir)
    {
        base.Move(dir);
    }

    public new void Attack(Direction dir, Character target)
    {
        base.Attack(dir, target, true);
    }    

}
