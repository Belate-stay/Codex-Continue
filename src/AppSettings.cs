using System;
using System.IO;
using System.Text.Json;

namespace CodexContinue;

/// <summary>App settings persisted to %LOCALAPPDATA%\CodexContinue\settings.json.</summary>
public sealed class AppSettings
{
    public string Theme { get; set; } = "system";   // system | light | dark
    public string Language { get; set; } = "system"; // system | zh | en
    string _accent = "blue";
    public string Accent
    {
        get => _accent;
        set => _accent = value is "blue" or "black" or "green" or "gray" or "pink" or "system" ? value : "blue";
    }
    public bool Transparency { get; set; }

    int _idleScanSeconds = 300;
    int _runningScanSeconds = 60;

    public int IdleScanSeconds
    {
        get => _idleScanSeconds;
        set => _idleScanSeconds = value is 60 or 180 or 300 or 600 or 1800 ? value : 300;
    }

    public int RunningScanSeconds
    {
        get => _runningScanSeconds;
        set => _runningScanSeconds = value is 0 or 30 or 60 or 180 or 300 ? value : 60;
    }

    // Realtime keeps polling, with a short pause between requests instead of a busy loop.
    public TimeSpan GetScanInterval(bool running) =>
        TimeSpan.FromSeconds(running ? Math.Max(1, RunningScanSeconds) : IdleScanSeconds);

    static string DirPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexContinue");
    static string FilePath => Path.Combine(DirPath, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(DirPath);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public ThemeMode ToThemeMode() => Theme switch
    {
        "light" => ThemeMode.Light,
        "dark" => ThemeMode.Dark,
        _ => ThemeMode.System,
    };

    public Loc.Lang ToLang() => Language switch
    {
        "zh" => Loc.Lang.Zh,
        "en" => Loc.Lang.En,
        _ => Loc.Lang.System,
    };
}
