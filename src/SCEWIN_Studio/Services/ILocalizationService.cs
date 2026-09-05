using System;
using System.ComponentModel;

namespace SCEWIN_Studio.Services;

public interface ILocalizationService : INotifyPropertyChanged
{
    event Action? LanguageChanged;
    string CurrentLanguage { get; set; }
    bool IsRussian { get; }
    bool IsEnglish { get; }
    string this[string key] { get; }
    string Get(string key, params object[] args);
}
