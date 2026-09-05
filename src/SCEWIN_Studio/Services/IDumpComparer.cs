using System.Collections.Generic;
using SCEWIN_Studio.Models;

namespace SCEWIN_Studio.Services;

public interface IDumpComparer
{
    List<DumpComparisonItem> Compare(ScewinDump dumpA, ScewinDump dumpB);
}
