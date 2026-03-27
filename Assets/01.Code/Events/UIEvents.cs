using _01.Code.Core;
using UnityEngine;

namespace _01.Code.Events
{
    public static class UIEvents
    {
        public static readonly ShowDamageTextRequestedEvent ShowDamageTextRequestedEvent = new ShowDamageTextRequestedEvent();
    }

    public class ShowDamageTextRequestedEvent : GameEvent
    {
        public Vector3 WorldPosition { get; private set; }
        public float Damage { get; private set; }
        public Transform FollowTarget { get; private set; }

        public ShowDamageTextRequestedEvent Initializer(Vector3 worldPosition, float damage, Transform followTarget = null)
        {
            WorldPosition = worldPosition;
            Damage = damage;
            FollowTarget = followTarget;
            return this;
        }
    }

    public class LeftUpperPanelChange : GameEvent
    {
        public int PanelIndex{get; private set;}

        public LeftUpperPanelChange Initializer(int panelIndex)
        {
            PanelIndex = panelIndex;
            return this;
        }
    }
}
