using System;
using System.Collections.Generic;
using UnityEngine;
using XFramework.Utils;

namespace XFramework
{
    /// <summary>
    /// 状态机管理器
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("XFramework/StateMachine Manager")]
    public sealed class StateMachineManager : XFrameworkComponent
    {
        private readonly Dictionary<int, StateMachineBase> _stateMachines = new();

        private const string DefaultStateMachineName = "default";

        internal override int Priority
        {
            get => XFrameworkConstant.ComponentPriority.StateMachineManager;
        }

        private void Update()
        {
            foreach (StateMachineBase stateMachine in _stateMachines.Values)
            {
                stateMachine.Update(Time.deltaTime, Time.unscaledDeltaTime);
            }
        }

        internal override void Clear()
        {
            base.Clear();

            foreach (StateMachineBase stateMachine in _stateMachines.Values)
            {
                stateMachine.Destroy();
            }
            _stateMachines.Clear();
        }

        public StateMachine<T> Create<T>(string name, T owner, params StateBase<T>[] states) where T : class
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name), "Create StateMachine failed. Name cannot be null.");
            }
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner), "Create StateMachine failed. Owner cannot be null.");
            }
            if (states == null || states.Length == 0)
            {
                throw new ArgumentNullException(nameof(states), "Create StateMachine failed. Initial states cannot be null or empty.");
            }
            int id = GetID(typeof(T), name);
            if (_stateMachines.ContainsKey(id))
            {
                throw new InvalidOperationException($"Create StateMachine failed. StateMachine with the same name ({name}) and same owner type ({typeof(T).Name}) already exists.");
            }

            var stateMachine = StateMachine<T>.Create(name, owner, states);
            _stateMachines.Add(id, stateMachine);
            return stateMachine;
        }

        public StateMachine<T> Create<T>(T owner, params StateBase<T>[] states) where T : class
        {
            return Create(DefaultStateMachineName, owner, states);
        }

        public StateMachine<T> Create<T>(T owner, List<StateBase<T>> states) where T : class
        {
            return Create(DefaultStateMachineName, owner, states.ToArray());
        }

        public StateMachine<T> Create<T>(string name, T owner, List<StateBase<T>> states) where T : class
        {
            return Create(name, owner, states.ToArray());
        }

        public StateMachine<T> Get<T>() where T : class
        {
            return Get<T>(DefaultStateMachineName);
        }

        public StateMachine<T> Get<T>(string name) where T : class
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name), "Get StateMachine failed. Name cannot be null.");
            }
            int id = GetID(typeof(T), name);
            if (_stateMachines.TryGetValue(id, out StateMachineBase stateMachine))
            {
                return stateMachine as StateMachine<T>;
            }
            return null;
        }

        public void Destroy<T>() where T : class
        {
            Destroy<T>(DefaultStateMachineName);
        }

        public void Destroy<T>(string name) where T : class
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name), "Destroy StateMachine failed. Name cannot be null.");
            }
            int id = GetID(typeof(T), name);
            if (_stateMachines.TryGetValue(id, out StateMachineBase stateMachine))
            {
                stateMachine.Destroy();
                _stateMachines.Remove(id);
            }
        }

        private int GetID(Type type, string name)
        {
            return (type.Name + name).GetHashCode();
        }
    }
}