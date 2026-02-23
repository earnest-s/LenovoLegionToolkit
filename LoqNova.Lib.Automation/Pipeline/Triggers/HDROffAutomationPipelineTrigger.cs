using System;
using System.Threading.Tasks;
using LoqNova.Lib.Automation.Resources;
using LoqNova.Lib.Listeners;
using Newtonsoft.Json;

namespace LoqNova.Lib.Automation.Pipeline.Triggers;

public class HDROffAutomationPipelineTrigger : IHDRPipelineTrigger
{
    [JsonIgnore]
    public string DisplayName => Resource.HDROffAutomationPipelineTrigger_DisplayName;

    public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        var result = automationEvent is HDRAutomationEvent { IsHDROn: false };
        return Task.FromResult(result);
    }

    public Task<bool> IsMatchingState()
    {
        var listener = IoCContainer.Resolve<DisplayConfigurationListener>();
        var result = listener.IsHDROn;
        return Task.FromResult(result.HasValue && !result.Value);
    }

    public void UpdateEnvironment(AutomationEnvironment environment) => environment.HDROn = false;

    public IAutomationPipelineTrigger DeepCopy() => new HDROffAutomationPipelineTrigger();

    public override bool Equals(object? obj) => obj is HDROffAutomationPipelineTrigger;

    public override int GetHashCode() => HashCode.Combine(DisplayName);
}
