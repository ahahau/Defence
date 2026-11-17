using Code.Audio;
using Code.Combat;
using UnityEngine;

namespace Code.Core
{
    /// <summary>
    /// 씬에 놓이지 않는 정적 시스템(효과음·음악·전투 연출·코드가 그리는 선)에 쓸 에셋을 넘겨준다.
    ///
    /// 예전에는 저마다 <c>Resources.Load</c>로 경로를 찾아 읽었다. 경로 문자열은 에셋을 옮기면 조용히 끊긴다.
    /// 직렬화 참조는 GUID로 묶이므로 옮겨도 그대로다.
    /// 씬 오브젝트의 Awake가 <c>AfterSceneLoad</c> 자동 설치보다 먼저 돌아, 설치가 시작될 때는 참조가 이미 들어와 있다.
    /// 타이틀·게임 씬에 하나씩 둔다. 어느 씬에서 시작해도 소리가 나야 한다.
    /// </summary>
    public sealed class RuntimeAssetInstaller : MonoBehaviour
    {
        [SerializeField] private GameSfxCatalogSO sfxCatalog;
        [SerializeField] private GameMusicCatalogSO musicCatalog;
        [SerializeField] private SfxEventBridgeSO sfxEventBridge;
        [SerializeField] private CombatFxCatalogSO combatFxCatalog;
        [SerializeField] private Material lineMaterial;

        private void Awake()
        {
            WarnIfMissing(sfxCatalog, nameof(sfxCatalog));
            WarnIfMissing(musicCatalog, nameof(musicCatalog));
            WarnIfMissing(sfxEventBridge, nameof(sfxEventBridge));
            WarnIfMissing(combatFxCatalog, nameof(combatFxCatalog));
            WarnIfMissing(lineMaterial, nameof(lineMaterial));

            GameSfxPlayer.Provide(sfxCatalog);
            GameMusicPlayer.Provide(musicCatalog);
            UiSfxHooks.Provide(sfxEventBridge);
            CombatFxHooks.Provide(combatFxCatalog);
            RuntimeMaterials.Provide(lineMaterial);
        }

        private void WarnIfMissing(Object asset, string fieldName)
        {
            if (asset == null)
                Debug.LogWarning($"{nameof(RuntimeAssetInstaller)}.{fieldName}이(가) 비어 있습니다.", this);
        }
    }
}
