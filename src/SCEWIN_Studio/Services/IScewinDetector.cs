using System.Threading.Tasks;
using SCEWIN_Studio.Models;

namespace SCEWIN_Studio.Services;

public interface IScewinDetector
{
    Task<DetectionResult> AutoDetectAsync();
    DetectionResult ValidatePath(string path);
}
