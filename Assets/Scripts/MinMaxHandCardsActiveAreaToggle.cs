using com.colorfulcoding.customVRLogic;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MinMaxHandCardsActiveAreaToggle : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<BoneTriggerLogic>() != null)
        {
            Debug.Log("Entered Trigger " + other.name);
            transform.parent.GetComponent<MinMaxHandCardsToggle>().SwitchShownCards();
        }
    }
}
