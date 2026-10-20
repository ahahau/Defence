using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Code.Buildings
{
    /// <summary>
    /// 포탈이 스스로 빛난다.
    ///
    /// 전역광 하나가 판 전체를 고르게 비추고 있어서 어디가 중요한 자리인지 빛이 말해 주지
    /// 않았다. 포탈은 침입자가 들어오는 단 하나의 입구다 — 판에서 가장 먼저 눈이 가야 할
    /// 자리이므로, 그쪽만 따뜻하게 띄운다.
    ///
    /// 라이트는 씬에 미리 놓지 않는다. 포탈은 플레이어가 세우는 건물이라 씬에 존재하지
    /// 않고, 세워지는 순간 함께 생겨야 한다.
    ///
    /// 숨쉬기는 <see cref="Time.unscaledDeltaTime"/>으로 돈다. 배속을 8배로 올려 두고
    /// 실측을 돌리는 프로젝트라, 게임 시간에 묶으면 8배로 깜빡여 눈에 거슬린다.
    /// </summary>
    [RequireComponent(typeof(Portal))]
    public sealed class PortalGlow : MonoBehaviour
    {
        private static readonly Color GlowColor = new(1f, 0.72f, 0.36f, 1f);

        private const float Radius = 3.4f;
        private const float InnerRadius = 0.6f;
        private const float BaseIntensity = 1.15f;

        /// <summary>숨쉬기 폭과 주기. 크게 흔들면 조명이 아니라 경고등으로 읽힌다.</summary>
        private const float PulseAmount = 0.16f;
        private const float PulseSpeed = 1.7f;

        private Light2D light2D;
        private float phase;

        private void Start()
        {
            var host = new GameObject("Portal Glow");
            host.transform.SetParent(transform, false);

            light2D = host.AddComponent<Light2D>();
            light2D.lightType = Light2D.LightType.Point;
            light2D.color = GlowColor;
            light2D.intensity = BaseIntensity;
            light2D.pointLightOuterRadius = Radius;
            light2D.pointLightInnerRadius = InnerRadius;

            // 시작 위상을 흩어 둔다. 나중에 빛나는 것이 더 생겨도 한 박자로 뛰지 않는다.
            phase = Random.value * Mathf.PI * 2f;
        }

        private void Update()
        {
            if (light2D == null)
                return;

            phase += Time.unscaledDeltaTime * PulseSpeed;
            light2D.intensity = BaseIntensity + Mathf.Sin(phase) * PulseAmount;
        }
    }
}
