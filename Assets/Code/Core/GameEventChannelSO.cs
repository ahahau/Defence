using System;
using System.Collections.Generic;
using UnityEngine;

namespace Code.Core
{
    /// <summary>이벤트 채널을 통해 전달되는 게임 상태 변화의 공통 바탕 타입.</summary>
    public class GameEvent
    { }
    
    /// <summary>
    /// 씬 오브젝트가 서로를 직접 찾지 않고 구독할 수 있게 하는 타입별 이벤트 채널.
    /// 같은 핸들러는 한 번만 등록하며, 파괴된 Unity 오브젝트 구독자는 발행 시 건너뛴다.
    /// </summary>
    [CreateAssetMenu(menuName = "SO/EventChannel", fileName = "EventChannel")]
    public class GameEventChannelSO : ScriptableObject
    {
        private readonly Dictionary<Type, Action<GameEvent>> _events = new();
        private readonly Dictionary<Delegate, Action<GameEvent>> _lookUp = new();
        
        public void AddListener<T>(Action<T> handler) where T : GameEvent
        {
            // 중복 구독이면 같은 이벤트가 두 번 처리되므로 등록하지 않는다.
            if (handler == null || _lookUp.ContainsKey(handler))
                return;

            Action<GameEvent> castHandler = evt => handler((T)evt);
            _lookUp.Add(handler, castHandler);

            var eventType = typeof(T);
            _events.TryGetValue(eventType, out var handlers);
            _events[eventType] = handlers + castHandler;
        }

        public void RemoveListener<T>(Action<T> handler) where T : GameEvent
        {
            if (handler == null)
                return;

            var eventType = typeof(T);
            if (_lookUp.TryGetValue(handler, out Action<GameEvent> action))
            {
                if (_events.TryGetValue(eventType, out Action<GameEvent> internalAction))
                {
                    internalAction -= action;
                    if (internalAction == null)
                        _events.Remove(eventType);
                    else
                        _events[eventType] = internalAction;
                }
                
                // 원래 델리게이트를 키로 보관해야 다음 구독 때 새 래퍼를 만들 수 있다.
                _lookUp.Remove(handler);
            }
        }

        public void RaiseEvent(GameEvent evt)
        {
            if (evt == null || !_events.TryGetValue(evt.GetType(), out Action<GameEvent> handlers))
                return;

            foreach (Action<GameEvent> handler in handlers.GetInvocationList())
            {
                // 씬 전환 뒤 제거된 리스너가 채널에 남아도 발행 전체가 멈추지 않게 한다.
                if (handler.Target is UnityEngine.Object unityObject && unityObject == null)
                    continue;

                handler.Invoke(evt);
            }
        }

        /// <summary>테스트 또는 명시적인 시스템 초기화 때만 모든 구독을 제거한다.</summary>
        public void Clear()
        {
            _events.Clear();
            _lookUp.Clear();
        }
        
    }
}
