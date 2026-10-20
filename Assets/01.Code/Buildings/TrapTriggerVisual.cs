using System.Collections.Generic;
using UnityEngine;

namespace Code.Buildings
{
    /// <summary>
    /// 함정이 터지는 모습. 방식마다 다르게 보여야 어디에 무엇이 깔렸는지 읽힌다 —
    /// 밟기는 베기, 범위는 폭발, 지속은 칸마다 솟는 불기둥, 투사체는 날아가는 화살이다.
    /// 그림과 색은 프리팹마다 <c>TrapEffectBuilder</c>가 넣는다.
    /// </summary>
    public class TrapTriggerVisual : MonoBehaviour
    {
        [SerializeField] private Sprite[] triggerFrames;
        [SerializeField, Min(0.01f)] private float frameSeconds = 0.06f;
        [SerializeField, Tooltip("효과의 아래 끝을 칸의 아래 끝에 맞춘다(솟는 효과).")]
        private bool anchorBottom;
        [SerializeField, Tooltip("투사체 함정이 날리는 그림. 오른쪽을 향해 있어야 한다.")]
        private Sprite projectileSprite;
        [SerializeField, Min(0.01f)] private float projectileSeconds = 0.18f;
        [SerializeField, Min(0.1f), Tooltip("화살 길이(칸).")]
        private float projectileWidthCells = 0.8f;
        [SerializeField, Tooltip("어두운 방에서도 보이게 빛을 받지 않는 재질을 쓴다.")]
        private Material effectMaterial;
        [SerializeField] private int sortingOrder = 50;

        private readonly List<TrapEffectSprite> _pool = new();

        public bool HasFrames => triggerFrames != null && triggerFrames.Length > 0 && triggerFrames[0] != null;
        public bool HasProjectile => projectileSprite != null;

        /// <summary>프레임을 한 번 돌린다. <paramref name="width"/>는 월드 폭이다.</summary>
        public void PlayOnce(Vector3 position, float width)
        {
            if (HasFrames)
                Acquire().Play(triggerFrames, frameSeconds, position, width, 0f, anchorBottom);
        }

        /// <summary>프레임을 <paramref name="seconds"/> 동안(게임 시간) 반복한다.</summary>
        public void PlayLoop(Vector3 position, float width, float seconds)
        {
            if (HasFrames)
                Acquire().Play(triggerFrames, frameSeconds, position, width, Mathf.Max(0.01f, seconds), anchorBottom);
        }

        public void FlyProjectile(Vector3 from, Vector3 to, float cellSize)
        {
            if (HasProjectile)
                Acquire().Fly(projectileSprite, from, to, projectileWidthCells * cellSize, projectileSeconds);
        }

        public void StopAll()
        {
            foreach (var effect in _pool)
            {
                if (effect != null)
                    effect.Stop();
            }
        }

        private void OnDisable()
        {
            StopAll();
        }

        private TrapEffectSprite Acquire()
        {
            foreach (var effect in _pool)
            {
                if (effect != null && !effect.IsPlaying)
                    return effect;
            }

            var child = new GameObject("TriggerEffect");
            child.transform.SetParent(transform, false);
            var created = child.AddComponent<TrapEffectSprite>();
            created.Initialize(effectMaterial, sortingOrder);
            _pool.Add(created);
            return created;
        }
    }
}
