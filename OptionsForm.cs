using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

// Janela Opções, em abas: Cliques (ação de cada gesto, tempos, cursor), Deslizar (voltar/avançar
// simulado por app) e Teste. As mudanças valem na hora (para testar); OK grava, Cancelar desfaz.
sealed class OptionsForm : Form {
  readonly TouchWindow app;
  readonly Settings original;
  bool saved, loading = true;

  readonly ComboBox twoClick = Actions(false), threeClick = Actions(false), twoTap = Actions(true), threeTap = Actions(true);
  readonly NumericUpDown tapMaxMs = Num(50, 1000, 10);
  readonly NumericUpDown tapMaxMove = Num(10, 1000, 10);
  readonly NumericUpDown staleMs = Num(50, 2000, 10);
  readonly NumericUpDown swallowMs = Num(0, 1000, 10);
  readonly TrackBar speed = new TrackBar { Minimum = 5, Maximum = 60, TickFrequency = 5, SmallChange = 1, LargeChange = 5, Width = 260, AutoSize = false, Height = 32 };
  readonly Label speedValue = new Label { AutoSize = true, Anchor = AnchorStyles.Left };

  readonly CheckedListBox swipeApps = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false };
  readonly ComboBox swipeMethod = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260, Anchor = AnchorStyles.Left };
  readonly CheckBox swipeInvert = new CheckBox { Text = "Inverter (dedos para a direita = avançar)", AutoSize = true };
  readonly NumericUpDown swipeMin = Num(100, 3000, 50);

  readonly Label fingers = new Label { AutoSize = true, Text = "Dedos no touchpad: 0", Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 24, 3) };
  readonly CheckBox showAll = new CheckBox { Text = "Mostrar sessões de toque e eventos em outros apps", AutoSize = true, Checked = false };
  readonly Label testArea = new Label {
    Text = "Clique aqui para testar", TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill,
    BorderStyle = BorderStyle.FixedSingle, BackColor = SystemColors.Window, Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 11f),
  };
  readonly ListBox events = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
  // Todos os eventos (os 200 últimos, mais novo primeiro); a lista mostra só os que passam no filtro.
  sealed record EventLine(string Text, bool Session, bool Outside) { public override string ToString() => Text; }
  readonly List<EventLine> allEvents = new();

  // Itens da lista: no clique, "Left" é deixar o clique esquerdo passar; no toque, não há clique
  // original, então "nada" é a opção de desligar e "botão esquerdo" vira um clique esquerdo.
  sealed record Item(ClickAction Action, string Text) { public override string ToString() => Text; }
  sealed record AppItem(string Process, string Title, bool Running) {
    public override string ToString() => Running ? $"{Title}  ({Process}.exe)" : $"{Process}.exe  (fechado)";
  }

  static ComboBox Actions(bool tap) {
    var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220, Anchor = AnchorStyles.Left };
    foreach (ClickAction a in Enum.GetValues<ClickAction>()) {
      if (!tap && a == ClickAction.None) continue;
      string text;
      if (!tap && a == ClickAction.Left) text = "Clique esquerdo (sem mudança)";
      else if (a == ClickAction.None) text = "Nada";
      else { var n = TouchWindow.Name(a); text = char.ToUpper(n[0]) + n[1..]; }
      c.Items.Add(new Item(a, text));
    }
    return c;
  }

  static void Select(ComboBox c, ClickAction a) {
    foreach (Item i in c.Items) if (i.Action == a) { c.SelectedItem = i; return; }
    c.SelectedIndex = 0;
  }

  static ClickAction Selected(ComboBox c) => ((Item)c.SelectedItem).Action;

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
    ClientSize = new Size(560, 600);
    MinimumSize = new Size(520, 560);
    Icon = TrayIcon.Create();

    swipeMethod.Items.AddRange(new object[] { "Botões voltar/avançar do mouse", "Teclado: Alt+← / Alt+→" });
    LoadSettings(app.Settings);

    var tabs = new TabControl { Dock = DockStyle.Fill };
    tabs.TabPages.Add(Page("Cliques", ClicksPage()));
    tabs.TabPages.Add(Page("Deslizar", SwipePage()));
    tabs.TabPages.Add(Page("Teste", TestPage()));

    var ok = new Button { Text = "OK", AutoSize = true };
    var cancel = new Button { Text = "Cancelar", AutoSize = true };
    var defaults = new Button { Text = "Restaurar padrões", AutoSize = true };
    var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
    buttons.Controls.AddRange(new Control[] { cancel, ok, defaults });
    AcceptButton = ok; CancelButton = cancel;

    var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(10) };
    root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
    root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    root.Controls.Add(tabs, 0, 0);
    root.Controls.Add(buttons, 0, 1);
    Controls.Add(root);

    foreach (var n in new[] { tapMaxMs, tapMaxMove, staleMs, swallowMs, swipeMin }) n.ValueChanged += (_, _) => Apply();
    foreach (var c in new[] { twoClick, threeClick, twoTap, threeTap, swipeMethod }) c.SelectedIndexChanged += (_, _) => Apply();
    swipeInvert.CheckedChanged += (_, _) => Apply();
    speed.ValueChanged += (_, _) => { speedValue.Text = $"{speed.Value * 5}%"; Apply(); };
    // ItemCheck chega antes de a marca mudar: aplica depois.
    swipeApps.ItemCheck += (_, _) => BeginInvoke(new Action(Apply));

    // padrões em tudo, menos na lista de apps marcados
    defaults.Click += (_, _) => LoadSettings(new Settings { SwipeApps = CheckedApps().ToList() });
    ok.Click += (_, _) => {
      Apply();
      app.Settings.Save();
      saved = true;
      Close();
    };
    cancel.Click += (_, _) => Close();

    app.ContactsChanged += OnContacts;
    app.Info += AddEvent;
    loading = false;
  }

  void LoadSettings(Settings s) {
    loading = true;
    Select(twoClick, s.TwoFingerClick); Select(threeClick, s.ThreeFingerClick);
    Select(twoTap, s.TwoFingerTap); Select(threeTap, s.ThreeFingerTap);
    tapMaxMs.Value = Clamp(tapMaxMs, s.TapMaxMs);
    tapMaxMove.Value = Clamp(tapMaxMove, s.TapMaxMove);
    staleMs.Value = Clamp(staleMs, s.StaleMs);
    swallowMs.Value = Clamp(swallowMs, s.SwallowAfterTapMs);
    speed.Value = Math.Clamp(s.TouchpadSpeed / 5, speed.Minimum, speed.Maximum);
    speedValue.Text = $"{speed.Value * 5}%";
    swipeMethod.SelectedIndex = s.SwipeMethod == SwipeMethod.AltArrows ? 1 : 0;
    swipeInvert.Checked = s.SwipeInvert;
    swipeMin.Value = Clamp(swipeMin, s.SwipeMinUnits);
    FillApps(s.SwipeApps);
    loading = false;
    Apply();
  }

  // Apps abertos + os marcados que estão fechados; mantém as marcas.
  void FillApps(IEnumerable<string> checkedApps) {
    var marked = new HashSet<string>(checkedApps);
    var open = Apps.Open();
    swipeApps.BeginUpdate();
    swipeApps.Items.Clear();
    foreach (var w in open) swipeApps.Items.Add(new AppItem(w.Process, w.Title, true), marked.Contains(w.Process));
    foreach (var p in marked.Where(p => open.All(w => w.Process != p)).OrderBy(p => p))
      swipeApps.Items.Add(new AppItem(p, p, false), true);
    swipeApps.EndUpdate();
  }

  IEnumerable<string> CheckedApps() => swipeApps.CheckedItems.Cast<AppItem>().Select(i => i.Process);

  static TabPage Page(string title, Control content) {
    var p = new TabPage(title) { Padding = new Padding(6), UseVisualStyleBackColor = true };
    p.Controls.Add(content);
    return p;
  }

  Control ClicksPage() {
    var actions = Grid();
    Row(actions, 0, "Clique com 2 dedos", twoClick, "");
    Row(actions, 1, "Clique com 3 dedos", threeClick, "");
    Row(actions, 2, "Toque com 2 dedos", twoTap, "");
    Row(actions, 3, "Toque com 3 dedos", threeTap, "");

    var timing = Grid();
    Row(timing, 0, "Duração máxima do toque", tapMaxMs, "ms");
    Row(timing, 1, "Movimento máximo do toque", tapMaxMove, "unidades (~0–4096)");
    Row(timing, 2, "Validade da contagem de dedos", staleMs, "ms");
    Row(timing, 3, "Ignorar clique esquerdo após toque", swallowMs, "ms");

    var cursor = Grid();
    cursor.Controls.Add(new Label { Text = "Velocidade", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
    cursor.Controls.Add(speed, 1, 0);
    cursor.Controls.Add(speedValue, 2, 0);
    var note = Note("Só para o touchpad, sobre a velocidade do Windows. Outros mouses não mudam.");
    cursor.Controls.Add(note, 0, 1); cursor.SetColumnSpan(note, 3);

    var page = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoScroll = true };
    page.Controls.Add(Box("Ações", actions));
    page.Controls.Add(Box("Tempos", timing));
    page.Controls.Add(Box("Cursor do touchpad", cursor));
    return page;
  }

  Control SwipePage() {
    var refresh = new Button { Text = "Atualizar lista", AutoSize = true, Anchor = AnchorStyles.Left };
    refresh.Click += (_, _) => FillApps(CheckedApps().ToList());

    var opts = Grid();
    Row(opts, 0, "Enviar como", swipeMethod, "");
    Row(opts, 1, "Distância mínima", swipeMin, "unidades (~0–4096)");
    opts.Controls.Add(swipeInvert, 0, 2); opts.SetColumnSpan(swipeInvert, 3);

    var page = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
    page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
    page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    page.Controls.Add(Note("Deslizar 2 dedos para o lado vira voltar/avançar só nos apps marcados (apps abertos agora; " +
                           "os marcados ficam na lista mesmo fechados). Dedos para a direita = voltar."), 0, 0);
    page.Controls.Add(swipeApps, 0, 1);
    page.Controls.Add(refresh, 0, 2);
    page.Controls.Add(opts, 0, 3);
    return page;
  }

  Control TestPage() {
    var test = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
    test.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    test.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
    test.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
    var top = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
    top.Controls.Add(fingers);
    top.Controls.Add(showAll);
    test.Controls.Add(top, 0, 0);
    // Desligado: só cliques e gestos feitos com esta janela em primeiro plano, sem as linhas "sessão: ...".
    showAll.CheckedChanged += (_, _) => {
      events.BeginUpdate();
      events.Items.Clear();
      foreach (var e in allEvents) if (PassesFilter(e)) events.Items.Add(e);
      events.EndUpdate();
    };
    test.Controls.Add(testArea, 0, 1);
    test.Controls.Add(events, 0, 2);

    testArea.MouseDown += (_, e) => {
      testArea.Text = e.Button switch {
        MouseButtons.Left => "Botão esquerdo",
        MouseButtons.Right => "Botão direito",
        MouseButtons.Middle => "Botão do meio",
        MouseButtons.XButton1 => "Voltar",
        MouseButtons.XButton2 => "Avançar",
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
    return test;
  }

  static Label Note(string text) =>
    new Label { Text = text, AutoSize = true, MaximumSize = new Size(500, 0), ForeColor = SystemColors.GrayText, Margin = new Padding(3, 3, 3, 6) };

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
    if (loading) return;
    var s = app.Settings;
    s.TwoFingerClick = Selected(twoClick); s.ThreeFingerClick = Selected(threeClick);
    s.TwoFingerTap = Selected(twoTap); s.ThreeFingerTap = Selected(threeTap);
    s.TapMaxMs = (int)tapMaxMs.Value;
    s.TapMaxMove = (int)tapMaxMove.Value;
    s.StaleMs = (int)staleMs.Value;
    s.SwallowAfterTapMs = (int)swallowMs.Value;
    s.TouchpadSpeed = speed.Value * 5;
    s.SwipeApps = CheckedApps().ToList();
    s.SwipeMethod = swipeMethod.SelectedIndex == 1 ? SwipeMethod.AltArrows : SwipeMethod.MouseButtons;
    s.SwipeInvert = swipeInvert.Checked;
    s.SwipeMinUnits = (int)swipeMin.Value;
  }

  void OnContacts(int n) => fingers.Text = $"Dedos no touchpad: {n}";

  bool PassesFilter(EventLine e) => showAll.Checked || (!e.Session && !e.Outside);

  void AddEvent(string s) {
    // O aviso chega logo depois do gesto; a janela ativa nessa hora diz onde ele aconteceu.
    var e = new EventLine($"{DateTime.Now:HH:mm:ss}  {s}", s.Contains("sessão:"), ActiveForm != this);
    allEvents.Insert(0, e);
    if (allEvents.Count > 200) allEvents.RemoveAt(allEvents.Count - 1);
    if (!PassesFilter(e)) return;
    events.Items.Insert(0, e);
    while (events.Items.Count > 200) events.Items.RemoveAt(events.Items.Count - 1);
  }

  protected override void OnFormClosed(FormClosedEventArgs e) {
    app.ContactsChanged -= OnContacts;
    app.Info -= AddEvent;
    if (!saved) app.Settings.CopyFrom(original);
    base.OnFormClosed(e);
  }
}
