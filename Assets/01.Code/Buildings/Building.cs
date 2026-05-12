using System;
using System.Collections;
using System.Reflection;
using _01.Code.Combat;
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
            MonoBehaviour feelFeedback = null)
        {
            if (target == null)
                return;

            PlayFeelFeedback(feelFeedback, target.transform.position);
            StartCoroutine(FlashTargetColor(target, flashColor, duration));
        }

        private static void PlayFeelFeedback(MonoBehaviour feelFeedback, Vector3 position)
        {
            if (feelFeedback == null)
                return;

            var playAtPosition = feelFeedback.GetType().GetMethod(
                "PlayFeedbacks",
                new[] { typeof(Vector3), typeof(float), typeof(bool) });
            if (playAtPosition != null)
            {
                playAtPosition.Invoke(feelFeedback, new object[] { position, 1f, false });
                return;
            }

            var play = feelFeedback.GetType().GetMethod(
                "PlayFeedbacks",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);
            play?.Invoke(feelFeedback, null);
        }

        private static IEnumerator FlashTargetColor(Combatant target, Color flashColor, float duration)
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
