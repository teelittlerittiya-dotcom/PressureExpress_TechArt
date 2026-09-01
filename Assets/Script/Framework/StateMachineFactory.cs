using System;

namespace PressureExpress.Framework
{
    public static class StateMachineFactory
    {
        public enum SubmarineStateTypes
        {
            Normal,
            Alert,
            Critical,
            GameOver
        }
        public static StateMachine Create()
        {
            return new StateMachine();
        }
    }
}
