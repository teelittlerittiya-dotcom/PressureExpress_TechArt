namespace PressureExpress.Framework
{
    public class StateMachine
    {
        public IState CurrentState { get; private set; }

        public void ChangeState(IState newState)
        {
            CurrentState?.Exit();
            CurrentState = newState;
            CurrentState?.Enter();
        }

        public void OnUpdate()
        {
            CurrentState?.OnUpdate();
        }

        public void OnFixedUpdate()
        {
            CurrentState?.OnFixedUpdate();
        }
    }
}
