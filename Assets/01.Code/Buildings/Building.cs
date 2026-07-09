using System.Collections;
using _01.Code.Combat;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace _01.Code.Buildings
{
    public class Building : MonoBehaviour
    {
        [field: SerializeField, Min(0)] public int DangerRating { get; private set; }
        public virtual void Initialize(BuildingDataSO data)
        {
            DangerRating = data.BaseDanger;
        }

        protected void PlayPassEffectFeedback(
            Combatant target,
            Color flashColor,
            float duration,
            MMF_Player feelFeedback = null)
        {
            if (target == null)
                return;

            if (feelFeedback != null)
                feelFeedback.PlayFeedbacks(target.transform.position);
            StartCoroutine(FlashTargetColor(target, flashColor, duration));
        }

        private IEnumerator FlashTargetColor(Combatant target, Color flashColor, float duration)
        {
            if (target == null)
                yield break;

            var renderers = target.GetComponentsInChildren<SpriteRenderer>();
            if (renderers == null || renderers.Length == 0)
                yield break;

            var originalColors = new Color[renderers.Length];
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;

                originalColors[i] = renderers[i].color;
                renderers[i].color = flashColor;
            }

            yield return new WaitForSeconds(Mathf.Max(0.01f, duration));

            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].color = originalColors[i];
            }
        }
    }
}
