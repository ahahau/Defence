using UnityEngine;

namespace _01.Code.Core.Stats
{
    /// <summary>
    /// 번호로 찾을 수 있는 자산. 스탯을 이름 문자열이 아니라 번호로 참조하면
    /// 이름을 바꿔도 참조가 끊기지 않고, 사전 조회도 문자열 해시보다 싸다.
    /// </summary>
    public abstract class IndexedAsset : ScriptableObject
    {
        [field: SerializeField] public int AssetIndex { get; set; }
    }
}
