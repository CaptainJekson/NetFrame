using System;
using Samples.UI;
using UnityEngine;

namespace Samples.Units
{
    public class PlayerController : MonoBehaviour
    {
        private ControlButtons _controlButtons;

        private void Awake()
        {
            _controlButtons = FindObjectOfType<ControlButtons>(); //todo for test
        }

        private void Update()
        {
            var currentPosition = transform.position;
            var speed = _controlButtons.slider.value;

            if (_controlButtons.upButton.buttonHeld)
            {
                currentPosition = Vector3.MoveTowards(currentPosition, currentPosition + Vector3.up,
                    Time.deltaTime * speed);
            }
            
            if (_controlButtons.downButton.buttonHeld)
            {
                currentPosition = Vector3.MoveTowards(currentPosition, currentPosition + Vector3.down,
                    Time.deltaTime * speed);
            }
            
            if (_controlButtons.leftButton.buttonHeld)
            {
                currentPosition = Vector3.MoveTowards(currentPosition, currentPosition + Vector3.left,
                    Time.deltaTime * speed);
            }
            
            if (_controlButtons.rightButton.buttonHeld)
            {
                currentPosition = Vector3.MoveTowards(currentPosition, currentPosition + Vector3.right,
                    Time.deltaTime * speed);
            }

            transform.position = currentPosition;
        }
    }
}