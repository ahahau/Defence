using UnityEngine;

namespace Code.Audio
{
    /// <summary>
    /// 배경음악을 국면에 맞춰 갈아준다.
    ///
    /// 소스를 두 개 두고 하나가 잦아드는 동안 다른 하나가 올라오게 한다. 곡을 딱 끊고 바꾸면
    /// 웨이브가 시작될 때마다 소리가 툭 끊겨서 오히려 거슬린다.
    ///
    /// 시간은 전부 <see cref="Time.unscaledDeltaTime"/>으로 센다 — 정산 모달이 timeScale을 0으로
    /// 내리는데, 그때도 음악은 계속 흘러야 한다.
    /// </summary>
    public sealed class GameMusicPlayer : MonoBehaviour
    {
        private const string CatalogResourcePath = "Audio/GameMusicCatalog";
        private const string VolumeKey = "audio.music.volume";
        private const float CrossfadeDuration = 1.2f;

        private static GameMusicPlayer instance;
        private static GameMusicCatalogSO catalog;
        private static bool catalogLoaded;
        private static float volume = -1f;

        private AudioSource activeSource;
        private AudioSource fadingSource;
        private MusicCue? currentCue;
        private float activeTrackVolume = 1f;
        private float fadingFromVolume;
        private float fadeProgress = 1f;

        /// <summary>0이면 음악이 꺼진다. 효과음과 따로 저장된다 — 음악만 끄고 싶은 사람이 있다.</summary>
        public static float Volume
        {
            get
            {
                if (volume < 0f)
                    volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 0.4f));
                return volume;
            }
            set
            {
                volume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(VolumeKey, volume);
                // 즉시 기록한다. Unity는 종료할 때 알아서 저장하지만, 그 전에 죽으면 설정이 날아간다.
                PlayerPrefs.Save();
                if (instance != null)
                    instance.ApplyVolume();
            }
        }

        /// <summary>같은 국면을 다시 요청하면 아무 일도 하지 않는다 — 곡이 처음부터 다시 시작되지 않게.</summary>
        public static void Play(MusicCue cue)
        {
            if (!Application.isPlaying)
                return;

            EnsureInstance();
            instance?.SwitchTo(cue);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            catalog = null;
            catalogLoaded = false;
            volume = -1f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            EnsureInstance();
            // 게임에 들어오면 일단 관리 단계 음악부터. 웨이브가 시작되면 알아서 바뀐다.
            Play(MusicCue.Management);
        }

        private static void EnsureInstance()
        {
            if (instance != null)
                return;

            var host = new GameObject("Game Music Player");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<GameMusicPlayer>();
        }

        private static GameMusicCatalogSO EnsureCatalog()
        {
            if (catalogLoaded)
                return catalog;

            catalogLoaded = true;
            catalog = Resources.Load<GameMusicCatalogSO>(CatalogResourcePath);
            if (catalog == null)
                Debug.LogWarning($"음악 표를 찾지 못했습니다: Resources/{CatalogResourcePath}. 배경음악이 나오지 않습니다.");

            return catalog;
        }

        private void Awake()
        {
            activeSource = CreateSource("Music A");
            fadingSource = CreateSource("Music B");
        }

        private AudioSource CreateSource(string sourceName)
        {
            var child = new GameObject(sourceName);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.ignoreListenerPause = true;
            return source;
        }

        private void SwitchTo(MusicCue cue)
        {
            if (currentCue == cue)
                return;

            var entry = EnsureCatalog()?.Find(cue);
            if (entry == null || entry.Clip == null)
                return;

            currentCue = cue;

            // 역할을 맞바꾼다. 지금 나오던 소스가 잦아드는 쪽이 된다.
            (activeSource, fadingSource) = (fadingSource, activeSource);

            fadingFromVolume = fadingSource.volume;
            activeTrackVolume = entry.Volume;

            activeSource.clip = entry.Clip;
            activeSource.volume = 0f;
            activeSource.Play();

            fadeProgress = 0f;
        }

        private void Update()
        {
            if (fadeProgress >= 1f)
                return;

            fadeProgress = Mathf.Min(1f, fadeProgress + Time.unscaledDeltaTime / CrossfadeDuration);

            activeSource.volume = Mathf.Lerp(0f, Volume * activeTrackVolume, fadeProgress);
            fadingSource.volume = Mathf.Lerp(fadingFromVolume, 0f, fadeProgress);

            if (fadeProgress >= 1f)
                fadingSource.Stop();
        }

        private void ApplyVolume()
        {
            // 교차 중이면 Update가 다음 프레임에 다시 맞춘다.
            if (fadeProgress >= 1f && activeSource != null)
                activeSource.volume = Volume * activeTrackVolume;
        }
    }
}
