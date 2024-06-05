using System;
using NetFrame.Encryption;
using UnityEngine;

namespace Examples
{
    public class KeyGenerator : MonoBehaviour
    {
        private NetFrameCryptographer _netFrameCryptographer;
        
        public void Awake()
        {
            _netFrameCryptographer = new NetFrameCryptographer();
            _netFrameCryptographer.TestRun();
        }
    }
}