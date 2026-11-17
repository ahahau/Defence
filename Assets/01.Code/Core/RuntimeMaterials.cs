using UnityEngine;

namespace Code.Core
{
    /// <summary>
    /// 코드가 실행 중에 그리는 선(함정 화살·설치 칸·침입 경로)이 함께 쓰는 재질.
    ///
    /// 예전에는 저마다 <c>Shader.Find</c>로 셰이더를 이름으로 찾아 새 재질을 만들었다. 이름이 바뀌거나 빌드에서
    /// 셰이더가 빠지면 조용히 null이 된다. 에셋 하나를 두면 빌드에 셰이더가 따라 들어간다.
    /// 에셋은 씬의 <see cref="RuntimeAssetInstaller"/>가 넘긴다.
    /// 색은 <see cref="LineRenderer"/>의 정점 색으로 주므로 재질은 공유한다.
    /// </summary>
    public static class RuntimeMaterials
    {
        private static Material line;
        private static bool missingLineReported;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            line = null;
            missingLineReported = false;
        }

        public static void Provide(Material lineMaterial)
        {
            if (lineMaterial != null)
                line = lineMaterial;
        }

        /// <summary>선 재질. 받지 못했으면 한 번 알리고 null — 선이 분홍색으로 보여 바로 드러난다.</summary>
        public static Material LoadLine()
        {
            if (line == null && !missingLineReported)
            {
                missingLineReported = true;
                // 에디트 모드 테스트에는 설치기가 없다. 그때는 테스트를 깨지 않게 경고로만 남긴다.
                if (Application.isPlaying)
                    Debug.LogError("선 재질을 받지 못했습니다. 씬의 RuntimeAssetInstaller를 확인하세요.");
                else
                    Debug.LogWarning("선 재질을 받지 못했습니다. 씬의 RuntimeAssetInstaller를 확인하세요.");
            }

            return line;
        }
    }
}
