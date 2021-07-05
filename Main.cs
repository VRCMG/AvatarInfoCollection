using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using AmplitudeSDKWrapper;
using MelonLoader;
using UnityEngine;
using VRC;
using VRC.Core;

[assembly: MelonGame("VRChat", "VRChat")]
[assembly: MelonInfo(typeof(AvatarInfoCollection.InfoCollecter), "AvatarInfoCollection", "1", "dltdat/charlesdeep")]

namespace AvatarInfoCollection
{
    public class InfoCollecter : MelonMod
    {

        struct AvatarInfo
        {
            public Vector3 size;
            public float handColliderRadius, handColliderHeight;
            public float breastBoneRadius;
        }

        private static InfoCollecter _Instance;
        private Dictionary<string, AvatarInfo> collectedInfo = new Dictionary<string, AvatarInfo>();
        private List<string> alreadyCollectedAvatars = new List<string>();

        private delegate void AvatarInstantiatedDelegate(IntPtr @this, IntPtr avatarPtr, IntPtr avatarDescriptorPtr, bool loaded);
        private static AvatarInstantiatedDelegate onAvatarInstantiatedDelegate;

        private static void Hook(IntPtr target, IntPtr detour)
        {
            Imports.Hook(target, detour);
        }

        public override void OnApplicationStart()
        {
            _Instance = this;
            HookCallbackFunctions();
            LoadList();
        }

        private void LoadList()
        {
            if (File.Exists("avatar_info.list")) alreadyCollectedAvatars.AddRange(File.ReadAllLines("avatar_info.list").Skip(1).Select(l => l.Substring(0, l.IndexOf(';'))));
        }

        private unsafe void HookCallbackFunctions()
        {

            IntPtr funcToHook = (IntPtr)typeof(VRCAvatarManager.MulticastDelegateNPublicSealedVoGaVRBoUnique).GetField("NativeMethodInfoPtr_Invoke_Public_Virtual_New_Void_GameObject_VRC_AvatarDescriptor_Boolean_0", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null);

            Hook(funcToHook, new System.Action<IntPtr, IntPtr, IntPtr, bool>(OnAvatarInstantiated).Method.MethodHandle.GetFunctionPointer());
            onAvatarInstantiatedDelegate = Marshal.GetDelegateForFunctionPointer<AvatarInstantiatedDelegate>(*(IntPtr*)funcToHook);
            MelonLogger.Log(ConsoleColor.Blue, $"Hooked OnAvatarInstantiated? {((onAvatarInstantiatedDelegate != null) ? "Yes!" : "No: critical error!!")}");
        }

        private static void OnAvatarInstantiated(IntPtr @this, IntPtr avatarPtr, IntPtr avatarDescriptorPtr, bool loaded)
        {
            onAvatarInstantiatedDelegate(@this, avatarPtr, avatarDescriptorPtr, loaded);

            try
            {
                if (loaded)
                {
                    GameObject avatar = new GameObject(avatarPtr);
                    if (!_Instance.collectedInfo.ContainsKey(avatar.transform.root.GetComponentInChildren<VRCPlayer>().prop_ApiAvatar_0.id) && !_Instance.alreadyCollectedAvatars.Contains(avatar.transform.root.GetComponentInChildren<VRCPlayer>().prop_ApiAvatar_0.id))
                    {
                        AvatarInfo av = new AvatarInfo();
                        DynamicBoneCollider[] dbc = avatar.transform.root.GetComponentInChildren<VRCPlayer>().field_Internal_Animator_0.GetBoneTransform(HumanBodyBones.LeftHand).GetComponentsInChildren<DynamicBoneCollider>();
                        if (dbc != null && dbc.Length == 1)
                        {
                            av.handColliderRadius = dbc[0]?.m_Radius ?? 0f;
                            av.handColliderHeight = dbc[0]?.m_Height ?? 0f;
                        }
                        av.size = avatar.transform.root.GetComponentInChildren<VRCPlayer>().prop_VRCAvatarManager_0.prop_AvatarPerformanceStats_0.field_Public_Nullable_1_Bounds_0.Value.size;

                        Transform chest = avatar.transform.root.GetComponentInChildren<VRCPlayer>().field_Internal_Animator_0.GetBoneTransform(HumanBodyBones.Chest);
                        av.breastBoneRadius = chest.GetComponentsInChildren<DynamicBone>(true)?.FirstOrDefault(db => db.gameObject.name.ToLowerInvariant().Contains("breast"))?.m_Radius ?? chest.GetComponentInChildren<DynamicBone>(true)?.m_Radius ?? 0f;
                        
                        if (av.size.y > 3f || (av.breastBoneRadius == 0f && av.handColliderRadius == 0f && av.handColliderHeight == 0f)) return;
                        
                        _Instance.collectedInfo.Add(avatar.transform.root.GetComponentInChildren<VRCPlayer>().prop_ApiAvatar_0.id, av);
                        MelonLogger.Log(ConsoleColor.Green, $"Added {avatar.transform.root.GetComponentInChildren<VRCPlayer>().nameplate.field_Private_String_0}'s '{ avatar.transform.root.GetComponentInChildren<VRCPlayer>().prop_ApiAvatar_0.id}' avatar info to dict");
                    }
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Log(ConsoleColor.Red ,ex.ToString());
            }
        }

        public override void OnApplicationQuit()
        {
            if (!File.Exists("avatar_info.list"))
            {
                File.WriteAllText("avatar_info.list", "avatar_id;bounds_size_x;bounds_size_y;bounds_size_z;hand_collider_radius;hand_collider_height;breast size\n");
            }
            File.AppendAllLines("avatar_info.list", collectedInfo.Select(i => new StringBuilder().AppendFormat("{0};{1};{2};{3};{4};{5};{6}", i.Key, i.Value.size.x.ToString("F5"), i.Value.size.y.ToString("F5"), i.Value.size.z.ToString("F5"), i.Value.handColliderRadius.ToString("F5"), i.Value.handColliderHeight.ToString("F5"), i.Value.breastBoneRadius.ToString("F5")).ToString()));
        }

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.I))
            {
                File.WriteAllLines("pokePhrases.txt", Resources.FindObjectsOfTypeAll<VRCPlusThankYou>().First().pokePhrases.ToArray());
                File.WriteAllLines("normalPhrases.txt", Resources.FindObjectsOfTypeAll<VRCPlusThankYou>().First().normalPhrases.ToArray());
            }
        }
    }
}
