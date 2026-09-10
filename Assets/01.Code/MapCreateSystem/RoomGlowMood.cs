using _01.Code.BT;
using UnityEngine;

namespace _01.Code.MapCreateSystem
{
    /// <summary>
    /// 방 바닥의 불빛 색을 방 사정에 맞춰 물들인다.
    ///
    /// 불빛을 넣고 나니 방이 다 똑같이 따뜻했다. 예쁘긴 한데 스무 개가 같은 색이면 어디가
    /// 급한 방인지는 여전히 이름표를 읽어야 안다. 색은 이미 눈이 보고 있으니, 거기에 사정을
    /// 실으면 지도를 훑는 것만으로 판이 읽힌다.
    ///
    /// - 침입자가 들어온 방은 붉게
    /// - 부하가 지키고 선 방은 푸르게
    /// - 아무 일 없는 방은 원래의 횃불색
    ///
    /// 색은 머티리얼이 아니라 렌더러에 얹는다. 셰이더가 자기 색에 렌더러 색을 곱하도록 돼
    /// 있어서, 머티리얼 쪽을 흰색으로 두면 렌더러 색이 그대로 빛의 색이 된다.
    ///
    /// 머티리얼 속성을 직접 건드리면 방마다 재질이 복제된다.
    /// <see cref="MaterialPropertyBlock"/>도 답이 아니다 — 이 속성은 셰이더의 상수 버퍼에
    /// 들어 있어서 SRP 배처가 켜져 있으면 머티리얼 값이 그대로 쓰이고 얹은 값이 무시된다.
    /// 스프라이트 색은 원래 렌더러마다 다른 값이라 그런 문제가 없다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class RoomGlowMood : MonoBehaviour
    {
        [SerializeField, Tooltip("아무 일 없을 때의 횃불색.")]
        private Color calmColor = new(1f, 0.72f, 0.38f, 1f);

        [SerializeField, Tooltip("침입자가 들어와 있을 때.")]
        private Color threatColor = new(1f, 0.34f, 0.26f, 1f);

        [SerializeField, Tooltip("부하가 지키고 있을 때.")]
        private Color guardedColor = new(0.55f, 0.78f, 1f, 1f);

        [SerializeField, Min(0.05f), Tooltip("색이 바뀌는 데 걸리는 시간. 툭 바뀌면 눈에 거슬린다.")]
        private float blendSeconds = 0.45f;

        [SerializeField, Min(0.05f), Tooltip("방 사정을 다시 살피는 간격(초).")]
        private float pollInterval = 0.2f;

        private SpriteRenderer _renderer;
        private NodeBattlefield _battlefield;
        private Color _current;
        private Color _target;
        private float _timer;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _battlefield = GetComponentInParent<NodeBattlefield>();
            _current = calmColor;
            _target = calmColor;
            Apply();
        }

        private void OnEnable()
        {
            _timer = 0f;
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                _timer = pollInterval;
                _target = ResolveTarget();
            }

            if (_current == _target)
                return;

            _current = Color.Lerp(_current, _target, Mathf.Clamp01(Time.deltaTime / blendSeconds));
            Apply();
        }

        /// <summary>침입자가 먼저다. 둘 다 있으면 그 방은 지키는 방이 아니라 싸우는 방이다.</summary>
        private Color ResolveTarget()
        {
            if (_battlefield == null)
                return calmColor;

            if (_battlefield.EnemyCount > 0)
                return threatColor;

            return _battlefield.PlayerCount > 0 ? guardedColor : calmColor;
        }

        private void Apply()
        {
            _renderer.color = _current;
        }
    }
}
