using System.Collections.Generic;

namespace EverythingCmdPal.Services;

/// <summary>
/// Search abstraction so pagination logic (SearchSession) can be tested without
/// loading the native Everything SDK.
/// </summary>
internal interface IEverythingSearch
{
    IReadOnlyList<SearchHit> Search(string query, int offset, int count, SortOrder.SortSpec sort, out int total);
}
