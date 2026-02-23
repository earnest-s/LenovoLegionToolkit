using System;
using Humanizer;
using LoqNova.Lib.Automation.Pipeline.Triggers;
using LoqNova.WPF.Extensions;

namespace LoqNova.WPF.Windows.Automation.TabItemContent;

public partial class UserInactivityPipelineTriggerTabItemContent : IAutomationPipelineTriggerTabItemContent<IUserInactivityPipelineTrigger>
{
    private static readonly TimeSpan[] TimeSpans =
    [
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(3),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(30)
    ];

    private readonly IUserInactivityPipelineTrigger _trigger;

    public UserInactivityPipelineTriggerTabItemContent(IUserInactivityPipelineTrigger trigger)
    {
        _trigger = trigger;
        InitializeComponent();

        _timeoutComboBox.SetItems(TimeSpans, trigger.InactivityTimeSpan, t => t.Humanize());
    }

    public IUserInactivityPipelineTrigger GetTrigger()
    {
        var state = _timeoutComboBox.TryGetSelectedItem(out TimeSpan tt)
            ? tt
            : TimeSpan.FromSeconds(30);
        return _trigger.DeepCopy(state);
    }
}
