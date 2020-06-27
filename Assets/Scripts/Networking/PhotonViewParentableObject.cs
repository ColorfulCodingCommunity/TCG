using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class PhotonViewParentableObject : MonoBehaviour
{
    [SerializeField]
    private string parentPath_vr = "";

    [SerializeField]
    private string alternativePath = "";
    
    void Start()
    {
        ParentObject(parentPath_vr);
    }

    public void ParentObject(string path)
    {
        var photonView = GetComponent<PhotonView>();
        var parent = GameObject.Find(photonView.IsMine ? path : alternativePath);
        

        Assert.IsNotNull(parent, "Photon view " + gameObject.name + " has an invalid parent gameobject path " + path);

        transform.parent = parent.transform;
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }
}
