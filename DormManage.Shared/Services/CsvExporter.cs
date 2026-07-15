using System.Globalization;
using System.Text;

namespace DormManage.Shared.Services;

/// <summary>
/// 通用 CSV 导出器（P2-9）
///
/// 使用示例：
/// <code>
/// var exporter = new CsvExporter();
/// exporter.AddHeader("工号", "姓名", "部门");
/// foreach (var e in employees)
///     exporter.AddRow(e.EmployeeCode, e.RealName, e.Department);
/// return File(exporter.ToBytes(), "text/csv", "人员.csv");
/// </code>
///
/// 特性：
/// - 自动添加 UTF-8 BOM（Excel 中文兼容）
/// - 自动转义（包含 , " \n 的字段加引号）
/// - 头行可选
/// </summary>
public class CsvExporter
{
    private readonly List<string[]> _rows = new();
    private string[]? _headers;
    private readonly StringBuilder _sb = new();

    public CsvExporter WithHeaders(params string[] headers)
    {
        _headers = headers;
        return this;
    }

    public CsvExporter AddRow(params object?[] values)
    {
        var cells = values.Select(v => v switch
        {
            null => "",
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateOnly d => d.ToString("yyyy-MM-dd"),
            decimal m => m.ToString(CultureInfo.InvariantCulture),
            double d2 => d2.ToString("G", CultureInfo.InvariantCulture),
            bool b => b ? "是" : "否",
            _ => v.ToString() ?? ""
        }).ToArray();
        _rows.Add(cells);
        return this;
    }

    public byte[] ToBytes()
    {
        _sb.Clear();
        if (_headers is not null)
        {
            _sb.AppendLine(string.Join(",", _headers.Select(Escape)));
        }
        foreach (var row in _rows)
        {
            _sb.AppendLine(string.Join(",", row.Select(Escape)));
        }

        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var content = Encoding.UTF8.GetBytes(_sb.ToString());
        var result = new byte[bom.Length + content.Length];
        bom.CopyTo(result, 0);
        content.CopyTo(result, bom.Length);
        return result;
    }

    public int RowCount => _rows.Count;

    private static string Escape(string? s)
    {
        s ??= "";
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}