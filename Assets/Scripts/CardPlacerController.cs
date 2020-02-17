using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardPlacerController : MonoBehaviour
{
    [SerializeField]
    private GameObject hoverObject;

    [SerializeField]
    private GameObject placedCardPrefab;

    [SerializeField]
    private GameObject placedCardsParent;

    [HideInInspector]
    public GameObject currentHoveredPlace;

    public bool TryPlaceCard(Texture2D cardTex)
    {
        if (currentHoveredPlace == null) return false;

        var go = Instantiate(placedCardPrefab, placedCardsParent.transform);
        go.transform.position = currentHoveredPlace.transform.position - new Vector3(0, 0.02f, 0);
        go.transform.Find("CardMesh").GetComponent<Renderer>().materials[1].mainTexture = cardTex;

        this.UnhoverPlace(currentHoveredPlace);
        return true;
    }

    public void HoverPlace(GameObject hoveredArea)
    {
        hoverObject.SetActive(true);
        hoverObject.transform.position = hoveredArea.transform.position;
        currentHoveredPlace = hoveredArea;
    }

    public void UnhoverPlace(GameObject unhoveredArea)
    {
        if(currentHoveredPlace == unhoveredArea)
        {
            currentHoveredPlace = null;
            hoverObject.SetActive(false);
        }
    }

    public void SetCard(Texture2D frontCardTexture)
    {
        if (currentHoveredPlace == null) return;


    }
}
