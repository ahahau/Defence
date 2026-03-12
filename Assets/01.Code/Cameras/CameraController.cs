using UnityEngine;

namespace _01.Code.Cameras
{
    public class CameraController : MonoBehaviour
    {
        [field:SerializeField] public InputDataSO InputData { get; private set; }
        
        [SerializeField] private CameraMover mover;
        private void Update()
        {
            mover.direction = InputData.MovementKey;
        }
    }
}