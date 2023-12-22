using UnityEngine;

namespace Samples.Units
{
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private float speed = 4;

        private void Update()
        {
            var inputX = Input.GetAxisRaw("Horizontal");
            var inputY = Input.GetAxisRaw("Vertical");

            var inputVector = new Vector3(inputX, inputY , 0);
            var currentPosition = transform.position;
            
            currentPosition = Vector2.MoveTowards(currentPosition, currentPosition + inputVector,
                Time.deltaTime * speed);

            transform.position = currentPosition;
        }
    }
}