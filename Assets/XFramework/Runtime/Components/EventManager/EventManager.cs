using System;
using System.Collections.Generic;
using UnityEngine;
using XFramework.Utils;

namespace XFramework
{
    [DisallowMultipleComponent]
    [AddComponentMenu("XFramework/Event Manager")]
    public sealed partial class EventManager : XFrameworkComponent
    {
        /// <summary>
        /// 事件字典
        /// </summary>
        /// <remarks>
        /// key 为事件 ID，value 为事件委托调用链。
        /// </remarks>
        private readonly Dictionary<int, EventHandlerChain> _handlerChainDict = new();

        /// <summary>
        /// 延迟发布事件列表
        /// </summary>
        private readonly XLinkedList<DelayEventWrapper> _delayedEvents = new();

        public int SubscribedEventCount
        {
            get => _handlerChainDict.Count;
        }

        public int DelayedEventCount
        {
            get => _delayedEvents.Count;
        }

        internal override int Priority
        {
            get => XFrameworkConstant.ComponentPriority.EventManager;
        }

        private void Update()
        {
            lock (_delayedEvents)
            {
                var node = _delayedEvents.First;
                while (node != null)
                {
                    DelayEventWrapper wrapper = node.Value;
                    wrapper.DelayFrame--;
                    if (wrapper.DelayFrame <= 0)
                    {
                        wrapper.HandlerChain.Fire(wrapper.Event);
                        _delayedEvents.Remove(node);
                        wrapper.Destroy();
                    }
                    node = node.Next;
                }
            }
        }

        internal override void Clear()
        {
            base.Clear();

            foreach (EventHandlerChain handlerChain in _handlerChainDict.Values)
            {
                handlerChain.Destroy();
            }
            foreach (DelayEventWrapper wrapper in _delayedEvents)
            {
                wrapper.Destroy();
            }
            _handlerChainDict.Clear();
            _delayedEvents.Clear();
        }

        public void Subscribe(int id, Action<IEvent> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler), "Subscribe failed, handler cannot be null.");
            }
            if (_handlerChainDict.TryGetValue(id, out EventHandlerChain handlerChian))
            {
                handlerChian.AddHandler(handler);
            }
            else
            {
                _handlerChainDict.Add(id, EventHandlerChain.Create());
                _handlerChainDict[id].AddHandler(handler);
            }
        }

        public void Unsubscribe(int id, Action<IEvent> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler), "Unsubscribe failed, handler cannot be null.");
            }
            if (_handlerChainDict.TryGetValue(id, out EventHandlerChain handlerChain))
            {
                handlerChain.RemoveHandler(handler);
                if (handlerChain.Count == 0)
                {
                    handlerChain.Destroy();
                    _handlerChainDict.Remove(id);
                }
            }
            else
            {
                Log.Error($"[XFramework] [EventManager] Unsubscribe failed, event id {id} does not exist.");
            }
        }

        public void Publish(int id, IEvent evt)
        {
            if (evt == null)
            {
                throw new ArgumentNullException(nameof(evt), "Publish failed, event arguments cannot be null.");
            }
            if (_handlerChainDict.TryGetValue(id, out EventHandlerChain handlerChain))
            {
                handlerChain.Fire(evt);
            }
            else
            {
                Log.Error($"[XFramework] [EventManager] Publish failed, event id {id} does not exist.");
            }
            evt.Destroy();
        }

        public void PublishLater(int id, IEvent evt, int delayFrame = 1)
        {
            if (evt == null)
            {
                throw new ArgumentNullException(nameof(evt), "PublishLater failed, event arguments cannot be null.");
            }
            lock (_delayedEvents)
            {
                if (_handlerChainDict.TryGetValue(id, out EventHandlerChain handlerChain))
                {
                    _delayedEvents.AddLast(DelayEventWrapper.Create(evt, handlerChain, delayFrame));
                }
                else
                {
                    Log.Error($"[XFramework] [EventManager] PublishLater failed, event id {id} does not exist.");
                }
            }
        }

        /// <summary>
        /// 移除所有订阅
        /// </summary>
        public void RemoveAllSubscriptions()
        {
            foreach (EventHandlerChain handlerChain in _handlerChainDict.Values)
            {
                handlerChain.Destroy();
            }
            _handlerChainDict.Clear();
        }
    }
}
