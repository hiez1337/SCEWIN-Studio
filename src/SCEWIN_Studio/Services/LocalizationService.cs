using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace SCEWIN_Studio.Services;

public class LocalizationService : ILocalizationService
{
    private static LocalizationService? _instance;
    public static LocalizationService Instance => _instance ??= new LocalizationService();

    private readonly Dictionary<string, string> _ruStrings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _enStrings = new(StringComparer.OrdinalIgnoreCase);

    private string _currentLanguage = "ru-RU";

    public event Action? LanguageChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage != value)
            {
                _currentLanguage = value;
                LanguageChanged?.Invoke();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRussian)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnglish)));
            }
        }
    }

    public bool IsRussian => _currentLanguage.StartsWith("ru", StringComparison.OrdinalIgnoreCase);
    public bool IsEnglish => !IsRussian;

    public LocalizationService()
    {
        LoadStrings();
    }

    private void LoadStrings()
    {
        LoadDictionary("Strings.ru.json", _ruStrings);
        LoadDictionary("Strings.en.json", _enStrings);
    }

    private void LoadDictionary(string fileName, Dictionary<string, string> target)
    {
        target.Clear();

        // 1. Try local application directory Resources/
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var filePath = Path.Combine(appDir, "Resources", fileName);

        if (!File.Exists(filePath))
        {
            // Try relative to executing assembly
            filePath = Path.Combine(appDir, fileName);
        }

        if (File.Exists(filePath))
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (parsed != null)
                {
                    foreach (var kvp in parsed)
                    {
                        target[kvp.Key] = kvp.Value;
                    }
                    return;
                }
            }
            catch { }
        }

        // 2. Embedded resource fallback if available
        var assembly = Assembly.GetExecutingAssembly();
        var resName = $"{assembly.GetName().Name}.Resources.{fileName}";
        using var stream = assembly.GetManifestResourceStream(resName);
        if (stream != null)
        {
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (parsed != null)
            {
                foreach (var kvp in parsed)
                {
                    target[kvp.Key] = kvp.Value;
                }
            }
        }
    }

    public string this[string key]
    {
        get
        {
            var dict = IsRussian ? _ruStrings : _enStrings;
            if (dict.TryGetValue(key, out var val)) return val;

            // Fallback to English
            if (_enStrings.TryGetValue(key, out var enVal)) return enVal;
            // Fallback to Russian
            if (_ruStrings.TryGetValue(key, out var ruVal)) return ruVal;

            return key;
        }
    }

    public string Get(string key, params object[] args)
    {
        var raw = this[key];
        try
        {
            return string.Format(raw, args);
        }
        catch
        {
            return raw;
        }
    }
}
