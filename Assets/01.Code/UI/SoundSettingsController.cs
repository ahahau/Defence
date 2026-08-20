using UnityEngine;

namespace _01.Code.UI
{
    /// <summary>기존 Start 씬 직렬화 호환용. 사운드 설정 UI는 더 이상 사용하지 않는다.</summary>
    public sealed class SoundSettingsController : MonoBehaviour
    {
        private void Awake()
        {
            gameObject.SetActive(false);
        }

        public void Load() { }
        public void ResetDefaults() { }
    }
}
