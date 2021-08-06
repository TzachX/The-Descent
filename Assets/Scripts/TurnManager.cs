using LvlGen.Scripts;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;
using TMPro;
using Random = UnityEngine.Random;

public class TurnManager : MonoBehaviour
{
    private bool isPlayerTurn;
    private Player player;
    private MapManager mapManager;
    private SpawnManager spawnManager;
    private bool isInBattle = false;
    private bool isAttacking = false;
    private bool isStartDone = false;
    private Vector3 cameraOffset;
    private Transform currTransform;
    private List<Mummy> mummies;
    [SerializeField] private Material gateMat;
    [SerializeField] private CanvasGroup turnIndicator;
    [SerializeField] private AudioManager audioManager;
    private TMP_Text indText;

    public Player Player { get => player; set => player = value; }
    public bool IsPlayerTurn { get => isPlayerTurn; set => isPlayerTurn = value; }
    public MapManager MapManager { get => mapManager; set => mapManager = value; }
    public SpawnManager SpawnManager { get => spawnManager; set => spawnManager = value; }
    public bool IsAttacking { set => isAttacking = value; }
    public bool IsStartDone { get => isStartDone; set => isStartDone = value; }
    public Vector3 CameraOffset { get => cameraOffset; set => cameraOffset = value; }
    public Transform CurrTransform { get => currTransform; set => currTransform = value; }


    // Start is called before the first frame update
    void Start()
    {
        indText = turnIndicator.GetComponentInChildren<TMP_Text>();
        gateMat.DOFade(0, "_TintColor", 0.01f);
        mummies = new List<Mummy>();

    }

    // Update is called once per frame
    void Update()
    {
        if (isPlayerTurn)
        {
            PlayerAction();
        }
    }

    private void LateUpdate()
    {
        if (isStartDone)
            Camera.main.transform.position = currTransform.position + CameraOffset;
    }

    private IEnumerator EnemyAction()
    {
        isPlayerTurn = false;
        if (mummies.Any())
        {
            for (int i = 0; i < mummies.Count; i++)
            {
                yield return new WaitForSeconds(1f);
                isStartDone = false;
                Camera.main.transform.DOMove(mummies[i].transform.position + cameraOffset, 1f);
                currTransform = mummies[i].transform;
                yield return new WaitForSeconds(2f);
                isStartDone = true;
                if (mummies[i].IsAlive)
                {
                    Tuple<Direction, Direction, bool> data = mapManager.GetBestMove(new Tuple<int, int>(mummies[i].Row, mummies[i].Col), mapManager.PlayerCoords);
                    
                    if (data.Item3)
                    {
                        mummies[i].Attack(data.Item2, player);
                    }
                    else if (data.Item2 != Direction.None)
                    {
                        mapManager.CurrPlayerSection.Tiles[mummies[i].Row].array[mummies[i].Col] = true;
                        mummies[i].Move(data.Item2);
                        switch (data.Item1)
                        {
                            case Direction.Up: mummies[i].Row -= 1; break;
                            case Direction.Down: mummies[i].Row += 1; break;
                            case Direction.Right: mummies[i].Col += 1; break;
                            case Direction.Left: mummies[i].Col -= 1; break;
                        }
                        mapManager.CurrPlayerSection.Tiles[mummies[i].Row].array[mummies[i].Col] = false;

                    }
                    yield return new WaitForSeconds(2f);
                }
                else
                {
                    Mummy deadMummy = mummies[i];
                    mummies.RemoveAt(i);
                    spawnManager.DespawnMummy(deadMummy.gameObject);
                }
            }
        }
        
        if (!mummies.Any())
        {
            EndBattle(mapManager.CurrPlayerSection);
        }

        isStartDone = false;
        Camera.main.transform.DOMove(player.transform.position + cameraOffset, 1f);
        currTransform = player.transform;
        yield return new WaitForSeconds(1f);
        isStartDone = true;
        SwitchTurn(false);

    }

    private void PlayerAction()
    {
        Direction dir = Direction.None;

        if (Input.GetKeyDown(KeyCode.UpArrow)) { dir = Direction.Up; }
        else if (Input.GetKeyDown(KeyCode.DownArrow)) { dir = Direction.Down; }
        else if (Input.GetKeyDown(KeyCode.RightArrow)) { dir = Direction.Right; }
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) { dir = Direction.Left; }

        if (dir != Direction.None && player.IsAvailable) 
        {
            if (isAttacking)
                StartCoroutine(PlayerAttack(dir));
            else
                StartCoroutine(MovePlayer(dir));
        }
    }

    public IEnumerator MovePlayer(Direction dir)
    {
        if (mapManager.CheckPlayerMove(dir))
        {
            player.IsAvailable = false;

            player.Move(dir);

            if (mapManager.IsBattleInit)
            {
                mapManager.ToggleWalls(true);
                InitBattle(mapManager.CurrPlayerSection);
            }
            else if (isInBattle)
            {
                yield return new WaitForSeconds(0.1f);
                SwitchTurn(true);
            }
        }
    }


    private IEnumerator PlayerAttack(Direction dir)
    { 
        player.IsAvailable = false;
        Tuple<Mummy, bool> tup = mapManager.IsMummyInRange(mummies, dir);
        player.Attack(dir, tup.Item1, tup.Item2);
        yield return new WaitForSeconds(2);
        if (tup.Item1 != null)
        {
            if (!tup.Item1.IsAlive)
            {
                mapManager.CurrPlayerSection.Tiles[tup.Item1.Row].array[tup.Item1.Col] = true;
                mummies.Remove(tup.Item1);
                spawnManager.DespawnMummy(tup.Item1.gameObject);
            }
        }
        if (isInBattle)
            SwitchTurn(true);
    }

    public void PlayerHeal()
    {
        if (player.IsAvailable && isPlayerTurn)
        {
            player.IsAvailable = false;
            StartCoroutine(player.Heal(10));
            if (isInBattle)
                SwitchTurn(true);
        }
    }

    public void SwitchTurn(bool isPlayer) 
    {
        if (isPlayer && isPlayerTurn)
        {
            indText.text = "Enemy Turn";
            StartCoroutine(EnemyAction());
        }
        else
        {
            indText.text = "Player Turn";
            isPlayerTurn = true;

        }
    }


    //IEnumerator performs the delay
    IEnumerator delayEnumerator(float newDelayTime)
    {
        isPlayerTurn = false;
        yield return new WaitForSeconds(newDelayTime - 1);
        indText.text = "Player Turn";
        turnIndicator.DOFade(1, 3);
        yield return new WaitForSeconds(1);
        isPlayerTurn = true;
    }

    private void InitBattle(Section currSection)
    {
        isInBattle = true;
        currSection.IsLocked = true;
        bool[][] currSectionMap = MapConvertor(currSection.Tiles);
        int enemyCount = Random.Range(2, currSection.MaxEnemiesAllowed);

        for (int i = 0; i < enemyCount; i++)
        {
            int row = Random.Range(0, currSectionMap.GetLength(0));
            int col = Random.Range(0, currSectionMap[row].Length);

            while (!currSectionMap[row][col])
            {
                row = Random.Range(0, currSectionMap.GetLength(0));
                col = Random.Range(0, currSectionMap[row].Length);
            }

            mapManager.CurrPlayerSection.Tiles[row].array[col] = false;
            Mummy mummy = spawnManager.SpawnMummy(mapManager.FindCoordPos(row, col)).GetComponent<Mummy>();
            mummy.Row = row;
            mummy.Col = col;
            mummies.Add(mummy);
        }

        gateMat.DOFade(1, "_TintColor", 1);
        audioManager.FadeMusic(true);
        StartCoroutine(delayEnumerator(5.5f));
    }

    private void EndBattle(Section currSection)
    {
        isInBattle = false;
        currSection.IsLocked = false;
        currSection.IsCleared = true;
        gateMat.DOFade(0, "_TintColor", 1);
        audioManager.FadeMusic(false);
        turnIndicator.DOFade(0, 3);
    }


    private bool[][] MapConvertor(Rows[] tiles)
    {
        bool[][] map = new bool[tiles.Length][];

        for (int i = 0; i < tiles.Length; i++)
        {
            map[i] = tiles[i].array;
        }

        return map;
    }

    public void ButtonMovePlayer(int dir)
    {
        if (isAttacking)
        {
            StartCoroutine(PlayerAttack((Direction)dir));
        }
        else
        {
            StartCoroutine(MovePlayer((Direction)dir));
        }
    }
}
