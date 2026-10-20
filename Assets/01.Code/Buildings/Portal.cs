using Code.MapCreateSystem;
using UnityEngine;

namespace Code.Buildings
{
    public class Portal : Building
    {
        /// <summary>
        /// 포탈은 다른 건물과 달리 중앙 슬롯에 맞춰 줄이지 않는다(CreateCentral 이 일부러 건너뛴다).
        /// 그래서 프리팹 크기를 그대로 쓰는데, 그게 방 안에서 작아 보였다. 여기서만 키운다.
        /// Awake 는 부모에 붙기 전에 돌고 SetParent 가 월드 크기를 지키므로 이 배율이 그대로 남는다.
        /// </summary>
        private const float VisualScale = 1.4f;

        protected override void Awake()
        {
            base.Awake();

            transform.localScale *= VisualScale;

            // 빛은 포탈이 세워지는 순간 함께 생겨야 한다. 포탈은 플레이어가 짓는 건물이라
            // 씬에 미리 없고, 프리팹을 고치는 대신 여기서 챙긴다.
            if (GetComponent<PortalGlow>() == null)
                gameObject.AddComponent<PortalGlow>();
        }

        public void Initialize(Node installedNode)
        {
            // 적 스폰은 WaveManager가 담당
        }
    }
}
