using System;
using NetFrame.Encryption;
using UnityEngine;

namespace Examples
{
    public class KeyGenerator : MonoBehaviour
    {
        private NetFrameCryptographer _netFrameCryptographer;
        private NetFrameRsaKeyGenerator _rsaKeyGenerator;
        
        public void Awake()
        {
            _netFrameCryptographer = new NetFrameCryptographer();
            _rsaKeyGenerator = new NetFrameRsaKeyGenerator();
        }
    }
}