using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LvlGen.Scripts;
using DG.Tweening;

public class SpawnManager : MonoBehaviour
{
    [SerializeField] private GameObject playerModel;
    [SerializeField] private GameObject mummyModel;
    [SerializeField] private Shader dissolveShader;
    private Quaternion[] rotations = { Quaternion.Euler(0, 180, 0), Quaternion.Euler(0, 90, 0), Quaternion.Euler(0, 270, 0) };

    public GameObject SpawnCharacter()
    {
        GameObject playerObj = Instantiate(playerModel, Vector3.zero, Quaternion.Euler(0, 90, 0));

        return playerObj;
    }

    public GameObject SpawnMummy(Vector3 pos)
    {
        int index = Random.Range(0, 2);
        GameObject mummy = Instantiate(mummyModel, pos, rotations[index]);
        Renderer mummyRend = mummy.transform.Find("mummy_default").GetComponent<Renderer>();
        ParticleSystem sandstorm = mummy.transform.Find("Sandstorm").GetComponent<ParticleSystem>();
        sandstorm.Play();
        mummyRend.material.DOFloat(0, "_Weight", 4.5f).OnComplete(() => { 
            mummyRend.material.shader = Shader.Find("Standard");
            CanvasGroup cg = mummy.transform.Find("Canvas").GetComponent<CanvasGroup>();
            cg.DOFade(1, 1);
        });
        return mummy;
    }

    public GameObject DespawnMummy(GameObject mummy)
    {
        Renderer mummyRend = mummy.transform.Find("mummy_default").GetComponent<Renderer>();
        CanvasGroup cg = mummy.transform.Find("Canvas").GetComponent<CanvasGroup>();
        cg.DOFade(0, 2);
        mummyRend.material.shader = dissolveShader;
        mummyRend.material.DOFloat(1, "_Weight", 2.0f).OnComplete(() => {
            Destroy(mummy);
        });
        return mummy;
    }
}
