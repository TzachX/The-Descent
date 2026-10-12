using System.Collections;
using UnityEngine;
using DG.Tweening;

public class AudioManager : MonoBehaviour
{
    [SerializeField] private AudioClip levelMusic;
    [SerializeField] private AudioClip battleMusic;
    private AudioSource audioSource;

    // Start is called before the first frame update
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void FadeMusic(bool isBattle)
    {
        audioSource.DOFade(0, 3).OnComplete(() => 
        {
            if (isBattle)
                StartCoroutine(WaitUntilBattleMusic(battleMusic));
            else
                StartCoroutine(WaitUntilBattleMusic(levelMusic));
        });
    }

    private IEnumerator WaitUntilBattleMusic(AudioClip audioClip)
    {
        yield return new WaitForSeconds(2.5f);
        audioSource.clip = audioClip;
        audioSource.volume = 1;
        audioSource.Play();
    }
}
