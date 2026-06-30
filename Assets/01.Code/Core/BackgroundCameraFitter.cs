using UnityEngine;

namespace _01.Code.Core
{
    /// <summary>맵 배경을 카메라에 맞춰 따라다니게 하고 항상 뷰포트를 덮는 크기로 스케일한다.
    /// 노드 간격/최대 줌이 커져도 배경 밖 빈 화면이 보이지 않는다(원점 고정 배경의 한계 해소).</summary>
    [DisallowMultipleComponent]
    public class BackgroundCameraFitter : MonoBehaviour
    {
        [SerializeField, Tooltip("비우면 Camera.main을 사용.")]
        private Camera targetCamera;
        [SerializeField, Tooltip("스케일할 배경 스프라이트. 비우면 자식에서 자동 탐색.")]
        private SpriteRenderer backgroundRenderer;
        [SerializeField, Min(1f), Tooltip("뷰포트 대비 여유 배율(줌/이동 중 가장자리 노출 방지).")]
        private float coverScale = 1.1f;

        private float _baseZ;

        private void Awake()
        {
            if (backgroundRenderer == null)
                backgroundRenderer = GetComponentInChildren<SpriteRenderer>();

            _baseZ = transform.position.z;
        }

        private void LateUpdate()
        {
            var cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null || backgroundRenderer == null || backgroundRenderer.sprite == null)
                return;

            var camPosition = cam.transform.position;
            transform.position = new Vector3(camPosition.x, camPosition.y, _baseZ);

            if (!cam.orthographic)
                return;

            var viewHeight = cam.orthographicSize * 2f;
            var viewWidth = viewHeight * cam.aspect;
            var spriteSize = backgroundRenderer.sprite.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f)
                return;

            // 종횡비를 유지한 채 가로/세로 모두 덮는 균일 스케일.
            var required = Mathf.Max(viewWidth / spriteSize.x, viewHeight / spriteSize.y) * coverScale;
            backgroundRenderer.transform.localScale = new Vector3(required, required, 1f);
        }
    }
}
