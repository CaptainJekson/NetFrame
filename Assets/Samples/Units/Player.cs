using NetFrame.Components;
using UnityEngine;

namespace Samples.Units
{
    public class Player : MonoBehaviour
    {
        [SerializeField] private NetFrameTransform netFrameTransform;

        public NetFrameTransform NetFrameTransform => netFrameTransform;
    }
}