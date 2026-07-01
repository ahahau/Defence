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

        private void PlayTargetPulse(float duration, float intensity)
        {
            if (Target == null)
                return;

            var basePosition = Target.localPosition;
            var baseScale = Target.localScale;
            var baseRotation = Target.localEulerAngles;
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

            sequence.OnComplete(() =>
            {
                if (Target == null)
                    return;

                Target.localPosition = basePosition;
                Target.localScale = baseScale;
                Target.localEulerAngles = baseRotation;
            });
            sequence.SetLink(Target.gameObject);
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
