using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : Character
{
    [SerializeField] private GameObject pistol;
    [SerializeField] private GameObject potion;

    private void Start()
    {
        MAX_HP = Hp = 50;
        hitp = 5;
        animTime = 0.75f;
    }

    public new void Attack(Direction dir, Character target, bool isNear)
    {
        base.Attack(dir, target, isNear);
    }

    public new void Move(Direction dir)
    {
        base.Move(dir);
    }

    public IEnumerator Heal(int amount)
    {
        pistol.SetActive(false);
        potion.SetActive(true);
        isAvailable = false;
        animator.SetTrigger("Heal");
        StartCoroutine(base.ChangeHP(amount, true));
        yield return new WaitForSeconds(1.2f);
        pistol.SetActive(true);
        potion.SetActive(false);
        isAvailable = true;
    }
}
