using System.Collections.Generic;
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

        // 자세와 같은 이유로 색도 처음 한 번만 기억한다. 다만 이쪽은 인스턴스별로는 부족하다.
        //
        // 예전에는 점멸을 시작할 때마다 그 순간의 색을 "원래 색"으로 삼았다. 그래서 앞선 점멸이
        // 아직 붉은 구간에 있을 때 또 맞으면 붉은색이 원래 색으로 기억되고, 그 값으로 되돌아가
        // 영영 붉게 남았다. 맞을수록 더 붉어진다.
        //
        // 게다가 한 유닛의 SpriteRenderer 하나를 여러 DamagePulse가 나눠 쓴다.
        // FeelCombatFeedbacks만 해도 일반 피격·강타·사망 셋이고, SkillCaster의 시전·궁극기까지
        // 같은 배열을 넘겨받는다. 그래서 원래 색과 돌고 있는 점멸은 인스턴스가 아니라
        // SpriteRenderer별로 기억해야 한다 — 강타가 일반 피격의 붉은 중간색을 물려받으면 똑같이 남는다.
        private static readonly Dictionary<SpriteRenderer, Color> BaseColors = new();
        private static readonly Dictionary<SpriteRenderer, Sequence> RunningFlashes = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistries()
        {
            // 도메인 리로드를 꺼 둔 프로젝트에서는 static이 플레이 모드를 넘어 살아남는다.
            // 지난 판의 죽은 렌더러가 남아 있으면 다음 판의 원래 색을 엉뚱하게 잡는다.
            BaseColors.Clear();
            RunningFlashes.Clear();
        }

        private void PlaySpriteFlash(float duration)
        {
            if (SpriteRenderers == null)
                return;

            foreach (var spriteRenderer in SpriteRenderers)
            {
                if (spriteRenderer == null || spriteRenderer.sortingOrder >= 40)
                    continue;

                if (!BaseColors.TryGetValue(spriteRenderer, out var baseColor))
                {
                    baseColor = spriteRenderer.color;
                    PruneDeadEntries();
                    BaseColors[spriteRenderer] = baseColor;
                }

                // 돌고 있던 점멸만 끊는다. spriteRenderer.DOKill()로 싹 끊으면
                // 사망 연출의 페이드까지 같이 끊긴다.
                if (RunningFlashes.TryGetValue(spriteRenderer, out var running))
                    running?.Kill();

                var renderer = spriteRenderer;
                RunningFlashes[renderer] = DOTween.Sequence()
                    .SetUpdate(true)
                    .Append(renderer.DOColor(WithAlphaOf(FlashColor, renderer), duration * 0.35f))
                    .Append(renderer.DOColor(WithAlphaOf(baseColor, renderer), duration * 0.65f))
                    // 자세 복원과 같이 OnKill이다. 다음 피격이 끊고 들어오든 오브젝트가 사라지든
                    // 색은 반드시 제자리로 돌아간다.
                    .OnKill(() =>
                    {
                        RunningFlashes.Remove(renderer);
                        if (renderer == null)
                            return;

                        // 알파는 건드리지 않는다. 사망 페이드가 그쪽을 쓰고 있을 수 있다.
                        renderer.color = WithAlphaOf(baseColor, renderer);
                    })
                    .SetLink(renderer.gameObject);
            }
        }

        /// <summary>파괴된 렌더러가 표에 쌓이지 않게 가끔 걷어낸다. 웨이브마다 유닛이 죽고 사라진다.</summary>
        private static void PruneDeadEntries()
        {
            if (BaseColors.Count < 256)
                return;

            var dead = new List<SpriteRenderer>();
            foreach (var key in BaseColors.Keys)
                if (key == null)
                    dead.Add(key);

            foreach (var key in dead)
            {
                BaseColors.Remove(key);
                RunningFlashes.Remove(key);
            }
        }

        private static Color WithAlphaOf(Color color, SpriteRenderer spriteRenderer)
        {
            color.a = spriteRenderer.color.a;
            return color;
        }
    }
}
