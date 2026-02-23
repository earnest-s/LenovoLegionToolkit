using System;
using System.Threading.Tasks;
using LoqNova.Lib.Automation.Resources;
using LoqNova.Lib.Listeners;
using Newtonsoft.Json;

namespace LoqNova.Lib.Automation.Pipeline.Triggers;

public class SessionLockAutomationPipelineTrigger : ISessionLockPipelineTrigger
{
    [JsonIgnore]
    public string DisplayName => Resource.SessionLockAutomationPipelineTrigger_DisplayName;

    public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        var result = automationEvent is SessionLockUnlockAutomationEvent { Locked: true };
        return Task.FromResult(result);
    }

    public Task<bool> IsMatchingState()
    {
        var listener = IoCContainer.Resolve<SessionLockUnlockListener>();
        var result = listener.IsLocked;
        return Task.FromResult(result.HasValue && result.Value);
    }

    public void UpdateEnvironment(AutomationEnvironment environment) => environment.SessionLocked = true;

    public IAutomationPipelineTrigger DeepCopy() => new SessionLockAutomationPipelineTrigger();

    public override bool Equals(object? obj) => obj is SessionLockAutomationPipelineTrigger;

    public override int GetHashCode() => HashCode.Combine(DisplayName);
}
