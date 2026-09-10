using TMPro;
using UnityEngine;

namespace _01.Code.UI
{
    /// <summary>
    /// 자리 비움 알림 프리팹이 자기 조각들을 가리키는 표.
    ///
    /// <see cref="SettingsWindowRefs"/>와 같은 이유로 이름 찾기 대신 직렬화 참조를 쓴다.
    /// 이름으로 찾으면 에디터에서 이름 한 번 고치는 것만으로 조용히 끊어진다.
    /// </summary>
    public sealed class IdleReturnNoticeRefs : MonoBehaviour
    {
        [Tooltip("알림 전체를 켜고 끄는 뿌리.")]
        public GameObject window;

        [Tooltip("남은 시간이 적히는 줄.")]
        public TMP_Text countdownText;

        /// <summary>표가 다 채워졌는가. 하나라도 비면 알림이 반쯤 죽은 채로 뜨므로 미리 본다.</summary>
        public bool IsComplete => window != null && countdownText != null;
    }
}
