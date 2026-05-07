using UnityEngine;

namespace _01.Code.UI
{
    public class TimeSpeedView : MonoBehaviour
    {
        public void SetTimeSpeed(float speed)
        {
            float clampedSpeed = Mathf.Clamp(Time.timeScale + speed, 0f, 3f);
        }
    }
}