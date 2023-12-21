using System.Collections.Generic;
using Mirror;
using NetFrame.Interpolation;
using NetFrame.Utils;
using UnityEngine;

namespace NetFrame.Components
{
    public class NetFrameTransform : MonoBehaviour
    {
        [SerializeField] private Transform transform;
        
        [Header("Times")] 
        [SerializeField] private int frequencySend = 30; //частота отправки
        
        [Header("Snapshot Interpolation settings")] //todo надо вытащить в другое место
        [SerializeField] private SnapshotInterpolationSettings snapshotSettings = new SnapshotInterpolationSettings();
        
        private float IntervalSend => 1.0f / frequencySend; //интервал отправки
        
        private SortedList<double, INetworkDataframeTransform> _bufferSnapshots;
        
        private double BufferTime => IntervalSend * snapshotSettings.bufferTimeMultiplier;
        
        private float _lastSendTime;
        
        private double _localTimeline;
        private double _localTimescale = 1;
        
        private ExponentialMovingAverage _driftEma;
        private ExponentialMovingAverage _deliveryTimeEma;

        private void Awake()
        {
            _driftEma = new ExponentialMovingAverage(frequencySend * snapshotSettings.driftEmaDuration);
            _deliveryTimeEma = new ExponentialMovingAverage(frequencySend * snapshotSettings.deliveryTimeEmaDuration);
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

                // Выполнить интерполяцию
                transform.position = Vector3.LerpUnclamped(fromSnapshot.Position, toSnapshot.Position, (float)time);
                transform.rotation = Quaternion.SlerpUnclamped(fromSnapshot.Rotation, toSnapshot.Rotation, (float)time);
            }
        }
        
        public void AddNetworkDataframeTransform(INetworkDataframeTransform dataframeSnapshots)
        {
            dataframeSnapshots.LocalTime = NetworkTime.LocalTime;
            
            SnapshotInterpolation.InsertAndAdjust(
                _bufferSnapshots,
                dataframeSnapshots,
                ref _localTimeline,
                ref _localTimescale,
                IntervalSend,
                BufferTime,
                snapshotSettings.catchupSpeed,
                snapshotSettings.slowdownSpeed,
                ref _driftEma,
                snapshotSettings.catchupNegativeThreshold,
                snapshotSettings.catchupPositiveThreshold,
                ref _deliveryTimeEma);
        }
    }
}