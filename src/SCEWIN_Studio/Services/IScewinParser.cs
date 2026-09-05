using System.Collections.Generic;
using SCEWIN_Studio.Models;

namespace SCEWIN_Studio.Services;

public interface IScewinParser
{
    ScewinDump Parse(string content, string filePath = "");
    string GenerateDiffScript(ScewinDump dump, IEnumerable<ScewinToken> modifiedTokens);
    string GenerateFullScript(ScewinDump dump);
}
