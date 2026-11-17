using System;
using UnityEngine;

namespace Code.Audio
{
    /// <summary>게임의 국면. 국면이 바뀌면 음악도 바뀐다.</summary>
    public enum MusicCue
    {
        /// <summary>웨이브 사이. 방을 짓고 부하를 배치하는 시간.</summary>
        Management,

        /// <summary>모험가가 들어오는 중.</summary>
        Wave,

        /// <summary>보스가 오는 날.</summary>
        Boss,

        /// <summary>상인이 왔을 때.</summary>
        Merchant,

        /// <summary>하루가 끝나고 장부를 볼 때.</summary>
        Settlement,
    }

    /// <summary>
    /// 국면마다 어떤 곡을 틀지 적어둔 표.
    /// 곡은 <see cref="GameSfxCatalogSO"/>와 달리 변주를 두지 않는다 —
    /// 배경음은 오래 깔리므로 매번 다른 곡이 나오면 오히려 산만하다.
    /// </summary>
    [CreateAssetMenu(fileName = "GameMusicCatalog", menuName = "Defence/Audio/Game Music Catalog")]
    public sealed class GameMusicCatalogSO : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [field: SerializeField] public MusicCue Cue { get; private set; }

            [field: SerializeField, Tooltip("루프 버전을 넣는다. 배경음은 끊기지 않아야 한다.")]
            public AudioClip Clip { get; private set; }

            [field: SerializeField, Range(0f, 1f), Tooltip("곡별 음량. 녹음 크기가 곡마다 달라 여기서 맞춘다.")]
            public float Volume { get; private set; } = 1f;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public Entry Find(MusicCue cue)
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
