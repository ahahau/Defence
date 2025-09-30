using System;
using UnityEngine;

namespace _01.Code.System
{
    public class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        public static T Instance { get; private set; }
        private void Awake()
        {
            Instance = FindObjectOfType(typeof(T)) as T;
        }
    }
}