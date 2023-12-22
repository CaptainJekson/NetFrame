using System;
using System.Collections.Generic;
using NetFrame.Interpolation;
using NetFrame.Utils;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace NetFrame.Components
{
    public class NetFrameTransform : MonoBehaviour //todo по сути это удаленный трансформ сейчас
    {
        [SerializeField] private Toggle togglePosInterpolate; //todo test
        
        [SerializeField] private Transform transform;

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
            positionInterpolate = togglePosInterpolate.isOn; //todo test
            
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
                    transform.position = Vector3.LerpUnclamped(fromSnapshot.Position, toSnapshot.Position, (float)time);
                }
                else
                {
                    var snap = _bufferSnapshots.Values[0];
                    transform.position = snap.Position;
                    _bufferSnapshots.RemoveAt(0);
                }

                if (rotationInterpolate)
                {
                    transform.rotation = Quaternion.SlerpUnclamped(fromSnapshot.Rotation, toSnapshot.Rotation, (float)time);
                }
                else
                {
                    var snap = _bufferSnapshots.Values[0];
                    transform.rotation = snap.Rotation;
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