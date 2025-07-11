using System;
using System.Collections;
using UnityEngine;
using XFramework.Utils;

namespace XFramework
{
    /// <summary>
    /// 流程管理器
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("XFramework/Procedure Manager")]
    public sealed class ProcedureManager : XFrameworkComponent
    {
        [SerializeField]
        private string[] _availableProcedureTypeNames;

        [SerializeField]
        private string _startupProcedureTypeName;

        private StateMachine<ProcedureManager> _procedureStateMachine;
        private ProcedureBase _startupProcedure;

        public ProcedureBase CurrentProcedure
        {
            get => _procedureStateMachine?.CurrentState as ProcedureBase;
        }

        public float CurrentProcedureTime
        {
            get => _procedureStateMachine == null ? 0 : _procedureStateMachine.CurrentStateTime;
        }

        internal override int Priority
        {
            get => XFrameworkConstant.ComponentPriority.ProcedureManager;
        }

        internal override void Init()
        {
            base.Init();

            ProcedureBase[] procedures = new ProcedureBase[_availableProcedureTypeNames.Length];
            // 注册所有流程为状态
            for (int i = 0; i < _availableProcedureTypeNames.Length; i++)
            {
                string typeName = _availableProcedureTypeNames[i];
                Type type = TypeHelper.GetType(typeName);
                if (type == null)
                {
                    Debug.LogError($"Can not find type {typeName}");
                    continue;
                }
                procedures[i] = Activator.CreateInstance(type) as ProcedureBase;
                if (typeName == _startupProcedureTypeName)
                {
                    _startupProcedure = procedures[i];
                }
            }

            if (_startupProcedure == null)
            {
                throw new InvalidOperationException("ProcedureManager init failed. Startup procedure is null.");
            }

            _procedureStateMachine = Global.StateMachineManager.Create(this, procedures);
            StartCoroutine(StartProcedureStateMachine());
        }

        internal override void Clear()
        {
            base.Clear();
            Global.StateMachineManager.Destroy<ProcedureManager>();
            _procedureStateMachine = null;
            _startupProcedure = null;
        }

        public T GetProcedure<T>() where T : ProcedureBase
        {
            return _procedureStateMachine.GetState<T>();
        }

        public bool HasProcedure<T>() where T : ProcedureBase
        {
            return _procedureStateMachine.HasState<T>();
        }

        /// <summary>
        /// 启动流程状态机
        /// </summary>
        private IEnumerator StartProcedureStateMachine()
        {
            // 等到帧末，确保所有必要组件都启动完毕
            yield return new WaitForEndOfFrame();
            _procedureStateMachine.Start(_startupProcedure.GetType());
        }
    }
}