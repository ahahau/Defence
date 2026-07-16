using UnityEngine;

namespace _01.Code.Audio
{
    /// <summary>
    /// 이전 씬과 프리팹의 직렬화 호환성을 위한 무음 컴포넌트.
    /// 프로젝트의 사운드 재생 경로는 제거되었으며 이 컴포넌트는 아무 동작도 하지 않는다.
    /// </summary>
    public sealed class GameSfxPlayer : MonoBehaviour
    {
        public static float Volume
        {
            get => 0f;
            set { }
        }

        public static void Play(GameSfxCue cue) { }

        private void Awake()
        {
            if (TryGetComponent<AudioSource>(out var source))
            {
                source.Stop();
                source.mute = true;
                source.playOnAwake = false;
                Destroy(source);
            }

            enabled = false;
        }
    }
}
