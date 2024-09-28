namespace XFramework
{
    /// <summary>
    /// 所有流程的基类
    /// </summary>
    /// <remarks>
    /// 流程实际上就是一系列的状态。
    /// </remarks>
    public abstract class Procedure : FsmState<ProcedureManager>
    {
        /// <summary>
        /// 流程初始化时
        /// </summary>
        /// <param name="fsm">流程管理器实例</param>
        public override void OnInit(Fsm<ProcedureManager> fsm)
        {
        }

        /// <summary>
        /// 进入流程时
        /// </summary>
        /// <param name="fsm">流程管理器实例</param>
        public override void OnEnter(Fsm<ProcedureManager> fsm)
        {
        }

        /// <summary>
        /// 离开流程时
        /// </summary>
        /// <param name="fsm">流程管理器实例</param>
        public override void OnExit(Fsm<ProcedureManager> fsm)
        {
        }

        /// <summary>
        /// 流程销毁时
        /// </summary>
        /// <param name="fsm">流程管理器实例</param>
        public override void OnDestroy(Fsm<ProcedureManager> fsm)
        {
        }

        /// <summary>
        /// 流程更新时
        /// </summary>
        /// <param name="fsm">流程管理器实例</param>
        /// <param name="logicSeconds">逻辑时间</param>
        /// <param name="realSeconds">真实时间</param>
        public override void OnUpdate(Fsm<ProcedureManager> fsm, float logicSeconds, float realSeconds)
        {
        }
    }
}