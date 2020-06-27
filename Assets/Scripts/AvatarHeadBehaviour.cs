using Photon.Pun;
using Photon.Voice.Unity;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class AvatarHeadBehaviour : MonoBehaviour
{
    public GameObject bodyAvatar;
    public GameObject audioIcon;

    private PhotonView photonView;
    private AudioSource audioSource;

    private float updateStep = 0.1f;
    private int sampleDataLength = 1024;
    private float currentUpdateTime = 0f;
    private float[] clipSampleData;

    private void Start()
    {
        photonView = GetComponent<PhotonView>();

        /*
        Debug.Log("My id: " + photonView.Owner.UserId);
        clipSampleData = new float[sampleDataLength];
        StartCoroutine(TryGetAudioSpeaker());
        */
        if (photonView.IsMine)
        {
            GetComponent<BoxCollider>().enabled = false;
            transform.Find("avt_head").gameObject.SetActive(false);
        }
    }

    void Update()
    {
        var parentRot = transform.rotation.eulerAngles;
        bodyAvatar.transform.rotation = Quaternion.Euler(new Vector3(0, parentRot.y, 0));
        audioIcon.transform.rotation = Quaternion.Euler(new Vector3(0, parentRot.y, 0));

       /* if(audioSource != null)
        {
            currentUpdateTime += Time.deltaTime;
            if (currentUpdateTime >= updateStep)
            {
                currentUpdateTime = 0f;
                UpdateAudioState();
            }
        }*/
    }


    void UpdateAudioState()
    {
        audioSource.clip.GetData(clipSampleData, audioSource.timeSamples);
        float clipLoudness = 0;
        foreach(float sample in clipSampleData)
        {
            clipLoudness += Mathf.Abs(sample);
        }
        clipLoudness /= sampleDataLength;

        if(clipLoudness > 0.005 && !audioIcon.activeSelf)
        {
            audioIcon.SetActive(true);
        }
        else if(clipLoudness < 0.005 && audioIcon.activeSelf)
        {
            audioIcon.SetActive(false);
        }
    }

    IEnumerator TryGetAudioSpeaker()
    {
        while(audioSource == null)
        {
            var go = GameObject.Find("RemoteVoices/" + photonView.Owner.NickName);
            if (go != null)
            {
                audioSource = go.GetComponent<AudioSource>();
            }
            yield return new WaitForSeconds(2);
        }
    }
}
