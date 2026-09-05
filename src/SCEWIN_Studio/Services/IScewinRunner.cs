using System.Threading.Tasks;

namespace SCEWIN_Studio.Services;

public interface IScewinRunner
{
    bool IsAdministrator();
    Task<(bool success, string output, string error)> ExportNvramAsync(string scewinPath, string outputFilePath);
    Task<(bool success, string output, string error)> ImportNvramAsync(string scewinPath, string diffFilePath);
    void KillOrphans();
}
