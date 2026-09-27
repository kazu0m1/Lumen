using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;

[assembly: AssemblyTitle("Lumen")]
[assembly: AssemblyDescription("Fast Windows photo culling viewer inspired by the Gwenview browsing experience")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("Lumen")]
[assembly: AssemblyCopyright("Copyright (c) 2026 kazu0m1")]
[assembly: AssemblyVersion("1.0.4.0")]
[assembly: AssemblyFileVersion("1.0.4.0")]
[assembly: ComVisible(false)]

namespace LumenApp
{
    internal sealed class App : Application
    {
    }

    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            App app = new App();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            MainWindow window = new MainWindow(args);
            app.Run(window);
        }
    }
}
