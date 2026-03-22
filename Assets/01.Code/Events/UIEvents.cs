using _01.Code.Core;
using UnityEngine;

namespace _01.Code.Events
{
    public static class UIEvents
    {
        public static readonly ShowBuildPanelRequestedEvent ShowBuildPanelRequested = new ShowBuildPanelRequestedEvent();
        public static readonly HideBuildPanelRequestedEvent HideBuildPanelRequested = new HideBuildPanelRequestedEvent();
    }

    public class ShowBuildPanelRequestedEvent : GameEvent
    {
        public Vector3 WorldPosition { get; private set; }

        public ShowBuildPanelRequestedEvent Initializer(Vector3 worldPosition)
        {
            WorldPosition = worldPosition;
            return this;
        }
    }

    public class HideBuildPanelRequestedEvent : GameEvent
    {
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
}
