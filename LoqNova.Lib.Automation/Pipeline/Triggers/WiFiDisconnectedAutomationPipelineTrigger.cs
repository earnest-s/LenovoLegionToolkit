using System;
using System.Threading.Tasks;
using LoqNova.Lib.Automation.Resources;
using LoqNova.Lib.System;

namespace LoqNova.Lib.Automation.Pipeline.Triggers;

public class WiFiDisconnectedAutomationPipelineTrigger : IWiFiDisconnectedPipelineTrigger
{
    public string DisplayName => Resource.WiFiDisconnectedAutomationPipelineTrigger_DisplayName;

    public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        var result = automationEvent is WiFiAutomationEvent { IsConnected: false };
        return Task.FromResult(result);
    }

    public Task<bool> IsMatchingState()
    {
        var ssid = WiFi.GetConnectedNetworkSsid();
        return Task.FromResult(ssid is null);
    }

    public void UpdateEnvironment(AutomationEnvironment environment) => environment.WiFiConnected = false;

    public IAutomationPipelineTrigger DeepCopy() => new WiFiDisconnectedAutomationPipelineTrigger();

    public override bool Equals(object? obj) => obj is WiFiDisconnectedAutomationPipelineTrigger;

    public override int GetHashCode() => HashCode.Combine(DisplayName);
}
