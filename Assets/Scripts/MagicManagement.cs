using System;
using UnityEngine;
using UnityEngine.UI;

public class MAgicManagement : MonoBehaviour
{
    public Image elementalEmblem;
    public MenuNavigationControl menuNavigationControl;
    public GameObject[] Orbs;
    public int spentOrbs = 0;
    public int numOrbs;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        menuNavigationControl = FindFirstObjectByType<MenuNavigationControl>();
    }

    // Update is called once per frame
    void Update()
    {
        elementalEmblem.sprite = menuNavigationControl.selectedWizard.factionEmblem;
        numOrbs = menuNavigationControl.selectedWizard.loadoutOrbs - spentOrbs; ;

        for (int i = 0; i < Orbs.Length; i++)
        {
            if (i < numOrbs) Orbs[i].SetActive(true);
            else Orbs[i].SetActive(false);
        }
    }
}
