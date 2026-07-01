using MoreMountains.Feedbacks;
using UnityEngine;

namespace _01.Code.Combat
{
    /// <summary>
    /// Feel 전투 연출에 필요한 씬 구성 요소를 런타임에 보장한다.
    /// 메인 카메라에 MMCameraShaker(+MMWiggle)가 없으면 자동으로 붙인다.
    /// </summary>
    public static class FeelCombatSceneSetup
    {
        private static MMCameraShaker cachedShaker;
        private static bool missingShakerReported;

        public static void EnsureCameraShaker()
        {
            if (cachedShaker != null)
                return;

            var mainCamera = Camera.main;
            if (mainCamera == null)
                return;

            cachedShaker = mainCamera.GetComponent<MMCameraShaker>();
            if (cachedShaker == null && !missingShakerReported)
            {
                missingShakerReported = true;
                Debug.LogError($"Main Camera requires {nameof(MMCameraShaker)} configured in the scene.", mainCamera);
            }
        }
    }
}
