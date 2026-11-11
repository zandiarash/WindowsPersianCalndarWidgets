using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PersianCalendar.Desktop.Services;

public enum WindowLayer { Normal, Topmost, Desktop }

public enum AppTheme { System, Light, Dark }

public sealed class WidgetSettings
{
    public double? Left { get; set; }
    public double? Top { get; set; }
    public WindowLayer Layer { get; set; } = WindowLayer.Desktop;
    public AppTheme Theme { get; set; } = AppTheme.System;
    public bool ShowSeconds { get; set; } = true;
    public bool ShowGregorianNumbers { get; set; } = true;
    public int HijriAdjustment { get; set; } = Core.HijriDate.IranAdjustment;
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(WidgetSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;

/// <summary>Persists <see cref="WidgetSettings"/> as JSON under %LocalAppData% (source generated, trimming safe).</summary>
public sealed class SettingsService
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PersianCalendarWidget", "settings.json");

    public WidgetSettings Current { get; private set; } = new();

    public SettingsService Load()
    {
        try
        {
            if (File.Exists(_path))
                Current = JsonSerializer.Deserialize(File.ReadAllText(_path), SettingsJsonContext.Default.WidgetSettings) ?? new();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            Current = new();
        }
        return this;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(Current, SettingsJsonContext.Default.WidgetSettings));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Settings are a convenience; never crash the widget because of them.
        }
    }
}
