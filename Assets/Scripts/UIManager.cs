using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField] private Image playerHp;
    [SerializeField] private Toggle attackToggle;
    [SerializeField] private Button healBtn;
    [SerializeField] private Button skipBtn;

    public Image PlayerHp { get => playerHp; set => playerHp = value; }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
    }
}
