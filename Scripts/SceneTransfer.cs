using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransfer : MonoBehaviour
{
    // Update is called once per frame
    public void LoadGame()
    {
        SceneManager.LoadScene("MainScene", LoadSceneMode.Single);
    }

    public void ExitGame()
    {
        Application.Quit();
    }
}
