using Autofac;
using LoqNova.Lib.Automation.Utils;
using LoqNova.Lib.Extensions;

namespace LoqNova.Lib.Automation;

public class IoCModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.Register<AutomationSettings>();
        builder.Register<AutomationProcessor>();
    }
}
