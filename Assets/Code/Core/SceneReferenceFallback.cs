using UnityEngine;

namespace Code.Core
{
    /// <summary>
    /// 씬에 아직 연결하지 않은 참조를 임시로 찾는 유일한 통로. 찾을 때마다 경고를 남긴다.
    ///
    /// 씬을 직접 고칠 수 없는 동안 직렬화 필드로 옮긴 참조가 비어도 게임이 돌게 둔다.
    /// 씬 배선이 끝나면 이 클래스를 지운다 — 남은 호출처가 컴파일 오류로 전부 드러난다.
    /// </summary>
    public static class SceneReferenceFallback
    {
        public static T Find<T>(Object owner, string fieldName) where T : Object
        {
            var ownerName = owner != null ? owner.GetType().Name : "?";
            Debug.LogWarning($"{ownerName}.{fieldName}가 비어 있어 씬에서 {typeof(T).Name}을(를) 찾습니다. 씬에 연결하세요.", owner);
            return Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
        }

        /// <summary>
        /// 실행 중에 생기는 화면(Toolkit 문서 등)이 씬 객체를 찾을 때. 씬에서 연결할 수 없으므로 경고하지 않는다.
        /// 2단계에서 씬 조립 루트가 넘겨주게 되면 지운다.
        /// </summary>
        public static T FindForRuntimeObject<T>() where T : Object =>
            Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
    }
}
