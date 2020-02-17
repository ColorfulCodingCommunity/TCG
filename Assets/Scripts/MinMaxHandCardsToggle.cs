using com.colorfulcoding.customVRLogic;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MinMaxHandCardsToggle : MonoBehaviour
{
    [SerializeField]
    private Material activeMat;
    [SerializeField]
    private Material holdMat;
    [SerializeField]
    private Material inactiveMat;

    [SerializeField]
    private GameObject maximizedCards;

    private bool areCardsShown = true;

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<BoneTriggerLogic>() != null)
        {
            Debug.Log("Entered hover Trigger " + other.name);
            GetComponent<MeshRenderer>().material = holdMat;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<BoneTriggerLogic>() != null)
        {
            GetComponent<MeshRenderer>().material = areCardsShown ? activeMat : inactiveMat;
        }
    }

    public void SwitchShownCards()
    {
        areCardsShown = !areCardsShown;
        if (areCardsShown)
        {
            GetComponent<MeshRenderer>().material = activeMat;
            maximizedCards.SetActive(true);
        }
        else
        {
            maximizedCards.SetActive(false);
            GetComponent<MeshRenderer>().material = inactiveMat;
        }
    }
}
