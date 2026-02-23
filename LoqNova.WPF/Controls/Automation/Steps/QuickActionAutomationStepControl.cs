using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LoqNova.Lib;
using LoqNova.Lib.Automation;
using LoqNova.Lib.Automation.Pipeline;
using LoqNova.Lib.Automation.Steps;
using LoqNova.WPF.Extensions;
using LoqNova.WPF.Resources;
using Wpf.Ui.Common;

namespace LoqNova.WPF.Controls.Automation.Steps;

public class QuickActionAutomationStepControl : AbstractAutomationStepControl<QuickActionAutomationStep>
{
    private readonly AutomationProcessor _processor = IoCContainer.Resolve<AutomationProcessor>();

    private readonly ComboBox _comboBox = new()
    {
        MinWidth = 150
    };

    private readonly StackPanel _stackPanel = new();

    public QuickActionAutomationStepControl(QuickActionAutomationStep step) : base(step)
    {
        Icon = SymbolRegular.Play24;
        Title = Resource.QuickActionAutomationStepControl_Title;
        Subtitle = Resource.QuickActionAutomationStepControl_Message;
    }

    public override IAutomationStep CreateAutomationStep()
    {
        return _comboBox.TryGetSelectedItem(out AutomationPipeline? pipeline)
            ? new QuickActionAutomationStep(pipeline?.Id)
            : new QuickActionAutomationStep(null);
    }

    protected override UIElement GetCustomControl()
    {
        _comboBox.SelectionChanged += (_, _) => { RaiseChanged(); };
        _stackPanel.Children.Add(_comboBox);
        return _stackPanel;
    }

    protected override void OnFinishedLoading() { }

    protected override async Task RefreshAsync()
    {
        var pipelines = await _processor.GetPipelinesAsync();
        var filteredPipelines = pipelines.Where(p => p.Trigger is null).ToArray();
        var selectedPipeline = filteredPipelines.FirstOrDefault(p => p.Id == AutomationStep.PipelineId);

        _comboBox.SetItems(filteredPipelines, selectedPipeline, p => p?.Name ?? "");
    }
}
