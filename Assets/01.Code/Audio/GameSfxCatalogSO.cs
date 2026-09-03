using System;
using UnityEngine;

namespace _01.Code.Audio
{
    /// <summary>
    /// 큐 하나에 어떤 소리를 붙일지 적어둔 표.
    /// 효과음 팩이 소리마다 변주를 3개씩 주므로, 하나만 고르면 같은 소리가 연달아 나서
    /// 금방 질린다. 여기서는 변주를 통째로 들고 있다가 재생할 때 무작위로 하나를 고른다.
    /// </summary>
    [CreateAssetMenu(fileName = "GameSfxCatalog", menuName = "Defence/Audio/Game SFX Catalog")]
    public sealed class GameSfxCatalogSO : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [field: SerializeField] public GameSfxCue Cue { get; private set; }

            [field: SerializeField, Tooltip("이 큐의 변주들. 재생할 때 하나를 무작위로 고른다.")]
            public AudioClip[] Variants { get; private set; } = Array.Empty<AudioClip>();

            [field: SerializeField, Range(0f, 1f), Tooltip("큐별 음량. 팩마다 녹음 크기가 달라 여기서 맞춘다.")]
            public float Volume { get; private set; } = 1f;

            [field: SerializeField, Min(0f),
                    Tooltip("같은 큐가 다시 울리기까지 비워둘 시간(실시간, 초). " +
                            "여럿이 동시에 싸우면 타격음이 한 프레임에 몰려 잡음이 된다.")]
            public float MinInterval { get; private set; } = 0.04f;

            [field: SerializeField, Range(0f, 0.5f), Tooltip("재생마다 음높이를 이만큼 무작위로 흔든다.")]
            public float PitchJitter { get; private set; } = 0.08f;

            public AudioClip PickVariant()
            {
                if (Variants == null || Variants.Length == 0)
                    return null;

                // 변주가 하나뿐이면 그대로. 여럿이면 무작위.
                return Variants.Length == 1
                    ? Variants[0]
                    : Variants[UnityEngine.Random.Range(0, Variants.Length)];
            }
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public Entry Find(GameSfxCue cue)
        {
            if (entries == null)
                return null;

            foreach (var entry in entries)
                if (entry != null && entry.Cue == cue)
                    return entry;

            return null;
        }
    }
}
