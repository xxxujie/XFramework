using System;
using System.Collections.Generic;
using XFramework.Utils;

namespace XFramework
{
    public abstract class FSM
    {
        internal abstract void Update(float deltaTime, float unscaleDeltaTime);
        internal abstract void Destroy();
    }

    /// <summary>
    /// 有限状态机
    /// </summary>
    /// <typeparam name="T">有限状态机的所有者类型</typeparam>
    public sealed class FSM<T> : FSM, IReference where T : class
    {
        private readonly Dictionary<Type, FSMState<T>> _stateDict = new();
        private string _name;
        private T _owner;
        private FSMState<T> _currentState;
        private float _currentStateTime = 0f;
        private bool _isDestroyed = false;

        /// <summary>
        /// 状态机名称
        /// </summary>
        public string Name
        {
            get { return _name; }
        }

        /// <summary>
        /// 状态机所有者
        /// </summary>
        public T Owner
        {
            get { return _owner; }
        }

        /// <summary>
        /// 状态机的状态数量
        /// </summary>
        public int StateCount
        {
            get { return _stateDict.Count; }
        }

        /// <summary>
        /// 当前状态
        /// </summary>
        public FSMState<T> CurrentState
        {
            get { return _currentState; }
        }

        /// <summary>
        /// 当前状态已持续时间
        /// </summary>
        /// <remarks>
        /// 单位：秒，切换时重置为 0。
        /// </remarks>
        public float CurrentStateTime
        {
            get { return _currentStateTime; }
        }

        public bool IsDestroyed
        {
            get { return _isDestroyed; }
        }

        internal static FSM<T> Create(string name, T owner, params FSMState<T>[] states)
        {
            var fsm = ReferencePool.Spawn<FSM<T>>();
            fsm._name = name ?? throw new ArgumentNullException(nameof(name), $"Spawn FSM failed. Name cannot be null.");
            fsm._owner = owner ?? throw new ArgumentNullException(nameof(owner), $"Spawn FSM failed. Owner cannot be null.");
            foreach (FSMState<T> state in states)
            {
                if (state == null)
                {
                    throw new ArgumentNullException(nameof(states), $"Spawn FSM failed. The state in initial states cannot be null.");
                }
                if (fsm._stateDict.ContainsKey(state.GetType()))
                {
                    throw new ArgumentException($"Spawn FSM failed. Duplicate state in initial states is not allowed, type {state.GetType().FullName} is already found.", nameof(states));
                }
                fsm._stateDict.Add(state.GetType(), state);
                state.OnInit(fsm);
            }
            fsm._isDestroyed = false;
            return fsm;
        }

        internal override void Update(float deltaTime, float unscaledeltaTime)
        {
            if (!CheckStarted() || _isDestroyed)
            {
                return;
            }
            _currentStateTime += unscaledeltaTime;
            _currentState.OnUpdate(this, deltaTime, unscaledeltaTime);
        }

        internal override void Destroy()
        {
            _currentState?.OnExit(this);
            foreach (FSMState<T> state in _stateDict.Values)
            {
                state.OnFsmDestroy(this);
            }
            _isDestroyed = true;
            ReferencePool.Recycle(this);
        }

        /// <summary>
        /// 启动状态机
        /// </summary>
        /// <typeparam name="TState">启动时的状态类型</typeparam>
        public void Start<TState>() where TState : FSMState<T>
        {
            if (_isDestroyed)
            {
                throw new InvalidOperationException($"Start FSM {Name} failed. It has already been destroyed.");
            }
            if (CheckStarted())
            {
                throw new InvalidOperationException($"Start FSM {Name} failed. It has already been started, don't start it again.");
            }

            if (_stateDict.TryGetValue(typeof(TState), out FSMState<T> state))
            {
                _currentState = state;
                _currentStateTime = 0;
                _currentState.OnEnter(this);
            }
            else
            {
                throw new ArgumentException($"Launch FSM {Name} failed. State of type {typeof(TState).FullName} not found.", nameof(TState));
            }
        }

        /// <summary>
        /// 启动状态机
        /// </summary>
        /// <param name="startStateType">启动时的状态类型</param>
        public void Start(Type startStateType)
        {
            if (_isDestroyed)
            {
                throw new InvalidOperationException($"Start FSM {Name} failed. It has already been destroyed.");
            }
            if (CheckStarted())
            {
                throw new InvalidOperationException($"Start FSM {Name} failed. It has already been started, don't start it again.");
            }
            CheckTypeCompilance(startStateType);

            if (_stateDict.TryGetValue(startStateType, out FSMState<T> state))
            {
                _currentState = state;
                _currentStateTime = 0;
                _currentState.OnEnter(this);
            }
            else
            {
                throw new ArgumentException($"Launch FSM {Name} failed. State of type {startStateType.FullName} not found.", nameof(startStateType));
            }
        }

        public TState GetState<TState>() where TState : FSMState<T>
        {
            if (_stateDict.TryGetValue(typeof(TState), out FSMState<T> state))
            {
                return state as TState ?? throw new InvalidOperationException($"Get state of FSM {Name} failed.");
            }
            return null;
        }

        public FSMState<T> GetState(Type stateType)
        {
            CheckTypeCompilance(stateType);

            if (_stateDict.TryGetValue(stateType, out FSMState<T> state))
            {
                return state;
            }
            return null;
        }

        public bool HasState<TState>() where TState : FSMState<T>
        {
            return _stateDict.ContainsKey(typeof(TState));
        }

        public bool HasState(Type stateType)
        {
            CheckTypeCompilance(stateType);

            return _stateDict.ContainsKey(stateType);
        }

        public void ChangeState<TState>() where TState : FSMState<T>
        {
            if (_isDestroyed)
            {
                throw new InvalidOperationException($"Change state of FSM {Name} failed. The FSM has already been destroyed.");
            }
            if (!CheckStarted())
            {
                throw new InvalidOperationException($"Change state of FSM {Name} failed. The FSM didn't start yet.");
            }
            if (_stateDict.TryGetValue(typeof(TState), out FSMState<T> state))
            {
                _currentState.OnExit(this);
                _currentState = state;
                _currentStateTime = 0;
                _currentState.OnEnter(this);
            }
            else
            {
                throw new ArgumentException($"Change state of FSM {Name} failed. State of type {typeof(TState).FullName} not found.", nameof(TState));
            }
        }

        public void ChangeState(Type stateType)
        {
            if (_isDestroyed)
            {
                throw new InvalidOperationException($"Change state of FSM {Name} failed. The FSM has already been destroyed.");
            }
            if (!CheckStarted())
            {
                throw new InvalidOperationException($"Change state of FSM {Name} failed. The FSM didn't start yet.");
            }
            CheckTypeCompilance(stateType);

            if (_stateDict.TryGetValue(stateType, out FSMState<T> state))
            {
                _currentState.OnExit(this);
                _currentState = state;
                _currentStateTime = 0;
                _currentState.OnEnter(this);
            }
            else
            {
                throw new ArgumentException($"Change state of FSM {Name} failed. State of type {stateType.FullName} not found.", nameof(stateType));
            }
        }

        public FSMState<T>[] GetAllStates()
        {
            if (_isDestroyed)
            {
                throw new InvalidOperationException($"Get all states of FSM {Name} failed. The FSM has already been destroyed.");
            }
            if (_stateDict.Count == 0)
            {
                return new FSMState<T>[0];
            }
            var result = new FSMState<T>[_stateDict.Count];
            _stateDict.Values.CopyTo(result, 0);
            return result;
        }

        public void Clear()
        {
            _stateDict.Clear();
            _name = null;
            _owner = null;
            _currentState = null;
            _currentStateTime = 0f;
        }

        private bool CheckStarted()
        {
            return _currentState != null;
        }

        private void CheckTypeCompilance(Type type)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type), $"Check type complience of FSM {Name} failed. State type cannot be null.");
            }
            if (!type.IsClass || type.IsAbstract)
            {
                throw new ArgumentException($"Check type complience of FSM {Name} failed. State type {type.FullName} must be a non-abstract class.", nameof(type));
            }
            if (!typeof(FSMState<T>).IsAssignableFrom(type))
            {
                throw new ArgumentException($"Check type complience of FSM {Name} failed. State type {type.FullName} must be a subclass of {typeof(FSMState<T>).Name}.", nameof(type));
            }
#endif
        }
    }
}