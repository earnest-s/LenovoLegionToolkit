using System.Threading;
using System.Threading.Tasks;
using LoqNova.Lib.Messaging;
using LoqNova.Lib.Messaging.Messages;
using Newtonsoft.Json;

namespace LoqNova.Lib.Automation.Steps;

[method: JsonConstructor]
public class NotificationAutomationStep(string? text)
    : IAutomationStep
{
    public string? Text { get; } = text;

    public Task<bool> IsSupportedAsync() => Task.FromResult(true);

    public Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token)
    {
        if (!string.IsNullOrWhiteSpace(Text))
        {
            var text = Text.Replace("$RUN_OUTPUT$", context.LastRunOutput);
            MessagingCenter.Publish(new NotificationMessage(NotificationType.AutomationNotification, text));
        }

        return Task.CompletedTask;
    }

    IAutomationStep IAutomationStep.DeepCopy() => new NotificationAutomationStep(Text);
}
