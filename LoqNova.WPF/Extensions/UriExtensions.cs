using System;
using System.Diagnostics;

namespace LoqNova.WPF.Extensions;

public static class UriExtensions
{
    public static void Open(this Uri uri) => Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
}
