using UnityEngine;
using NotImplementedException = System.NotImplementedException;

namespace _01.Code.Cameras
{
    public class CameraController : MonoBehaviour
    {
        [field:SerializeField] public InputDataSO InputData { get; private set; }
        
        [SerializeField] private CameraMover mover;
        private float _beforeScrolly;

        private void Awake()
        {
            _beforeScrolly = Camera.main.orthographicSize;
        }

        private void Update()
        {
            mover.direction = InputData.MovementKey;
            //ScrollMove();
        }

        private void ScrollMove()
        {
            float scrollY = InputData.ScrollValue;
            
            if (_beforeScrolly - scrollY < 5 || _beforeScrolly - scrollY > 15)
                return;
            
            
            _beforeScrolly -= scrollY;
            Camera.main.orthographicSize  = _beforeScrolly;
        }
    }
}