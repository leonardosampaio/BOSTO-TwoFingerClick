using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

// Ação de cada gesto. Left = deixa o clique esquerdo do aparelho passar sem mudança;
// None = descarta (num toque, simplesmente não faz nada).
enum ClickAction { Left, Right, Middle, Back, Forward, None }

// Parâmetros ajustáveis pela janela Opções, gravados em %APPDATA%\TwoFingerClick\settings.json.
sealed class Settings {
  public ClickAction TwoFingerClick { get; set; } = ClickAction.Right;
  public ClickAction ThreeFingerClick { get; set; } = ClickAction.Middle;
  public ClickAction TwoFingerTap { get; set; } = ClickAction.Right;
  public ClickAction ThreeFingerTap { get; set; } = ClickAction.None;
  public int TapMaxMs { get; set; } = 220;          // toque mais longo que isso não é toque
  public int TapMaxMove { get; set; } = 120;        // deslocamento máximo (unidades do touchpad, ~0..4096)
  public int StaleMs { get; set; } = 250;           // contagem de dedos mais velha que isso é ignorada
  public int SwallowAfterTapMs { get; set; } = 150; // engole o clique esquerdo que o firmware gere no fim do toque
  public int TouchpadSpeed { get; set; } = 100;     // % do movimento do cursor quando vem do touchpad

  static readonly string FilePath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TwoFingerClick", "settings.json");

  static readonly JsonSerializerOptions Json = new JsonSerializerOptions {
    WriteIndented = true, Converters = { new JsonStringEnumConverter() },
  };

  public static Settings Load() {
    try {
      if (File.Exists(FilePath)) return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Json) ?? new Settings();
    } catch (Exception) { }
    return new Settings();
  }

  public void Save() {
    Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
    File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
  }

  public Settings Clone() => (Settings)MemberwiseClone();

  public void CopyFrom(Settings o) {
    TwoFingerClick = o.TwoFingerClick; ThreeFingerClick = o.ThreeFingerClick;
    TwoFingerTap = o.TwoFingerTap; ThreeFingerTap = o.ThreeFingerTap;
    TapMaxMs = o.TapMaxMs; TapMaxMove = o.TapMaxMove;
    StaleMs = o.StaleMs; SwallowAfterTapMs = o.SwallowAfterTapMs; TouchpadSpeed = o.TouchpadSpeed;
  }
}
