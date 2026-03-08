using _01.Code.Buildings;
using _01.Code.Enemies;
using _01.Code.Manager;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _01.Code.Test
{
    public class TestObstacle : MonoBehaviour
    {
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                GameManager.Instance.WaveManager.StartWaves();
            }
        }
    }
}