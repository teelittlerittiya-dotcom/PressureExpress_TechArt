namespace PressureExpress.Framework
{
    public interface IState
    {
        void Enter();
        void OnUpdate();
        void OnFixedUpdate();
        void Exit();
    }
}
