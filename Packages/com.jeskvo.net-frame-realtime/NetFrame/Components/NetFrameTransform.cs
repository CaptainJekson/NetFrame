using System;
using System.Collections.Generic;
using NetFrame.Interpolation;
using NetFrame.Utils;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace NetFrame.Components
{
    [Obsolete("Потом удалить")]
    public class NetFrameTransform : MonoBehaviour //todo по сути это только удаленный трансформ сейчас
    {
        [SerializeField] private Transform targetTransform;

        [SerializeField] private bool positionInterpolate = true;
        [SerializeField] private bool rotationInterpolate = true;
        
        [Header("Times")] 
        [SerializeField] private int frequencySend; //частота отправки
        
        [Header("Snapshot Interpolation settings")]
        [SerializeField] public SnapshotInterpolationSettings snapshotSettings = new SnapshotInterpolationSettings();

        private SortedList<double, INetworkDataframeTransform> _bufferSnapshots;
        
        private double BufferTime => IntervalSend * snapshotSettings.bufferTimeMultiplier;
        
        private float IntervalSend => 1.0f / frequencySend; //интервал отправки
        
        private float _lastSendTime;
        
        private double _localTimeline;
        private double _localTimescale = 1;
        
        private ExponentialMovingAverage _driftEma;
        private ExponentialMovingAverage _deliveryTimeEma;

        private void Awake()
        {
            _bufferSnapshots = new SortedList<double, INetworkDataframeTransform>();
            
            _driftEma = new ExponentialMovingAverage(frequencySend * snapshotSettings.driftEmaDuration);
            _deliveryTimeEma = new ExponentialMovingAverage(frequencySend * snapshotSettings.deliveryTimeEmaDuration);
        }

        private void Update()
        {
            var unscaledDeltaTime = Time.unscaledDeltaTime;

            if (_bufferSnapshots.Count > 0)
            {
                SnapshotInterpolation.Step(
                    _bufferSnapshots,
                    unscaledDeltaTime,
                    ref _localTimeline,
                    _localTimescale,
                    out INetworkDataframeTransform fromSnapshot,
                    out INetworkDataframeTransform toSnapshot,
                    out double time);

                if (positionInterpolate)
                {
                    targetTransform.position = Vector3.LerpUnclamped(fromSnapshot.Position, toSnapshot.Position, (float)time);
                }
                else
                {
                    var snap = _bufferSnapshots.Values[0];
                    targetTransform.position = snap.Position;
                    _bufferSnapshots.RemoveAt(0);
                }

                if (rotationInterpolate)
                {
                    targetTransform.rotation = Quaternion.SlerpUnclamped(fromSnapshot.Rotation, toSnapshot.Rotation, (float)time);
                }
                else
                {
                    var snap = _bufferSnapshots.Values[0];
                    targetTransform.rotation = snap.Rotation;
                    _bufferSnapshots.RemoveAt(0);
                }
            }
        }
        
        public void AddNetworkDataframeTransform(INetworkDataframeTransform dataframeSnapshots)
        {
            dataframeSnapshots.LocalTime = NetworkTime.LocalTime;

            if (snapshotSettings.dynamicAdjustment)
            {
                snapshotSettings.bufferTimeMultiplier = SnapshotInterpolation.DynamicAdjustment(
                    IntervalSend,
                    _deliveryTimeEma.StandardDeviation,
                    snapshotSettings.dynamicAdjustmentTolerance
                );
            }
            
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