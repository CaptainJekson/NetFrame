using System.Collections.Generic;
using NetFrame.Client;
using NetFrame.Interpolation;
using NetFrame.Utils;
using UnityEngine;

namespace NetFrame.Components
{
    public abstract class NetworkTransformBase<T> : MonoBehaviour where T : struct, INetworkDataframeTransform
    {
        [SerializeField] private bool isLocal;
        [SerializeField] private Transform targetTransform;

        [SerializeField] private bool positionInterpolate = true;
        [SerializeField] private bool rotationInterpolate = true;

        [Header("Times")] 
        [SerializeField] private int frequencySend = 30;
        
        [Header("Snapshot Interpolation settings")]
        [SerializeField] public SnapshotInterpolationSettings snapshotSettings = new SnapshotInterpolationSettings();

        private SortedList<double, T> _bufferSnapshots;
        
        private double BufferTime => IntervalSend * snapshotSettings.bufferTimeMultiplier;
        
        private float IntervalSend => 1.0f / frequencySend;
        
        private float _lastSendTime;
        
        private double _localTimeline;
        private double _localTimescale = 1;
        
        private ExponentialMovingAverage _driftEma;
        private ExponentialMovingAverage _deliveryTimeEma;
        
        private NetFrameClient _netFrameClient; //todo как получить это сразу
        
        public void ClientInitialize(NetFrameClient netFrameClient) //todo без вот этого ???
        {
            _netFrameClient = netFrameClient;
            
            _netFrameClient.Subscribe<T>(DataframeSnapshotsHandler);
        }

        private void Awake()
        {
            _bufferSnapshots = new SortedList<double, T>();
                        
            _driftEma = new ExponentialMovingAverage(frequencySend * snapshotSettings.driftEmaDuration);
            _deliveryTimeEma = new ExponentialMovingAverage(frequencySend * snapshotSettings.deliveryTimeEmaDuration);
        }

        private void Update()
        {
            if (isLocal)
            {
                if (Time.time >= _lastSendTime + IntervalSend)
                {
                    var currentTransform = transform;
                
                    var dataframe = new T
                    {
                        RemoteTime = NetworkTime.LocalTime,
                        LocalTime = 0,
                        Position = currentTransform.position,
                        Rotation = currentTransform.rotation,
                    };
                    
                    _lastSendTime = Time.time;
                    _netFrameClient.Send(ref dataframe);
                }
            }
            else
            {
                var unscaledDeltaTime = Time.unscaledDeltaTime;

                if (_bufferSnapshots.Count > 0)
                {
                    SnapshotInterpolation.Step(_bufferSnapshots, unscaledDeltaTime, ref _localTimeline, _localTimescale,
                        out T fromSnapshot, out T toSnapshot,out double time);

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
        }
        
        private void DataframeSnapshotsHandler(T dataframeSnapshots)
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

        private void OnDestroy()
        {
            _netFrameClient.Unsubscribe<T>(DataframeSnapshotsHandler);
        }
    }
}