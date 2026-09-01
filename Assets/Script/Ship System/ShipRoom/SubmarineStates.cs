using UnityEngine;
using PressureExpress.Framework;

public abstract class SubmarineStateBase : IState
{
    protected SubmarineManager submarine;

    protected SubmarineStateBase(SubmarineManager submarine)
    {
        this.submarine = submarine;
    }

    public virtual void Enter() {}
    public virtual void OnUpdate() {}
    public virtual void OnFixedUpdate() {}
    public virtual void Exit() {}

    protected void CheckForStateTransitions()
    {
        if (!submarine.IsServer) return;

        // If a game-over critical condition is met, transition to critical state
        if (submarine.IsCriticalConditionActive())
        {
            submarine.ChangeSubmarineState(StateMachineFactory.SubmarineStateTypes.Critical);
            return;
        }

        // Check alerts
        bool warningsActive = submarine.IsAnyWarningActive();
        if (warningsActive && this is SubmarineNormalState)
        {
            submarine.ChangeSubmarineState(StateMachineFactory.SubmarineStateTypes.Alert);
        }
        else if (!warningsActive && this is SubmarineAlertState)
        {
            submarine.ChangeSubmarineState(StateMachineFactory.SubmarineStateTypes.Normal);
        }
    }
}

public class SubmarineNormalState : SubmarineStateBase
{
    public SubmarineNormalState(SubmarineManager submarine) : base(submarine) {}

    public override void Enter()
    {
        if (submarine.IsServer)
        {
            submarine.isInCriticalState.Value = false;
        }
    }

    public override void OnUpdate()
    {
        CheckForStateTransitions();
    }
}

public class SubmarineAlertState : SubmarineStateBase
{
    public SubmarineAlertState(SubmarineManager submarine) : base(submarine) {}

    public override void Enter()
    {
        if (submarine.IsServer)
        {
            submarine.isInCriticalState.Value = false;
        }
    }

    public override void OnUpdate()
    {
        CheckForStateTransitions();
    }
}

public class SubmarineCriticalState : SubmarineStateBase
{
    private float simulationAccumulator = 0f;

    public SubmarineCriticalState(SubmarineManager submarine) : base(submarine) {}

    public override void Enter()
    {
        if (submarine.IsServer)
        {
            submarine.isInCriticalState.Value = true;
            submarine.SetCriticalTimer(0f);
            submarine.SetFailureReason(submarine.GetCriticalFailureReason());
        }
    }

    public override void OnUpdate()
    {
        if (!submarine.IsCriticalConditionActive())
        {
            if (submarine.IsAnyWarningActive())
            {
                submarine.ChangeSubmarineState(StateMachineFactory.SubmarineStateTypes.Alert);
            }
            else
            {
                submarine.ChangeSubmarineState(StateMachineFactory.SubmarineStateTypes.Normal);
            }
        }
    }

    public override void OnFixedUpdate()
    {
        if (!submarine.IsServer) return;

        simulationAccumulator += Time.fixedDeltaTime;
        if (simulationAccumulator >= submarine.SimulationInterval)
        {
            submarine.IncrementCriticalTimer(submarine.SimulationInterval);
            if (submarine.GetCriticalTimer() >= submarine.gameOverDelay)
            {
                submarine.TriggerGameOverServer();
            }
            simulationAccumulator = 0f;
        }
    }
}

public class SubmarineGameOverState : SubmarineStateBase
{
    public SubmarineGameOverState(SubmarineManager submarine) : base(submarine) {}

    public override void Enter()
    {
        if (submarine.IsServer)
        {
            submarine.isInCriticalState.Value = true;
        }
    }
}
