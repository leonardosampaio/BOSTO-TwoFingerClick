using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

// Apps com janela aberta (para a lista da aba Deslizar) e o app em primeiro plano.
// Os apps são identificados pelo nome do processo, em minúsculas e sem ".exe".
static class Apps {
  public sealed record Window(string Process, string Title);

  public static List<Window> Open() {
    var found = new Dictionary<string, Window>();
    var self = Environment.ProcessId;
    EnumWindows((h, _) => {
      if (!IsWindowVisible(h) || GetWindow(h, GW_OWNER) != IntPtr.Zero) return true;
      if ((GetWindowLongPtr(h, GWL_EXSTYLE).ToInt64() & WS_EX_TOOLWINDOW) != 0) return true;
      if (DwmGetWindowAttribute(h, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return true;
      int len = GetWindowTextLength(h);
      if (len == 0) return true;
      var sb = new StringBuilder(len + 1);
      GetWindowText(h, sb, sb.Capacity);
      GetWindowThreadProcessId(h, out uint pid);
      if (pid == self) return true;
      var name = ProcessName(pid);
      if (name != null && !found.ContainsKey(name)) found[name] = new Window(name, sb.ToString());
      return true;
    }, IntPtr.Zero);
    var list = new List<Window>(found.Values);
    list.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase));
    return list;
  }

  public static string Foreground() {
    var h = GetForegroundWindow();
    if (h == IntPtr.Zero) return null;
    GetWindowThreadProcessId(h, out uint pid);
    return ProcessName(pid);
  }

  static string ProcessName(uint pid) {
    try { using var p = Process.GetProcessById((int)pid); return p.ProcessName.ToLowerInvariant(); }
    catch (Exception) { return null; }
  }

  const int GW_OWNER = 4, GWL_EXSTYLE = -20, DWMWA_CLOAKED = 14;
  const long WS_EX_TOOLWINDOW = 0x80;
  delegate bool EnumWindowsProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc cb, IntPtr l);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] static extern IntPtr GetWindowLongPtr(IntPtr h, int index);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int max);
  [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr h);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
  [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int value, int size);
}
