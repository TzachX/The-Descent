using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LvlGen.Scripts;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [SerializeField] private LevelGenerator levelGenerator;
    [SerializeField] private SpawnManager   spawnManager;
    [SerializeField] private MapManager     mapManager;
    [SerializeField] private TurnManager    turnManager;
    [SerializeField] private UIManager      uiManager;
    
    private Player player;

    private void Awake()
    {
        levelGenerator.StartGeneration();
    }
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(waitForGen());
    }

    IEnumerator waitForGen()
    {
        yield return new WaitForSeconds(0.1f);
        player = spawnManager.SpawnCharacter().GetComponent<Player>();
        
        mapManager.AddPlayerToSpawnRoom(levelGenerator.GetSpawnRoom());
        player.Hpbar = uiManager.PlayerHp;
        turnManager.Player = player;
        turnManager.CurrTransform = player.transform;
        turnManager.CameraOffset = Camera.main.transform.position - player.transform.position;
        turnManager.IsPlayerTurn = true;
        turnManager.MapManager = mapManager;
        turnManager.SpawnManager = spawnManager;
        turnManager.IsStartDone = true;
    }

    public void LoadMenu()
    {
        SceneManager.LoadScene("MenuScene", LoadSceneMode.Single);
    }

    public void Pause(bool isPause)
    {
        if (isPause)
        {
            Time.timeScale = 0;
        }
        else
        {
            Time.timeScale = 1;
        }
    }
}
