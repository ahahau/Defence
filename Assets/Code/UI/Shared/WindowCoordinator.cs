using UnityEngine;

namespace Code.UI
{
    /// <summary>한 캔버스 안의 상호 배타 창을 직렬화된 목록으로 관리한다.</summary>
    public class WindowCoordinator : MonoBehaviour
    {
        [SerializeField] private GameObject[] windows = System.Array.Empty<GameObject>();

        /// <summary>대상만 열고 나머지 등록 창은 닫는다. 마지막 형제 순서로 올려 같은 캔버스의 앞에 표시한다.</summary>
        public void Open(GameObject target)
        {
            if (target == null)
                return;

            foreach (var window in windows)
            {
                if (window != null && window != target)
                    window.SetActive(false);
            }

            target.SetActive(true);
            target.transform.SetAsLastSibling();
        }

        /// <summary>창을 파괴하지 않고 비활성화해 인스펙터 배선과 내부 상태를 보존한다.</summary>
        public void Close(GameObject target)
        {
            if (target != null)
                target.SetActive(false);
        }
    }
}
