using System.Collections.Generic;
using NetFrame.Interpolation;
using NetFrame.Utils;
using UnityEngine;

namespace NetFrame.Components
{
    public class NetFrameTransform : MonoBehaviour
    {
        [SerializeField] private Transform transform;

        [SerializeField] private bool positionInterpolate = true;
        [SerializeField] private bool rotationInterpolate = true;

        private SortedList<double, INetworkDataframeTransform> _bufferSnapshots;
        
        private double BufferTime => NetFrameClientSettings.Instance.IntervalSend * NetFrameClientSettings.Instance.bufferTimeMultiplier;
        
        private float _lastSendTime;
        
        private double _localTimeline;
        private double _localTimescale = 1;

        private void Awake()
        {
            _bufferSnapshots = new SortedList<double, INetworkDataframeTransform>();
        }

        private void Update()
        {
            var unscaledDeltaTime = Time.unscaledDeltaTime;

            if (_bufferSnapshots.Count > 0)
            {
                //Шаг интерполяции
                //TODO может быть можно применить StepInterpolation разобраться в чем отличие???
                SnapshotInterpolation.Step(
                    _bufferSnapshots,
                    unscaledDeltaTime,
                    ref _localTimeline,
                    _localTimescale,
                    out INetworkDataframeTransform fromSnapshot,
                    out INetworkDataframeTransform toSnapshot,
                    out double time);
                
                transform.position = Vector3.LerpUnclamped(fromSnapshot.Position, toSnapshot.Position, (float)time);
                transform.rotation = Quaternion.SlerpUnclamped(fromSnapshot.Rotation, toSnapshot.Rotation, (float)time);
            }
        }
        
        public void AddNetworkDataframeTransform(INetworkDataframeTransform dataframeSnapshots)
        {
            dataframeSnapshots.LocalTime = NetworkTime.LocalTime;

            if (NetFrameClientSettings.Instance == null) //todo костыль!!!
            {
                return;
            }

            if (NetFrameClientSettings.Instance.dynamicAdjustment)
            {
                NetFrameClientSettings.Instance.bufferTimeMultiplier = SnapshotInterpolation.DynamicAdjustment(
                    NetFrameClientSettings.Instance.IntervalSend,
                    NetFrameClientSettings.Instance.DeliveryTimeEma.StandardDeviation,
                    NetFrameClientSettings.Instance.dynamicAdjustmentTolerance
                );
            }
            
            SnapshotInterpolation.InsertAndAdjust(
                _bufferSnapshots,
                dataframeSnapshots,
                ref _localTimeline,
                ref _localTimescale,
                NetFrameClientSettings.Instance.IntervalSend,
                BufferTime,
                NetFrameClientSettings.Instance.catchupSpeed,
                NetFrameClientSettings.Instance.slowdownSpeed,
                ref NetFrameClientSettings.Instance.DriftEma,
                NetFrameClientSettings.Instance.catchupNegativeThreshold,
                NetFrameClientSettings.Instance.catchupPositiveThreshold,
                ref NetFrameClientSettings.Instance.DeliveryTimeEma);
        }
    }
}