using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace _01.Code.MapCreateSystem
{
    /// <summary>
    /// 방 하나를 은은하게 밝힌다.
    ///
    /// 전역광만 있으면 판이 고르게 밝아 방과 방 사이가 구분되지 않는다. 방마다 빛을 하나씩
    /// 두면 던전이 <b>빛 웅덩이가 늘어선 모양</b>으로 읽히고, 어디까지가 한 방인지 눈으로 잡힌다.
    ///
    /// 포탈처럼 숨쉬지 않는다. 방은 스물 넘게 깔리므로 전부 맥동하면 화면이 술렁인다 —
    /// 움직이는 빛은 <see cref="_01.Code.Buildings.PortalGlow"/> 하나로 충분하다.
    ///
    /// 잠긴 방은 어둡게 둔다. 열 수 있는 곳과 아닌 곳이 밝기로 갈리면 설명이 한 줄 줄어든다.
    /// </summary>
    public sealed class NodeGlow : MonoBehaviour
    {
        private static readonly Color UnlockedColor = new(1f, 0.86f, 0.66f, 1f);
        private static readonly Color LockedColor = new(0.55f, 0.60f, 0.78f, 1f);

        private const float Radius = 2.6f;
        private const float InnerRadius = 0.35f;
        private const float UnlockedIntensity = 0.85f;
        private const float LockedIntensity = 0.3f;

        private Light2D light2D;

        private void Awake()
        {
            var host = new GameObject("Node Glow");
            host.transform.SetParent(transform, false);

            light2D = host.AddComponent<Light2D>();
            light2D.lightType = Light2D.LightType.Point;
            light2D.pointLightOuterRadius = Radius;
            light2D.pointLightInnerRadius = InnerRadius;

            // 노드는 크기가 제각각인데 자식이라 부모 배율을 그대로 받는다.
            // 빛까지 같이 늘어나면 큰 방만 과하게 밝아지므로 되돌린다.
            var scale = transform.lossyScale;
            var factor = Mathf.Max(0.0001f, Mathf.Max(scale.x, scale.y));
            host.transform.localScale = Vector3.one / factor;

            SetUnlocked(false);
        }

        /// <summary>열린 방은 따뜻하게, 잠긴 방은 차갑고 어둡게.</summary>
        public void SetUnlocked(bool unlocked)
        {
            if (light2D == null)
                return;

            light2D.color = unlocked ? UnlockedColor : LockedColor;
            light2D.intensity = unlocked ? UnlockedIntensity : LockedIntensity;
        }
    }
}
