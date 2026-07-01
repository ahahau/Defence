using System.Collections;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace _01.Code.Combat
{
    /// <summary>
    /// 전투 히트스톱용 커스텀 Feel 피드백.
    /// MMTimeManager는 프리즈 후 timeScale을 NormalTimeScale(1)로 되돌려서
    /// TimeSpeedView의 배속(2x 등)이나 일시정지와 충돌하므로,
    /// 현재 timeScale을 기억했다가 그대로 복원하는 방식을 쓴다.
    /// </summary>
    [FeedbackPath("Time/Combat Hit Stop")]
    public class MMF_HitStop : MMF_Feedback
    {
        /// a static bool used to disable all feedbacks of this type at once
        public static bool FeedbackTypeAuthorized = true;

        [MMFInspectorGroup("Hit Stop", true, 64)]
        /// 히트스톱 지속 시간(실시간 기준, 초)
        public float Duration = 0.045f;
        /// 히트스톱 동안 적용할 timeScale (0이면 완전 정지)
        [Range(0f, 1f)] public float SlowTimeScale = 0.05f;
        /// timeScale이 이 값보다 낮으면(일시정지, 이미 히트스톱 중) 재생하지 않는다
        public float MinimumTimescaleThreshold = 0.1f;

        public override float FeedbackDuration
        {
            get { return ApplyTimeMultiplier(Duration); }
            set { Duration = value; }
        }

        protected override void CustomPlayFeedback(Vector3 position, float feedbacksIntensity = 1.0f)
        {
            if (!Active || !FeedbackTypeAuthorized)
                return;

            HitStopRunner.Play(FeedbackDuration, SlowTimeScale, MinimumTimescaleThreshold);
        }
    }

    /// <summary>timeScale을 잠깐 낮췄다가 원래 배속으로 복원하는 러너.</summary>
    public class HitStopRunner : MonoBehaviour
    {
        private static HitStopRunner _instance;

        private Coroutine _routine;
        private float _capturedTimeScale = 1f;
        private float _slowedTimeScale;

        public static void Play(float duration, float slowTimeScale, float minimumThreshold)
        {
            if (duration <= 0f || Time.timeScale < minimumThreshold)
                return;

            if (_instance == null)
            {
                var runnerObject = new GameObject("CombatHitStopRunner");
                DontDestroyOnLoad(runnerObject);
                _instance = runnerObject.AddComponent<HitStopRunner>();
            }

            _instance.Run(duration, slowTimeScale);
        }

        private void Run(float duration, float slowTimeScale)
        {
            if (_routine != null)
                StopCoroutine(_routine);
            else
                _capturedTimeScale = Time.timeScale;

            _routine = StartCoroutine(HitStopRoutine(duration, slowTimeScale));
        }

        private IEnumerator HitStopRoutine(float duration, float slowTimeScale)
        {
            _slowedTimeScale = Mathf.Min(Time.timeScale, slowTimeScale);
            Time.timeScale = _slowedTimeScale;
            yield return new WaitForSecondsRealtime(duration);

            RestoreTimeScale();
            _routine = null;
        }

        // 히트스톱 도중 외부(일시정지/배속 변경/게임오버)에서 timeScale을 건드렸다면 복원하지 않는다
        private void RestoreTimeScale()
        {
            if (Mathf.Approximately(Time.timeScale, _slowedTimeScale))
                Time.timeScale = _capturedTimeScale;
        }

        private void OnDestroy()
        {
            if (_routine != null)
                RestoreTimeScale();
        }
    }
}
