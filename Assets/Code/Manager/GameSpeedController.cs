using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Code.Manager
{
    /// <summary>
    /// 게임 속도의 단일 소유자. 플레이어가 고른 배속과, 연출·모달이 잠시 거는 정지를 함께 관리한다.
    ///
    /// 전에는 히트스톱·보스 연출·정책 모달·설정 창이 저마다 <see cref="Time.timeScale"/>을
    /// 읽고 썼다. 각자 "쓰기 전 값"을 기억했다가 되돌리는 방식이라, 겹치면 누가 마지막에
    /// 되돌리느냐에 따라 배속이 엉뚱한 값으로 남았다. 멈춘 채로 다음 날이 시작되기도 했다.
    ///
    /// 이제 되돌릴 곳은 언제나 플레이어가 고른 배속 하나다.
    /// </summary>
    public class GameSpeedController : MonoBehaviour
    {
        public static GameSpeedController Current { get; private set; }

        public const float PausedSpeed = 0f;
        public const float NormalSpeed = 1f;
        public const float FastSpeed = 2f;

        [SerializeField, Tooltip("판을 시작할 때의 배속.")]
        private float defaultSpeed = NormalSpeed;

        /// <summary>플레이어가 고른 배속. 연출이 끝나면 언제나 이 값으로 돌아온다.</summary>
        public float Setting { get; private set; } = NormalSpeed;

        /// <summary>지금 시간을 멈춰 둔 것들. 모달 두 개가 겹쳐도 둘 다 닫혀야 풀린다.</summary>
        private readonly HashSet<Object> _suspenders = new();

        /// <summary>
        /// 지금 세상이 멈춰 있는가. 플레이어가 세웠든 창이 세웠든 멈춘 것은 멈춘 것이다.
        ///
        /// 전에는 플레이어가 고른 배속만 봤다. 그래서 정책 창이 시간을 세워 둔 동안에도
        /// "안 멈췄다"고 답했고, 적이 한 발도 움직이지 않는 화면을 정상으로 읽게 만들었다.
        /// </summary>
        public bool IsPaused => Setting <= 0f || _suspenders.Count > 0;

        /// <summary>
        /// 플레이어가 직접 세워 둔 상태인가. 창이 세운 것은 여기 포함되지 않는다.
        ///
        /// 멈춤 버튼과 스페이스가 이 값을 본다. 창이 떠 있다고 해서 스페이스가 "재개"로
        /// 바뀌면, 창을 닫는 순간 플레이어가 고른 적 없는 배속으로 돌아간다.
        /// </summary>
        public bool IsPausedByPlayer => Setting <= 0f;

        /// <summary>배속이 바뀌었을 때. 버튼 표시를 맞추는 쪽이 듣는다.</summary>
        public event Action<float> SettingChanged;

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogError($"Duplicate {nameof(GameSpeedController)} detected. Keep exactly one scene instance.", this);
                enabled = false;
                return;
            }

            Current = this;
            Setting = Mathf.Clamp(defaultSpeed, PausedSpeed, FastSpeed);
            _suspenders.Clear();
            Apply();
        }

        /// <summary>
        /// 스페이스로 멈추고 푼다. 배속을 고르려고 매번 버튼까지 커서를 옮기면
        /// 급할 때 손이 늦는다. 1·2로 배속을 직접 고른다.
        ///
        /// 모달이 시간을 잡고 있는 동안에는 받지 않는다. 정책 창을 띄워 둔 채로
        /// 스페이스를 눌러 전투를 돌려 버리는 일을 막는다.
        /// </summary>
        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || _suspenders.Count > 0)
                return;

            if (keyboard.spaceKey.wasPressedThisFrame)
                TogglePause();
            else if (keyboard.digit1Key.wasPressedThisFrame)
                SetSetting(NormalSpeed);
            else if (keyboard.digit2Key.wasPressedThisFrame)
                SetSetting(FastSpeed);
        }

        private void OnDestroy()
        {
            if (Current != this)
                return;

            // 씬을 떠날 때 멈춘 채로 두면 다음 판이 시작하자마자 얼어 있다.
            Time.timeScale = NormalSpeed;
            Current = null;
        }

        /// <summary>플레이어가 배속을 고른다. 0이면 일시정지.</summary>
        public void SetSetting(float speed)
        {
            var clamped = Mathf.Clamp(speed, PausedSpeed, FastSpeed);
            if (Mathf.Approximately(clamped, Setting))
                return;

            Setting = clamped;
            Apply();
            SettingChanged?.Invoke(Setting);
        }
        
        public void TogglePause() => SetSetting(IsPausedByPlayer ? NormalSpeed : PausedSpeed);

        /// <summary>
        /// 모달이나 연출이 시간을 멈춘다. 같은 주인이 여러 번 불러도 한 번으로 센다.
        /// </summary>
        public void Suspend(Object owner)
        {
            if (owner == null || !_suspenders.Add(owner))
                return;

            Apply();
        }

        /// <summary>멈춰 둔 것을 푼다. 남은 것이 없으면 플레이어가 고른 배속으로 돌아간다.</summary>
        public void Release(Object owner)
        {
            if (owner == null || !_suspenders.Remove(owner))
                return;

            Apply();
        }

        /// <summary>
        /// 씬을 떠나기 직전처럼 모든 것을 정상으로 되돌려야 할 때.
        /// 멈춰 둔 주인들이 정리될 틈 없이 사라지는 경로가 있어 명시적인 출구가 필요하다.
        /// </summary>
        public void ResetToNormal()
        {
            _suspenders.Clear();
            Setting = NormalSpeed;
            Apply();
            SettingChanged?.Invoke(Setting);
        }

        private void Apply() => Time.timeScale = _suspenders.Count > 0 ? PausedSpeed : Setting;

        /// <summary>이 컨트롤러가 소유한 현재 복귀 속도.</summary>
        public float RestoreTimeScale => _suspenders.Count > 0 ? PausedSpeed : Setting;

        /// <summary>
        /// 연출이 끝나고 되돌릴 값. 히트스톱과 보스 연출이 "쓰기 전 값"을 저마다 기억하는 대신
        /// 여기를 본다. 그래야 연출 도중 플레이어가 배속을 바꿔도 끝나고 그 배속이 남는다.
        /// </summary>
        public static float RestoreTarget =>
            Current != null ? Current.RestoreTimeScale : NormalSpeed;
    }
}
