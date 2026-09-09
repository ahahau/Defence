using _01.Code.MapCreateSystem;
using UnityEngine;

namespace _01.Code.Buildings
{
    public class Portal : Building
    {
        protected override void Awake()
        {
            base.Awake();

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
