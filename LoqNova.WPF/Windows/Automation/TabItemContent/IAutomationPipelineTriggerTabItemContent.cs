using LoqNova.Lib.Automation.Pipeline.Triggers;

namespace LoqNova.WPF.Windows.Automation.TabItemContent;

public interface IAutomationPipelineTriggerTabItemContent<out T> where T : IAutomationPipelineTrigger
{
    T GetTrigger();
}
