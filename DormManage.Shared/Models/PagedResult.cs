using System.Collections.Generic;

namespace DormManage.Shared.Models;

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Total { get; set; }
    public int TotalCount { get => Total; set => Total = value; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalPages => PageSize > 0 ? (Total + PageSize - 1) / PageSize : 0;
}
