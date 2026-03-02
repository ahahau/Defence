using UnityEngine;

namespace _01.Code.Cameras
{
    public class CameraController : MonoBehaviour
    {
        [field:SerializeField] public CameraInputSO Input { get; private set; }
        
        [SerializeField] private CameraMover mover;
        private void Update()
        {
            mover.direction = Input.MovementKey;
        }
    }
}