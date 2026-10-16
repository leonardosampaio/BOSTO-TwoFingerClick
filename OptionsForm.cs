using System;
using System.Drawing;
using System.Windows.Forms;

// Janela Opções: parâmetros de tempo, velocidade do cursor e área para testar os cliques.
// As mudanças valem na hora (para testar); OK grava, Cancelar desfaz.
sealed class OptionsForm : Form {
  readonly TouchWindow app;
  readonly Settings original;
  bool saved;

  readonly CheckBox tap = new CheckBox { Text = "Toque com 2 dedos = clique direito", AutoSize = true };
  readonly NumericUpDown tapMaxMs = Num(50, 1000, 10);
  readonly NumericUpDown tapMaxMove = Num(10, 1000, 10);
  readonly NumericUpDown staleMs = Num(50, 2000, 10);
  readonly NumericUpDown swallowMs = Num(0, 1000, 10);
  readonly TrackBar speed = new TrackBar { Minimum = 5, Maximum = 60, TickFrequency = 5, SmallChange = 1, LargeChange = 5, Width = 260, AutoSize = false, Height = 32 };
  readonly Label speedValue = new Label { AutoSize = true, Anchor = AnchorStyles.Left };
  readonly Label fingers = new Label { AutoSize = true, Text = "Dedos no touchpad: 0" };
  readonly Label testArea = new Label {
    Text = "Clique aqui para testar", TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill,
    BorderStyle = BorderStyle.FixedSingle, BackColor = SystemColors.Window, Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 11f),
  };
  readonly ListBox events = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };

  static NumericUpDown Num(int min, int max, int step) =>
    new NumericUpDown { Minimum = min, Maximum = max, Increment = step, Width = 80, Anchor = AnchorStyles.Left };

  public OptionsForm(TouchWindow app) {
    this.app = app;
    original = app.Settings.Clone();

    Text = "Clique com 2 dedos - Opções";
    Font = SystemFonts.MessageBoxFont;
    FormBorderStyle = FormBorderStyle.Sizable;
    MinimizeBox = MaximizeBox = false;
    StartPosition = FormStartPosition.CenterScreen;
    AutoScaleMode = AutoScaleMode.Dpi;
    ClientSize = new Size(520, 640);
    MinimumSize = new Size(480, 560);
    Icon = TrayIcon.Create();

    var s = app.Settings;
    tap.Checked = s.TapEnabled;
    tapMaxMs.Value = Clamp(tapMaxMs, s.TapMaxMs);
    tapMaxMove.Value = Clamp(tapMaxMove, s.TapMaxMove);
    staleMs.Value = Clamp(staleMs, s.StaleMs);
    swallowMs.Value = Clamp(swallowMs, s.SwallowAfterTapMs);
    speed.Value = Math.Clamp(s.TouchpadSpeed / 5, speed.Minimum, speed.Maximum);
    speedValue.Text = $"{speed.Value * 5}%";

    var timing = Grid();
    timing.Controls.Add(tap, 0, 0); timing.SetColumnSpan(tap, 3);
    Row(timing, 1, "Duração máxima do toque", tapMaxMs, "ms");
    Row(timing, 2, "Movimento máximo do toque", tapMaxMove, "unidades (~0–4096)");
    Row(timing, 3, "Validade da contagem de dedos", staleMs, "ms");
    Row(timing, 4, "Ignorar clique esquerdo após toque", swallowMs, "ms");
    var timingBox = Box("Tempos", timing);

    var cursor = Grid();
    cursor.Controls.Add(new Label { Text = "Velocidade", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
    cursor.Controls.Add(speed, 1, 0);
    cursor.Controls.Add(speedValue, 2, 0);
    var note = new Label { Text = "Só para o touchpad, sobre a velocidade do Windows. Outros mouses não mudam.", AutoSize = true, ForeColor = SystemColors.GrayText };
    cursor.Controls.Add(note, 0, 1); cursor.SetColumnSpan(note, 3);
    var cursorBox = Box("Cursor do touchpad", cursor);

    var test = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(6) };
    test.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    test.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
    test.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
    test.Controls.Add(fingers, 0, 0);
    test.Controls.Add(testArea, 0, 1);
    test.Controls.Add(events, 0, 2);
    var testBox = new GroupBox { Text = "Teste", Dock = DockStyle.Fill, Padding = new Padding(6) };
    testBox.Controls.Add(test);

    var ok = new Button { Text = "OK", AutoSize = true, DialogResult = DialogResult.OK };
    var cancel = new Button { Text = "Cancelar", AutoSize = true, DialogResult = DialogResult.Cancel };
    var defaults = new Button { Text = "Restaurar padrões", AutoSize = true };
    var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
    buttons.Controls.AddRange(new Control[] { cancel, ok, defaults });
    AcceptButton = ok; CancelButton = cancel;

    var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(10) };
    root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
    root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    root.Controls.Add(timingBox, 0, 0);
    root.Controls.Add(cursorBox, 0, 1);
    root.Controls.Add(testBox, 0, 2);
    root.Controls.Add(buttons, 0, 3);
    Controls.Add(root);

    foreach (var n in new[] { tapMaxMs, tapMaxMove, staleMs, swallowMs }) n.ValueChanged += (_, _) => Apply();
    tap.CheckedChanged += (_, _) => Apply();
    speed.ValueChanged += (_, _) => { speedValue.Text = $"{speed.Value * 5}%"; Apply(); };

    defaults.Click += (_, _) => {
      var d = new Settings();
      tap.Checked = d.TapEnabled;
      tapMaxMs.Value = d.TapMaxMs; tapMaxMove.Value = d.TapMaxMove;
      staleMs.Value = d.StaleMs; swallowMs.Value = d.SwallowAfterTapMs;
      speed.Value = d.TouchpadSpeed / 5;
    };
    ok.Click += (_, _) => {
      Apply();
      app.Settings.Save();
      saved = true;
      Close();
    };
    cancel.Click += (_, _) => Close();

    testArea.MouseDown += (_, e) => {
      testArea.Text = e.Button switch {
        MouseButtons.Left => "Botão esquerdo",
        MouseButtons.Right => "Botão direito",
        MouseButtons.Middle => "Botão do meio",
        _ => e.Button.ToString(),
      };
      testArea.BackColor = e.Button == MouseButtons.Right ? Color.FromArgb(0xEB, 0x68, 0x34) : Color.FromArgb(0x2A, 0x78, 0xD6);
      testArea.ForeColor = Color.White;
      AddEvent("teste: " + testArea.Text.ToLowerInvariant());
    };
    testArea.MouseUp += (_, _) => { testArea.BackColor = SystemColors.Window; testArea.ForeColor = SystemColors.ControlText; };
    // Sem menu de contexto na área de teste: o clique direito só pinta a área.
    testArea.ContextMenuStrip = new ContextMenuStrip();
    testArea.ContextMenuStrip.Opening += (_, e) => e.Cancel = true;

    app.ContactsChanged += OnContacts;
    app.Info += AddEvent;
  }

  static decimal Clamp(NumericUpDown n, int v) => Math.Clamp(v, n.Minimum, n.Maximum);

  static TableLayoutPanel Grid() {
    var g = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true, Padding = new Padding(6, 2, 6, 6) };
    g.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
    g.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
    g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
    return g;
  }

  static void Row(TableLayoutPanel g, int row, string label, Control input, string unit) {
    g.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
    g.Controls.Add(input, 1, row);
    g.Controls.Add(new Label { Text = unit, AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = SystemColors.GrayText }, 2, row);
  }

  static GroupBox Box(string title, Control content) {
    var b = new GroupBox { Text = title, Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(6) };
    b.Controls.Add(content);
    return b;
  }

  void Apply() {
    var s = app.Settings;
    s.TapEnabled = tap.Checked;
    s.TapMaxMs = (int)tapMaxMs.Value;
    s.TapMaxMove = (int)tapMaxMove.Value;
    s.StaleMs = (int)staleMs.Value;
    s.SwallowAfterTapMs = (int)swallowMs.Value;
    s.TouchpadSpeed = speed.Value * 5;
  }

  void OnContacts(int n) => fingers.Text = $"Dedos no touchpad: {n}";

  void AddEvent(string s) {
    events.Items.Insert(0, $"{DateTime.Now:HH:mm:ss}  {s}");
    while (events.Items.Count > 200) events.Items.RemoveAt(events.Items.Count - 1);
  }

  protected override void OnFormClosed(FormClosedEventArgs e) {
    app.ContactsChanged -= OnContacts;
    app.Info -= AddEvent;
    if (!saved) app.Settings.CopyFrom(original);
    base.OnFormClosed(e);
  }
}
