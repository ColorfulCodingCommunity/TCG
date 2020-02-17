using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardController : MonoBehaviour
{
    [SerializeField]
    private Texture2D frontCardTexture;

    public CardPlacerController cardPlacerController;

    private Vector3 initialPos;
    private Quaternion initialRot;
    private ArenaManager arenaManager;

    void Start()
    {
        arenaManager = GameObject.Find("SceneAssets/Arena").GetComponent<ArenaManager>();

        transform.Find("CardMesh").GetComponent<MeshRenderer>().materials[1].mainTexture = frontCardTexture;
        initialPos = transform.position;
        initialRot = transform.rotation;
    }

    [ContextMenu("OnGrabEnd")]
    public void OnGrabEnd()
    {
        GameObject location = cardPlacerController.currentHoveredPlace;

        bool isPlaced = cardPlacerController.TryPlaceCard(frontCardTexture);
        if (!isPlaced)
        {
            transform.position = initialPos;
            transform.rotation = initialRot;
        }
        else
        {
            this.EnableBehavior(location);
            gameObject.SetActive(false);
        }
    }

    private void EnableBehavior(GameObject location)
    {
        arenaManager.SummonCube(location.name);
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Entered " + other.name);
        if(other.transform.parent.name == "CardPlaces")
        {
            cardPlacerController.HoverPlace(other.gameObject);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.transform.parent.name == "CardPlaces")
        {
            cardPlacerController.UnhoverPlace(other.gameObject);
        }
    }
}
