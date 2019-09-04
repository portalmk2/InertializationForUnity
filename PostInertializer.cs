using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using static Pd.InertiaAlgorithm;

namespace Pd {

    [RequireComponent(typeof(Animator))]
    public class PostInertializer : MonoBehaviour
    {
        public enum EvaluateSpace {
            Local,
            Character,
            World
        }


        struct TransformState
        {
            public float3 PrevPosition;
            public float3 Position;

            public quaternion PrevRotation;
            public quaternion Rotation;

            public EvaluateSpace Space;
            public float BeginTime;
            public float EndTime;
        }


        private Animator _Animator;
        public Animator Animator {
            get {
                if (_Animator == null)
                    _Animator = GetComponent<Animator>();
                return _Animator;
            }
        }


        public AvatarMask AvatarMask;

        public float BlendTime = 0.5f;


        #region AvatarMaskBodyPart Mapping
        public static readonly Dictionary<AvatarMaskBodyPart, HumanBodyBones[]> AvatarBodyPartMapping = new Dictionary<AvatarMaskBodyPart, HumanBodyBones[]>() {
            { AvatarMaskBodyPart.Body, new HumanBodyBones[]{
                HumanBodyBones.Chest,
                HumanBodyBones.Hips,
                HumanBodyBones.Spine,
                HumanBodyBones.UpperChest } },
            { AvatarMaskBodyPart.Head, new HumanBodyBones[]{
                HumanBodyBones.Head,
                HumanBodyBones.Neck,
                HumanBodyBones.LeftEye,
                HumanBodyBones.RightEye,
                HumanBodyBones.Jaw } },
            { AvatarMaskBodyPart.LeftArm, new HumanBodyBones[]{
                HumanBodyBones.LeftUpperArm,
                HumanBodyBones.LeftLowerArm,
                HumanBodyBones.LeftHand} },
            { AvatarMaskBodyPart.RightArm, new HumanBodyBones[]{
                HumanBodyBones.RightUpperArm,
                HumanBodyBones.RightLowerArm,
                HumanBodyBones.RightHand} },
            { AvatarMaskBodyPart.LeftLeg, new HumanBodyBones[]{
                HumanBodyBones.LeftUpperLeg,
                HumanBodyBones.LeftLowerLeg,
                HumanBodyBones.LeftFoot,
                HumanBodyBones.LeftToes } },
            { AvatarMaskBodyPart.RightLeg, new HumanBodyBones[]{
                HumanBodyBones.RightUpperLeg,
                HumanBodyBones.RightLowerLeg,
                HumanBodyBones.RightFoot,
                HumanBodyBones.RightToes } },
            { AvatarMaskBodyPart.LeftFingers, new HumanBodyBones[] {
                HumanBodyBones.LeftThumbProximal,
                HumanBodyBones.LeftThumbIntermediate,
                HumanBodyBones.LeftThumbDistal,
                HumanBodyBones.LeftIndexProximal,
                HumanBodyBones.LeftIndexIntermediate,
                HumanBodyBones.LeftIndexDistal,
                HumanBodyBones.LeftMiddleProximal,
                HumanBodyBones.LeftMiddleIntermediate,
                HumanBodyBones.LeftMiddleDistal,
                HumanBodyBones.LeftRingProximal,
                HumanBodyBones.LeftRingIntermediate,
                HumanBodyBones.LeftRingDistal,
                HumanBodyBones.LeftLittleProximal,
                HumanBodyBones.LeftLittleIntermediate,
                HumanBodyBones.LeftLittleDistal } },
            { AvatarMaskBodyPart.RightFingers, new HumanBodyBones[] {
                HumanBodyBones.RightThumbProximal,
                HumanBodyBones.RightThumbIntermediate,
                HumanBodyBones.RightThumbDistal,
                HumanBodyBones.RightIndexProximal,
                HumanBodyBones.RightIndexIntermediate,
                HumanBodyBones.RightIndexDistal,
                HumanBodyBones.RightMiddleProximal,
                HumanBodyBones.RightMiddleIntermediate,
                HumanBodyBones.RightMiddleDistal,
                HumanBodyBones.RightRingProximal,
                HumanBodyBones.RightRingIntermediate,
                HumanBodyBones.RightRingDistal,
                HumanBodyBones.RightLittleProximal,
                HumanBodyBones.RightLittleIntermediate,
                HumanBodyBones.RightLittleDistal } },
        };
        #endregion

        Transform[] CollectTransforms() {
            HashSet<Transform> xforms = new HashSet<Transform>();

            if (AvatarMask != null) {
                for (int i = 0; i < AvatarMask.transformCount; ++i) {
                    string path = AvatarMask.GetTransformPath(i);
                    var xform = transform.Find(path);
                    if (xform != null)
                        xforms.Add(xform);
                }
            }

            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; ++i) {
                bool active = AvatarMask == null ? true : AvatarMask.GetHumanoidBodyPartActive((AvatarMaskBodyPart)i);

                HumanBodyBones[] hbbones;
                if (!AvatarBodyPartMapping.TryGetValue((AvatarMaskBodyPart)i, out hbbones))
                    continue;
                foreach (var hbb in hbbones) {
                    var xform = Animator.GetBoneTransform(hbb);
                    if (xform == null)
                        continue;

                    if (active)
                        xforms.Add(xform);
                    else
                        xforms.Remove(xform);
                }
            }

            xforms.Remove(transform);

            return Sym.TransformUtil.SortParentToChild(xforms);
        }


        class InertiaState
        {
            public Transform[] Transforms;
            public TransformState[] States;

            public float CurrTime;
            public float DeltaTime;

            public void Trigger(float blendTime) {
                for (int i = 0; i < Transforms.Length; ++i) {
                    Trigger(i, EvaluateSpace.Local, blendTime);
                }   
            }

            public void Trigger(int index, EvaluateSpace space, float blendTime) {
                States[index].BeginTime = Time.time;
                States[index].EndTime = States[index].BeginTime + blendTime;
                States[index].Space = EvaluateSpace.Local;
            }

            public void Update() {
                float dt = max(1 / 120.0f, DeltaTime);
                
                for (int i = 0; i < Transforms.Length; ++i) {
                    TransformState state = States[i];

                    float3 targetPos = Transforms[i].localPosition;
                    quaternion targetRot = Transforms[i].localRotation;


                    float tf = max(0.0001f, state.EndTime - CurrTime);
                    float3 pos = InertializeMagnitude(state.PrevPosition, state.Position, targetPos, dt, tf, dt);
                    quaternion rot = InertializeMagnitude(state.PrevRotation, state.Rotation, targetRot, dt, tf, dt);


                    state.PrevPosition = state.Position;
                    state.PrevRotation = state.Rotation;
                    state.Position = pos;
                    state.Rotation = rot;

                    States[i] = state;

                    Transforms[i].localPosition = pos;
                    Transforms[i].localRotation = rot;
                }
            }
        }

        InertiaState PostInertia = null;

        void InitializePostProcess() {
            PostInertia = new InertiaState();
            PostInertia.Transforms = CollectTransforms();

            PostInertia.States = (from pt
                                   in PostInertia.Transforms
                                   select new TransformState
                                   {
                                       Position = pt.localPosition,
                                       PrevPosition = pt.localPosition,
                                       Rotation = pt.localRotation,
                                       PrevRotation = pt.localRotation
                                   }).ToArray();


            

        }

        private void LateUpdate()
        {
            if (PostInertia == null)
                InitializePostProcess();

            if (PostInertia != null) {
                PostInertia.CurrTime = Time.time;
                PostInertia.DeltaTime = Time.deltaTime;
                PostInertia.Update();
            }
        }
		
		public void Trigger(float blendTime){
			PostInertia?.Trigger(blendTime);
		}

    }


}
