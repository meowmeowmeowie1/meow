using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using System.Linq;

namespace WrathCombo.Services;

/// <summary>
///     Minimal BLU spell helpers. Upstream added a fuller BlueMageService in
///     Services/ (out of this fork's sync scope); this provides the one method
///     the fork's Features window needs.
/// </summary>
internal static class BlueMageService
{
    /// <summary>The BLU log number for a spell action id (for display).</summary>
    public static int GetBLUIndex(uint id)
    {
        var aozKey = Svc.Data.GetExcelSheet<AozAction>()!.First(x => x.Action.RowId == id).RowId;
        return Svc.Data.GetExcelSheet<AozActionTransient>().GetRow(aozKey).Number;
    }
}
