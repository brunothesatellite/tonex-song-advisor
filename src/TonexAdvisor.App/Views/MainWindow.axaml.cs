using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace TonexAdvisor.App.Views;

public partial class MainWindow : Window
{
    // Windows uses different attribute numbers depending on the build; both mean
    // "paint the non-client caption bar with the dark system colour".
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;

    public MainWindow()
    {
        InitializeComponent();
        Opened += (_, _) => ApplyDarkTitleBar();
    }

    /// <summary>
    /// Keeps the OS caption bar in step with the dark rock theme instead of leaving the default
    /// bright system accent colour on top of it.
    /// </summary>
    private void ApplyDarkTitleBar()
    {
        var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero)
            return;

        var enabled = 1;
        if (DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref enabled, 4) != 0)
            DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkModeBefore20H1, ref enabled, 4);
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
