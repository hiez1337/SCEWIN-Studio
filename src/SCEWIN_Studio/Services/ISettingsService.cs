using SCEWIN_Studio.Models;

namespace SCEWIN_Studio.Services;

public interface ISettingsService
{
    AppSettings Settings { get; }
    void Save();
    void Load();
}
