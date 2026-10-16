using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

// Ícone de touchpad desenhado em tempo de execução: retângulo arredondado com a divisória dos
// botões e dois pontos (os dois dedos). Branco na barra escura, cinza-escuro na clara.
static class TrayIcon {
  public static Icon Create() {
    var size = SystemInformation.SmallIconSize;
    int px = Math.Max(16, size.Width);
    using var bmp = new Bitmap(px, px);
    using (var g = Graphics.FromImage(bmp)) {
      g.SmoothingMode = SmoothingMode.AntiAlias;
      g.Clear(Color.Transparent);
      var ink = TaskbarIsLight() ? Color.FromArgb(0x20, 0x20, 0x20) : Color.White;
      float s = px / 16f, w = Math.Max(1.2f, 1.3f * s);
      var pad = new RectangleF(1.5f * s, 2.5f * s, 13f * s, 11f * s);
      using var pen = new Pen(ink, w);
      using var path = Rounded(pad, 2.2f * s);
      g.DrawPath(pen, path);
      // divisória dos botões
      float by = pad.Bottom - 3.2f * s;
      g.DrawLine(pen, pad.Left, by, pad.Right, by);
      g.DrawLine(pen, pad.Left + pad.Width / 2, by, pad.Left + pad.Width / 2, pad.Bottom);
      // dois dedos
      using var accent = new SolidBrush(Color.FromArgb(0xEB, 0x68, 0x34));
      float r = 1.6f * s, cy = pad.Top + (by - pad.Top) / 2;
      g.FillEllipse(accent, pad.Left + pad.Width * 0.36f - r, cy - r, 2 * r, 2 * r);
      g.FillEllipse(accent, pad.Left + pad.Width * 0.64f - r, cy - r, 2 * r, 2 * r);
    }
    var h = bmp.GetHicon();
    try { return (Icon)Icon.FromHandle(h).Clone(); } finally { DestroyIcon(h); }
  }

  static GraphicsPath Rounded(RectangleF r, float rad) {
    var p = new GraphicsPath(); float d = rad * 2;
    p.AddArc(r.Left, r.Top, d, d, 180, 90);
    p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
    p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
    p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
    p.CloseFigure();
    return p;
  }

  static bool TaskbarIsLight() {
    try {
      using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
      return k?.GetValue("SystemUsesLightTheme") is int v && v != 0;
    } catch (Exception) { return false; }
  }

  [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
}
