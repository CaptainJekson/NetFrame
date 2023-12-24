using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Samples.UI
{
    public class ControlButtons : MonoBehaviour
    {
        [SerializeField] public ButtonHoldAction upButton;
        [SerializeField] public ButtonHoldAction downButton;
        [SerializeField] public ButtonHoldAction leftButton;
        [SerializeField] public ButtonHoldAction rightButton;
        [SerializeField] public Slider slider;
        [SerializeField] public TextMeshProUGUI speedText;

        private void Awake()
        {
            speedText.text = slider.value.ToString(CultureInfo.InvariantCulture);
            slider.onValueChanged.AddListener(speed =>
            {
                speedText.text = speed.ToString(CultureInfo.InvariantCulture);
            });
        }
    }
}