using DG.Tweening;
using UnityEngine;

namespace _01.Code.UI
{
    /// <summary>
    /// 타이틀 뒤에서 계속 벌어지고 있는 싸움.
    ///
    /// 로고와 버튼만 놓인 화면은 게임이 시작되기 전까지 아무것도 말해 주지 않는다. 뒤에서
    /// 부하와 침입자가 맞붙어 있으면, 누르기 전에 이 게임이 무엇인지 한 장면으로 보여 준다.
    ///
    /// 진짜 전투를 돌리지 않는다. 유닛·적 프리팹을 세우면 관리자와 행동 그래프가 줄줄이 딸려
    /// 오는데, 타이틀에는 그것들이 없다. 여기 있는 것은 그림뿐이고 움직임도 정해진 왕복이다 —
    /// 보이는 것만 필요한 자리에 굴러가는 판을 통째로 올릴 이유가 없다.
    ///
    /// 배치는 씬에 있다. 이 컴포넌트는 씬에 놓인 것들을 흔들 뿐 자리를 만들지 않는다.
    /// </summary>
    public sealed class TitleDiorama : MonoBehaviour
    {
        [SerializeField, Tooltip("왼쪽에서 밀고 들어오는 침입자들.")]
        private RectTransform[] raiders = System.Array.Empty<RectTransform>();

        [SerializeField, Tooltip("오른쪽에서 막아서는 부하들.")]
        private RectTransform[] defenders = System.Array.Empty<RectTransform>();

        [Header("Idle")]
        [SerializeField, Min(0f)] private float bobHeight = 9f;
        [SerializeField, Min(0.1f)] private float bobDuration = 0.95f;

        [Header("Clash")]
        [SerializeField, Min(0f), Tooltip("맞붙을 때 서로에게 내미는 거리.")]
        private float lungeDistance = 46f;

        [SerializeField, Min(0.05f)] private float lungeDuration = 0.18f;

        [SerializeField, Min(0.2f), Tooltip("한 번 부딪히고 다음까지의 간격(초).")]
        private float clashInterval = 2.6f;

        private float _timer;

        private void OnEnable()
        {
            _timer = clashInterval * 0.5f;
            StartBobbing(raiders);
            StartBobbing(defenders);
        }

        private void OnDisable()
        {
            DOTween.Kill(this);
        }

        private void Update()
        {
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f)
                return;

            _timer = clashInterval;
            Clash();
        }

        /// <summary>
        /// 제자리에서 오르내리게 둔다. 완전히 멈춰 있으면 그림을 붙여 놓은 것처럼 보인다.
        ///
        /// 시작 시점을 조금씩 어긋내는 것이 중요하다. 넷이 같은 박자로 오르내리면 살아 있는 게
        /// 아니라 기계가 돌아가는 것처럼 읽힌다.
        /// </summary>
        private void StartBobbing(RectTransform[] group)
        {
            for (var i = 0; i < group.Length; i++)
            {
                var target = group[i];
                if (target == null)
                    continue;

                target.DOAnchorPosY(target.anchoredPosition.y + bobHeight, bobDuration)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetDelay(i * 0.23f)
                    .SetUpdate(true)
                    .SetLink(gameObject)
                    .SetId(this);
            }
        }

        /// <summary>맨 앞의 둘이 서로에게 한 번 달려들었다 물러난다.</summary>
        private void Clash()
        {
            Lunge(First(raiders), +lungeDistance);
            Lunge(First(defenders), -lungeDistance);
        }

        private void Lunge(RectTransform target, float distance)
        {
            if (target == null)
                return;

            // 세로 왕복은 그대로 두고 가로로만 움직인다. 둘을 한 트윈으로 묶으면
            // 오르내리던 것이 끊겨 툭 떨어진다.
            target.DOAnchorPosX(target.anchoredPosition.x + distance, lungeDuration)
                .SetEase(Ease.OutQuad)
                .SetLoops(2, LoopType.Yoyo)
                .SetUpdate(true)
                .SetLink(gameObject)
                .SetId(this);
        }

        private static RectTransform First(RectTransform[] group)
        {
            for (var i = 0; i < group.Length; i++)
            {
                if (group[i] != null)
                    return group[i];
            }

            return null;
        }
    }
}
