using System.Collections;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;

public enum Direction
{
    Up,
    Down,
    Left,
    Right,
    None
}

public class Character : MonoBehaviour
{
    protected const float TILE_SIZE = 2.5f;
    protected Transform transform;
    protected Animator animator;
    protected bool isAvailable = true;
    protected float animTime;
    protected int MAX_HP;
    private int hp;
    protected int hitp;
    private bool isAlive = true;
    private AudioSource audioSource;
    [SerializeField] private Image hpbar;
    [SerializeField] protected AudioClip audioMove;
    [SerializeField] protected AudioClip audioAttackFar;
    [SerializeField] protected AudioClip audioAttackNear;
    [SerializeField] protected AudioClip audioHurt;
    [SerializeField] protected AudioClip audioDead;


    public bool IsAvailable { get => isAvailable; set => isAvailable = value; }
    public Image Hpbar { get => hpbar; set => hpbar = value; }
    public bool IsAlive { get => isAlive; set => isAlive = value; }
    public int Hp { get => hp; set => hp = value; }



    // Start is called before the first frame update
    void Awake()
    {
        transform = this.gameObject.transform;
        animator = this.gameObject.GetComponent<Animator>();
        audioSource = this.gameObject.GetComponent<AudioSource>();
    }


    // Used for heal and damage
    protected IEnumerator ChangeHP(int amount, bool isHeal) 
    {
        if (!isHeal) 
        {
            yield return new WaitForSeconds(0.8f);
            amount = Hp - amount < 0 ? -Hp : -amount;
            StartCoroutine(SmoothHurt(Hp + amount));
        }
        else
        {
            amount = Hp + amount > MAX_HP ? MAX_HP - Hp : amount;
        }
        
        hpbar.DOFillAmount((float)(Hp + amount) / (float)MAX_HP, 0.5f);
        Hp += amount;
    }


    protected void Move(Direction dir)
    {
        Vector3 moveDir = Vector3.zero;

        switch (dir)
        {
            case Direction.Up:
                transform.rotation = Quaternion.Euler(0, 90, 0);
                moveDir = new Vector3(TILE_SIZE, 0, 0);
                break;
            case Direction.Down:
                transform.rotation = Quaternion.Euler(0, 270, 0);
                moveDir = new Vector3(-TILE_SIZE, 0, 0);
                break;
            case Direction.Right:
                transform.rotation = Quaternion.Euler(0, 180, 0);
                moveDir = new Vector3(0, 0, -TILE_SIZE);
                break;
            case Direction.Left:
                transform.rotation = Quaternion.Euler(0, 0, 0);
                moveDir = new Vector3(0, 0, TILE_SIZE);
                break;
            default:
                break;
        }

        Vector3 endPos = transform.position + moveDir;
        StartCoroutine(SmoothMovement(endPos));
    }


    protected void Attack(Direction dir, Character target, bool isNear)
    {
        switch (dir)
        {
            case Direction.Up:
                transform.rotation = Quaternion.Euler(0, 90, 0);
                break;
            case Direction.Down:
                transform.rotation = Quaternion.Euler(0, 270, 0);
                break;
            case Direction.Right:
                transform.rotation = Quaternion.Euler(0, 180, 0);
                break;
            case Direction.Left:
                transform.rotation = Quaternion.Euler(0, 0, 0);
                break;
            default:
                break;
        }

        StartCoroutine(SmoothAttack(isNear));

        if (target != null)
        {
            StartCoroutine(target.ChangeHP(hitp, false));
        }
    }


    IEnumerator SmoothHurt(int currHp)
    {
        isAvailable = false;
        if (currHp > 0)
        {
            animator.SetTrigger("Hurt");
            audioSource.clip = audioHurt;
            audioSource.Play();
            yield return new WaitForSeconds(1.2f);
            isAvailable = true;
        }
        else
        {
            isAlive = false;
            animator.SetBool("IsAlive", isAlive);
            audioSource.clip = audioDead;
            audioSource.Play();
            yield return new WaitForSeconds(1.2f);
        }
    }


    IEnumerator SmoothAttack(bool isNear)
    {
        if (isNear)
        {
            animator.SetTrigger("Attack");
        }
        else
        {
            audioSource.clip = audioAttackFar;
            audioSource.Play();
            animator.SetTrigger("Shoot");
        }

        yield return new WaitForSeconds(1f);
        audioSource.clip = audioAttackNear;
        audioSource.Play();
        yield return new WaitForSeconds(1f);
        // Add bullet
        isAvailable = true;
    }

    IEnumerator SmoothMovement(Vector3 endPos)
    {
        DOTween.Init();
        Tween myTween = transform.DOMove(endPos, animTime).SetEase(Ease.Linear).OnComplete(() => { animator.SetBool("Walk", false); isAvailable = true; }); ;
        audioSource.clip = audioMove;
        audioSource.Play();
        animator.SetBool("Walk", true);
        yield return myTween.WaitForCompletion();
        // This will happen after the tween has completed
        
    }
}
