using OculusSampleFramework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace com.colorfulcoding.customVRLogic{
    public class FingerTipPokeInteraction : MonoBehaviour
    {
        public bool isRightHanded = true;
        public TextMesh debugText;
        // Start is called before the first frame update
        void Start()
        {
            StartCoroutine(AttachTriggerLogic());
        }

        private IEnumerator AttachTriggerLogic()
        {
            while(!HandsManager.Instance || !HandsManager.Instance.IsInitialized())
            {
                yield return null;
            }

            OVRSkeleton handSkeleton = isRightHanded ? HandsManager.Instance.RightHandSkeleton : HandsManager.Instance.LeftHandSkeleton;
            OVRSkeleton.BoneId bone = OVRSkeleton.BoneId.Hand_Index3;
            
            List<OVRBoneCapsule> boneCapsules = HandsManager.GetCapsulesPerBone(handSkeleton, bone);

            foreach (var ovrCapsuleInfo in boneCapsules)
            {
                debugText.text = "Adding BTL to " + ovrCapsuleInfo.CapsuleCollider.gameObject.name;
                ovrCapsuleInfo.CapsuleCollider.gameObject.AddComponent<BoneTriggerLogic>();
                ovrCapsuleInfo.CapsuleCollider.isTrigger = true;
            }
        }
    }
}
