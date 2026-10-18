using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.ViewModels;
namespace Slx.Client.Desktop.Presentation;
internal sealed class TrayIcon : IDisposable
{
    private const uint CallbackMessage = 0x8001;
    private readonly MainWindow window;
    private readonly MainViewModel model;
    private readonly HwndSource source;
    private NotifyData data;
    private readonly uint taskbarCreated;
    private bool disposed;
    public bool IsAvailable { get; private set; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyData
    {
        public uint Size; public nint Window; public uint Id; public uint Flags; public uint Callback;
        public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State; public uint StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Timeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags; public Guid Guid; public nint BalloonIcon;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Shell_NotifyIcon(uint operation, ref NotifyData data);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string path, int index, out nint large, out nint small, uint count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string message);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyIcon(nint icon);
    public TrayIcon(MainWindow window, MainViewModel model)
    {
        this.window = window; this.model = model;
        var handle = new WindowInteropHelper(window).Handle;
        source = HwndSource.FromHwnd(handle);
        source.AddHook(WndProc);
        taskbarCreated = RegisterWindowMessage("TaskbarCreated");
        ExtractIconEx(Environment.ProcessPath!, 0, out var large, out var small, 1);
        if (large != 0) DestroyIcon(large);
        data = new NotifyData { Size = (uint)Marshal.SizeOf<NotifyData>(), Window = handle, Id = 1,
            Flags = 1 | 2 | 4, Callback = CallbackMessage, Icon = small, Tip = "SLX VPN", Info = "", InfoTitle = "" };
        IsAvailable = Shell_NotifyIcon(0, ref data);
    }
    private nint WndProc(nint handle, int message, nint wParam, nint lParam, ref bool handled)
    {
        if ((uint)message == taskbarCreated) IsAvailable = Shell_NotifyIcon(0, ref data);
        if ((uint)message != CallbackMessage) return 0;
        if ((int)lParam is 0x202 or 0x203) window.Restore();
        else if ((int)lParam == 0x205) ShowMenu();
        handled = true; return 0;
    }
    private void ShowMenu()
    {
        var menu = new ContextMenu { Placement = PlacementMode.MousePoint, PlacementTarget = window };
        menu.SetResourceReference(Control.BackgroundProperty, "Surface");
        menu.SetResourceReference(Control.ForegroundProperty, "Text");
        menu.SetResourceReference(Control.BorderBrushProperty, "Border");
        Add(menu, Text.Catalog["TrayOpen"], () => window.Restore());
        Add(menu, Text.Catalog["Reconnect"], () => model.ReconnectCommand.Execute(null), model.CanReconnect);
        Add(menu, Text.Catalog["Disconnect"], () => model.ToggleConnectionCommand.Execute(null), model.IsTunnelActive);
        menu.Items.Add(new Separator());
        Add(menu, Text.Catalog["TrayExit"], () => _ = window.ExitAsync());
        window.Activate(); menu.IsOpen = true;
    }
    private static void Add(ContextMenu menu, string label, Action action, bool enabled = true)
    {
        var item = new MenuItem { Header = label, IsEnabled = enabled };
        item.SetResourceReference(Control.ForegroundProperty, "Text");
        item.SetResourceReference(Control.BackgroundProperty, "Surface");
        item.Click += (_, _) => action(); menu.Items.Add(item);
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Shell_NotifyIcon(2, ref data); source.RemoveHook(WndProc);
        if (data.Icon != 0) DestroyIcon(data.Icon);
    }
}
