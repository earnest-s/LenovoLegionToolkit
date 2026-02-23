using System.Collections.Generic;
using LoqNova.Lib.Automation.Pipeline;
using LoqNova.Lib.Automation.Pipeline.Triggers;
using LoqNova.Lib.Automation.Resources;
using LoqNova.Lib.Automation.Steps;
using LoqNova.Lib.Settings;

namespace LoqNova.Lib.Automation.Utils;

public class AutomationSettings() : AbstractSettings<AutomationSettings.AutomationSettingsStore>("automation.json")
{
    public class AutomationSettingsStore
    {
        public bool IsEnabled { get; set; }

        public List<AutomationPipeline> Pipelines { get; set; } = [];
    }

    protected override AutomationSettingsStore Default => new()
    {
        Pipelines =
        {
            new AutomationPipeline
            {
                Trigger = new ACAdapterConnectedAutomationPipelineTrigger(),
                Steps = { new PowerModeAutomationStep(PowerModeState.Balance) },
            },
            new AutomationPipeline
            {
                Trigger = new ACAdapterDisconnectedAutomationPipelineTrigger(),
                Steps = { new PowerModeAutomationStep(PowerModeState.Quiet) },
            },
            new AutomationPipeline
            {
                Name = Resource.DeactivateGpuQuickAction_Title,
                Steps = { new DeactivateGPUAutomationStep(DeactivateGPUAutomationStepState.KillApps) },
            },
        },
    };
}
