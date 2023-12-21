using Samples.Dataframes;
using UnityEngine;

namespace Samples.Units
{
    public class PlayerServer : MonoBehaviour
    {
        [SerializeField] private float speed = 4;

        [SerializeField] private int frequencySend = 30; //частота отправки
        
        private float IntervalSend => 1.0f / frequencySend; //интервал отправки
        
        private float _lastSendTime;
        
        private void Update()
        {
            var inputX = Input.GetAxisRaw("Horizontal");
            var inputY = Input.GetAxisRaw("Vertical");

            var inputVector = new Vector3(inputX, inputY , 0);
            var currentPosition = transform.position;
            
            currentPosition = Vector2.MoveTowards(currentPosition, currentPosition + inputVector,
                Time.deltaTime * speed);

            transform.position = currentPosition;

            if (Time.time >= _lastSendTime + IntervalSend)
            {
                var dataframe = new PlayerMoveDataframe
                {
                    RemoteTime = Time.timeAsDouble,
                    LocalTime = 0,
                    Position = transform.position,
                    //Rotation = //надо тоже затестить
                };
                
                _lastSendTime = Time.time;

                ServerManager.Instance.NetFrameServer.SendAll(ref dataframe);
                
                //Debug.LogError($"{Time.timeAsDouble} --- {lastSendTime}");
            }
        }
    }
}