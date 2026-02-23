using System;
using System.Threading.Tasks;
using LoqNova.Lib.Automation.Resources;
using LoqNova.Lib.Listeners;
using Newtonsoft.Json;

namespace LoqNova.Lib.Automation.Pipeline.Triggers;

public class DisplayOnAutomationPipelineTrigger : INativeWindowsMessagePipelineTrigger, IDisallowDuplicatesAutomationPipelineTrigger
{
    [JsonIgnore]
    public string DisplayName => Resource.DisplayOnAutomationPipelineTrigger_DisplayName;

    public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        var result = automationEvent is NativeWindowsMessageEvent { Message: NativeWindowsMessage.MonitorOn };
        return Task.FromResult(result);
    }

    public Task<bool> IsMatchingState()
    {
        var listener = IoCContainer.Resolve<NativeWindowsMessageListener>();
        var result = listener.IsMonitorOn;
        return Task.FromResult(result);
    }

    public void UpdateEnvironment(AutomationEnvironment environment) => environment.DisplayOn = true;

    public IAutomationPipelineTrigger DeepCopy() => new DisplayOnAutomationPipelineTrigger();

    public override bool Equals(object? obj) => obj is DisplayOnAutomationPipelineTrigger;

    public override int GetHashCode() => HashCode.Combine(DisplayName);
}
