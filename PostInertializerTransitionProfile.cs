using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using static Pd.InertiaAlgorithm;

namespace Pd
{
    public class PostInertializerTransitionProfile : ScriptableObject
    {
        public struct TransformConfig {
            public PostInertializer.EvaluateSpace Space;
            public float BlendTime;
        }

        public AvatarMask AvatarMask;

        public TransformConfig[] TransformConfigs;

        private int[] IndexMapping;

    }
}
