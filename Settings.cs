using System;
using System.IO;
using System.Text.Json;

// Parâmetros ajustáveis pela janela Opções, gravados em %APPDATA%\TwoFingerClick\settings.json.
sealed class Settings {
  public bool TapEnabled { get; set; } = true;
  public int TapMaxMs { get; set; } = 220;          // toque com 2 dedos mais longo que isso não é toque
  public int TapMaxMove { get; set; } = 120;        // deslocamento máximo (unidades do touchpad, ~0..4096)
  public int StaleMs { get; set; } = 250;           // contagem de dedos mais velha que isso é ignorada
  public int SwallowAfterTapMs { get; set; } = 150; // engole o clique esquerdo que o firmware gere no fim do toque
  public int TouchpadSpeed { get; set; } = 100;     // % do movimento do cursor quando vem do touchpad

  static readonly string FilePath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TwoFingerClick", "settings.json");

  public static Settings Load() {
    try {
      if (File.Exists(FilePath)) return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
    } catch (Exception) { }
    return new Settings();
  }

  public void Save() {
    Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
    File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
  }

  public Settings Clone() => (Settings)MemberwiseClone();

  public void CopyFrom(Settings o) {
    TapEnabled = o.TapEnabled; TapMaxMs = o.TapMaxMs; TapMaxMove = o.TapMaxMove;
    StaleMs = o.StaleMs; SwallowAfterTapMs = o.SwallowAfterTapMs; TouchpadSpeed = o.TouchpadSpeed;
  }
}
