// Clique com 2 dedos = botão direito para o touchpad Bosto (se anuncia como Apple 05AC:0265).
// O Windows usa o touchpad só como mouse; a coleção de touchpad (MI_01, report 0x0D) continua
// mandando os contatos, e é dela que vem a contagem de dedos.
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

static class Program {
  [STAThread]
  static void Main(string[] args) {
    using var mutex = new Mutex(true, @"Local\TwoFingerClick", out bool first);
    if (!first) return;
    Application.EnableVisualStyles();
    var settings = Settings.Load();
    if (Array.IndexOf(args, "--no-tap") >= 0) settings.TapEnabled = false;
    using var app = new TouchWindow(Array.IndexOf(args, "--log") >= 0, settings);
    Application.Run();
  }
}

sealed class TouchWindow : NativeWindow, IDisposable {
  // Report 0x0D: [0]=id, 5 contatos de 5 bytes a partir de [1] (flags, X LE16, Y LE16),
  // [26..27]=scan time, [28]=nº de contatos, [29] bit0 = botão físico.
  const int ReportId = 0x0D, ReportLen = 30, CountOffset = 28, ButtonOffset = 29;
  const string DeviceMatch = "vid_05ac&pid_0265&mi_01";

  public readonly Settings Settings;
  // Avisos para a janela Opções: mudança no nº de dedos e eventos (sessões, cliques trocados).
  public event Action<int> ContactsChanged;
  public event Action<string> Info;
  OptionsForm options;

  readonly StreamWriter log;
  readonly NotifyIcon tray;
  readonly LowLevelMouseProc hookProc;
  IntPtr hook;

  int contacts;
  long lastReport;
  bool rightHeld;
  long swallowUntil;

  // sessão de toque (de 0 dedos até voltar a 0)
  bool inSession;
  long sessionStart;
  int sessionMax;
  bool sessionButton;
  int moved;
  readonly int[] startX = new int[8], startY = new int[8];
  readonly bool[] seen = new bool[8];

  public TouchWindow(bool logging, Settings settings) {
    Settings = settings;
    if (logging) {
      var path = Path.Combine(AppContext.BaseDirectory, "TwoFingerClick.log");
      log = new StreamWriter(path, true) { AutoFlush = true };
    }
    CreateHandle(new CreateParams());
    var rid = new[] { new RAWINPUTDEVICE { UsagePage = 0x0D, Usage = 0x05, Flags = RIDEV_INPUTSINK, Target = Handle } };
    if (!RegisterRawInputDevices(rid, 1, Marshal.SizeOf<RAWINPUTDEVICE>()))
      Log("RegisterRawInputDevices falhou: " + Marshal.GetLastWin32Error());

    hookProc = HookCallback;
    hook = SetWindowsHookEx(WH_MOUSE_LL, hookProc, GetModuleHandle(null), 0);
    if (hook == IntPtr.Zero) Log("SetWindowsHookEx falhou: " + Marshal.GetLastWin32Error());

    var menu = new ContextMenuStrip();
    menu.Items.Add("Opções...", null, (s, e) => ShowOptions());
    menu.Items.Add(new ToolStripSeparator());
    menu.Items.Add("Sair", null, (s, e) => Application.Exit());
    tray = new NotifyIcon { Icon = TrayIcon.Create(), Text = "Clique com 2 dedos", ContextMenuStrip = menu, Visible = true };
    tray.DoubleClick += (s, e) => ShowOptions();
    Log($"iniciado (toque={Settings.TapEnabled})");
  }

  void ShowOptions() {
    if (options == null || options.IsDisposed) {
      options = new OptionsForm(this);
      options.Show();
    }
    options.WindowState = FormWindowState.Normal;
    options.Activate();
  }

  static long Now => Environment.TickCount64;

  void Log(string s) => log?.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {s}");

  // Registra e avisa a janela Opções; o aviso é postado para não atrasar o hook.
  void Event(string s) {
    Log(s);
    var o = options;
    if (Info != null && o != null && !o.IsDisposed) o.BeginInvoke(() => Info?.Invoke(s));
  }

  protected override void WndProc(ref Message m) {
    if (m.Msg == WM_INPUT) OnRawInput(m.LParam);
    base.WndProc(ref m);
  }

  readonly StringBuilder nameBuf = new StringBuilder(512);
  IntPtr knownDevice, otherDevice;

  bool IsTouchpad(IntPtr dev) {
    if (dev == knownDevice) return true;
    if (dev == otherDevice) return false;
    int n = nameBuf.Capacity; nameBuf.Clear();
    GetRawInputDeviceInfo(dev, RIDI_DEVICENAME, nameBuf, ref n);
    bool ok = nameBuf.ToString().ToLowerInvariant().Contains(DeviceMatch);
    if (ok) knownDevice = dev; else otherDevice = dev;
    return ok;
  }

  void OnRawInput(IntPtr hRaw) {
    int hs = Marshal.SizeOf<RAWINPUTHEADER>(), size = 0;
    GetRawInputData(hRaw, RID_INPUT, IntPtr.Zero, ref size, hs);
    var buf = Marshal.AllocHGlobal(size);
    try {
      if (GetRawInputData(hRaw, RID_INPUT, buf, ref size, hs) != size) return;
      var hdr = Marshal.PtrToStructure<RAWINPUTHEADER>(buf);
      if (hdr.Type != RIM_TYPEHID || !IsTouchpad(hdr.Device)) return;
      int each = Marshal.ReadInt32(buf, hs), count = Marshal.ReadInt32(buf, hs + 4);
      var data = new byte[each];
      for (int i = 0; i < count; i++) {
        Marshal.Copy(buf + hs + 8 + i * each, data, 0, each);
        if (each >= ReportLen && data[0] == ReportId) OnReport(data);
      }
    } finally { Marshal.FreeHGlobal(buf); }
  }

  void OnReport(byte[] r) {
    int n = r[CountOffset];
    bool button = (r[ButtonOffset] & 1) != 0;
    long now = Now;
    if (n != contacts) ContactsChanged?.Invoke(n);
    contacts = n;
    lastReport = now;

    if (n > 0 && !inSession) {
      inSession = true; sessionStart = now; sessionMax = 0; sessionButton = false; moved = 0;
      Array.Clear(seen);
    }
    if (!inSession) return;

    sessionMax = Math.Max(sessionMax, n);
    sessionButton |= button;
    for (int i = 0; i < 5; i++) {
      int o = 1 + i * 5, flags = r[o];
      if ((flags & 0x02) == 0) continue; // sem tip switch
      int id = (flags >> 2) & 7, x = r[o + 1] | r[o + 2] << 8, y = r[o + 3] | r[o + 4] << 8;
      if (!seen[id]) { seen[id] = true; startX[id] = x; startY[id] = y; }
      else moved = Math.Max(moved, Math.Abs(x - startX[id]) + Math.Abs(y - startY[id]));
    }

    if (n == 0) {
      inSession = false;
      long dur = now - sessionStart;
      Event($"sessão: {sessionMax} dedo(s), {dur} ms, mov {moved}{(sessionButton ? ", apertou" : "")}");
      if (Settings.TapEnabled && sessionMax == 2 && !sessionButton && dur <= Settings.TapMaxMs && moved <= Settings.TapMaxMove) {
        Event("toque com 2 dedos -> clique direito");
        swallowUntil = now + Settings.SwallowAfterTapMs;
        SendRight(true); SendRight(false);
      }
    }
  }

  IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam) {
    if (code >= 0) {
      var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
      bool injected = (info.Flags & LLMHF_INJECTED) != 0;
      int msg = (int)wParam;
      if (!injected && msg == WM_MOUSEMOVE && ScaleMove(info.Pt)) return (IntPtr)1;
      if (!injected && msg == WM_LBUTTONDOWN) {
        long now = Now;
        if (contacts >= 2 && now - lastReport <= Settings.StaleMs) {
          Event($"clique com {contacts} dedos -> botão direito");
          rightHeld = true;
          SendRight(true);
          return (IntPtr)1;
        }
        if (now <= swallowUntil) { Event("clique esquerdo do toque engolido"); swallowUntil = -1; return (IntPtr)1; }
      } else if (!injected && msg == WM_LBUTTONUP) {
        if (rightHeld) { rightHeld = false; SendRight(false); return (IntPtr)1; }
        if (swallowUntil == -1) { swallowUntil = 0; return (IntPtr)1; }
      }
    }
    return CallNextHookEx(hook, code, wParam, lParam);
  }

  // Velocidade só do touchpad: com dedos no touchpad, o movimento vem dele; multiplica o
  // deslocamento que o Windows já calculou (com a velocidade e a aceleração do sistema) e
  // move o cursor por SendInput absoluto. Sem dedos, o movimento passa intacto (outros mouses).
  double fracX, fracY;

  bool ScaleMove(POINT target) {
    double factor = Settings.TouchpadSpeed / 100.0;
    if (Math.Abs(factor - 1) < 0.001 || contacts < 1 || Now - lastReport > Settings.StaleMs) {
      fracX = fracY = 0;
      return false;
    }
    if (!GetCursorPos(out var cur)) return false;
    fracX += (target.X - cur.X) * factor;
    fracY += (target.Y - cur.Y) * factor;
    int dx = (int)Math.Truncate(fracX), dy = (int)Math.Truncate(fracY);
    fracX -= dx; fracY -= dy;
    if (dx == 0 && dy == 0) return true;

    int vx = GetSystemMetrics(SM_XVIRTUALSCREEN), vy = GetSystemMetrics(SM_YVIRTUALSCREEN);
    int vw = GetSystemMetrics(SM_CXVIRTUALSCREEN), vh = GetSystemMetrics(SM_CYVIRTUALSCREEN);
    int x = Math.Clamp(cur.X + dx, vx, vx + vw - 1), y = Math.Clamp(cur.Y + dy, vy, vy + vh - 1);
    var input = new INPUT { Type = 0, Mi = new MOUSEINPUT {
      Dx = (int)Math.Round((x - vx) * 65535.0 / Math.Max(1, vw - 1)),
      Dy = (int)Math.Round((y - vy) * 65535.0 / Math.Max(1, vh - 1)),
      Flags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK } };
    SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    return true;
  }

  static void SendRight(bool down) {
    var input = new INPUT { Type = 0, Mi = new MOUSEINPUT { Flags = down ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP } };
    SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
  }

  public void Dispose() {
    if (hook != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
    options?.Dispose();
    tray.Visible = false; tray.Dispose();
    log?.Dispose();
    DestroyHandle();
  }

  // --- Win32 ---
  const int WM_INPUT = 0x00FF, WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, WH_MOUSE_LL = 14;
  const int RIDEV_INPUTSINK = 0x100, RIM_TYPEHID = 2, LLMHF_INJECTED = 1;
  const uint RID_INPUT = 0x10000003, RIDI_DEVICENAME = 0x20000007;
  const uint MOUSEEVENTF_MOVE = 0x0001, MOUSEEVENTF_RIGHTDOWN = 0x0008, MOUSEEVENTF_RIGHTUP = 0x0010;
  const uint MOUSEEVENTF_VIRTUALDESK = 0x4000, MOUSEEVENTF_ABSOLUTE = 0x8000;
  const int SM_XVIRTUALSCREEN = 76, SM_YVIRTUALSCREEN = 77, SM_CXVIRTUALSCREEN = 78, SM_CYVIRTUALSCREEN = 79;

  delegate IntPtr LowLevelMouseProc(int code, IntPtr wParam, IntPtr lParam);

  [StructLayout(LayoutKind.Sequential)] struct RAWINPUTDEVICE { public ushort UsagePage, Usage; public int Flags; public IntPtr Target; }
  [StructLayout(LayoutKind.Sequential)] struct RAWINPUTHEADER { public int Type, Size; public IntPtr Device, WParam; }
  [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
  [StructLayout(LayoutKind.Sequential)] struct MSLLHOOKSTRUCT { public POINT Pt; public int MouseData, Flags, Time; public IntPtr ExtraInfo; }
  [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int Dx, Dy, MouseData; public uint Flags, Time; public IntPtr ExtraInfo; }
  [StructLayout(LayoutKind.Explicit)] struct INPUT { [FieldOffset(0)] public int Type; [FieldOffset(8)] public MOUSEINPUT Mi; }

  [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devices, int count, int size);
  [DllImport("user32.dll")] static extern int GetRawInputData(IntPtr hRaw, uint cmd, IntPtr data, ref int size, int headerSize);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetRawInputDeviceInfo(IntPtr dev, uint cmd, StringBuilder data, ref int size);
  [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWindowsHookEx(int id, LowLevelMouseProc proc, IntPtr mod, uint thread);
  [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
  [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
  [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] inputs, int size);
  [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
  [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
  [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
}
