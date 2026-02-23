using Autofac;
using LoqNova.Lib.Extensions;
using LoqNova.WPF.CLI;
using LoqNova.WPF.Settings;
using LoqNova.WPF.Utils;

namespace LoqNova.WPF;

public class IoCModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.Register<MainThreadDispatcher>();

        builder.Register<SpectrumScreenCapture>();

        builder.Register<ThemeManager>().AutoActivate();
        builder.Register<NotificationsManager>().AutoActivate();

        builder.Register<DashboardSettings>();

        builder.Register<IpcServer>();
    }
}
