using DG.Tweening;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace _01.Code.Combat
{
    [FeedbackPath("Combat/Damage Pulse")]
    public class MMF_DamagePulse : MMF_Feedback
    {
        public static bool FeedbackTypeAuthorized = true;

        [MMFInspectorGroup("Damage Pulse", true, 65)]
        public Transform Target;
        public SpriteRenderer[] SpriteRenderers = new SpriteRenderer[0];
        public Color FlashColor = new(1f, 0.18f, 0.06f, 1f);
        public float Duration = 0.16f;
        public float ShakeDistance = 0.08f;
        public float PunchScale = 0.12f;
        public float RotationAngle = 5f;
        public int Vibrato = 12;

        public override float FeedbackDuration
        {
            get { return ApplyTimeMultiplier(Duration); }
            set { Duration = value; }
        }

        protected override void CustomPlayFeedback(Vector3 position, float feedbacksIntensity = 1.0f)
        {
            if (!Active || !FeedbackTypeAuthorized)
                return;

            var duration = Mathf.Max(0.01f, FeedbackDuration);
            PlayTargetPulse(duration, feedbacksIntensity);
            PlaySpriteFlash(duration);
        }

        // 처음 재생할 때의 자세를 기억해 두고 언제나 여기로 되돌린다.
        //
        // 예전에는 재생할 때마다 현재 자세를 기준으로 삼았다. 그래서 피격이 겹치면
        // 두 번째 펄스가 "이미 흔들린 중간 자세"를 기준으로 잡고, 그 값으로 복원해 버렸다.
        // 회전이 -14도쯤 틀어진 채 남는 버그가 그것이었다(2026-09-01 실측).
        private bool _basePoseCaptured;
        private Vector3 _basePosition;
        private Vector3 _baseScale;
        private Vector3 _baseRotation;

        private void PlayTargetPulse(float duration, float intensity)
        {
            if (Target == null)
                return;

            if (!_basePoseCaptured)
            {
                _basePosition = Target.localPosition;
                _baseScale = Target.localScale;
                _baseRotation = Target.localEulerAngles;
                _basePoseCaptured = true;
            }

            // 앞선 펄스가 돌고 있으면 끊고 원래 자세로 돌려놓은 뒤 시작한다.
            // 겹친 채로 시작하면 흔들린 자세 위에 또 흔들림이 쌓인다.
            RestoreBasePose(true);

            var basePosition = _basePosition;
            var baseScale = _baseScale;
            var baseRotation = _baseRotation;
            var direction = Random.value < 0.5f ? -1f : 1f;

            var sequence = DOTween.Sequence().SetUpdate(true);
            if (ShakeDistance > 0f)
            {
                sequence.Join(Target.DOShakePosition(
                    duration,
                    ShakeDistance * intensity,
                    Mathf.Max(1, Vibrato),
                    70f,
                    false,
                    true));
            }

            if (PunchScale > 0f)
                sequence.Join(Target.DOPunchScale(Vector3.one * (PunchScale * intensity), duration, 1, 0.45f));

            if (RotationAngle > 0f)
                sequence.Join(Target.DOPunchRotation(new Vector3(0f, 0f, RotationAngle * intensity * direction), duration, 1, 0.35f));

            // OnComplete가 아니라 OnKill이다. 정상 종료뿐 아니라 중간에 끊겼을 때도 불린다 —
            // 회피 연출의 DOComplete나 오브젝트 파괴로 끊기면 OnComplete는 돌지 않아
            // 어긋난 자세가 그대로 남았다.
            sequence.OnKill(() =>
            {
                if (Target == null)
                    return;

                Target.localPosition = basePosition;
                Target.localScale = baseScale;
                Target.localEulerAngles = baseRotation;
            });
            sequence.SetLink(Target.gameObject);
        }

        /// <summary>기억해 둔 자세로 되돌린다. <paramref name="killRunning"/>이면 돌고 있는 트윈부터 끊는다.</summary>
        private void RestoreBasePose(bool killRunning)
        {
            if (Target == null || !_basePoseCaptured)
                return;

            if (killRunning)
                Target.DOKill();

            Target.localPosition = _basePosition;
            Target.localScale = _baseScale;
            Target.localEulerAngles = _baseRotation;
        }

        private void PlaySpriteFlash(float duration)
        {
            if (SpriteRenderers == null)
                return;

            foreach (var spriteRenderer in SpriteRenderers)
            {
                if (spriteRenderer == null || spriteRenderer.sortingOrder >= 40)
                    continue;

                var originalColor = spriteRenderer.color;
                DOTween.Sequence()
                    .SetUpdate(true)
                    .Append(spriteRenderer.DOColor(FlashColor, duration * 0.35f))
                    .Append(spriteRenderer.DOColor(originalColor, duration * 0.65f))
                    .SetLink(spriteRenderer.gameObject);
            }
        }
    }
}
