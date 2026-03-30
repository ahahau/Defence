using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace _01.Code.Test
{
    public class TestSceneLoader : MonoBehaviour
    {
        private void Update()
        {
            if (Keyboard.current.fKey.wasPressedThisFrame)
            {
                SceneManager.LoadScene("00.Scenes/s");
            }
            else if (Keyboard.current.lKey.wasPressedThisFrame)
            {
                SceneManager.LoadScene("00.Scenes/SampleScene");
            }
        }
    }
}