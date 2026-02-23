using Autofac;
using LoqNova.Lib.Extensions;
using LoqNova.Lib.Macro.Utils;

namespace LoqNova.Lib.Macro;

public class IoCModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.Register<MacroSettings>();
        builder.Register<MacroController>();
    }
}
