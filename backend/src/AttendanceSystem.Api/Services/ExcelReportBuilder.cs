using ClosedXML.Excel;

namespace AttendanceSystem.Api.Services;

public static class ExcelReportBuilder
{
    public static byte[] Build(CourseAnalyticsDto a)
    {
        using var wb = new XLWorkbook();

        var ws = wb.AddWorksheet("Students");
        var headers = new[] { "University ID", "Name", "Attended", "Late", "Total sessions", "Attendance %", "At risk" };
        for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        var row = 2;
        foreach (var s in a.Students)
        {
            ws.Cell(row, 1).Value = s.UniversityId;
            ws.Cell(row, 2).Value = s.FullName;
            ws.Cell(row, 3).Value = s.Attended;
            ws.Cell(row, 4).Value = s.Late;
            ws.Cell(row, 5).Value = s.Total;
            ws.Cell(row, 6).Value = s.Percent;
            ws.Cell(row, 7).Value = s.AtRisk ? "Yes" : "No";
            row++;
        }
        ws.Row(1).Style.Font.Bold = true;
        ws.Columns().AdjustToContents();

        var ss = wb.AddWorksheet("Sessions");
        var h2 = new[] { "Date (UTC)", "Present", "Late", "Absent", "Attendance %" };
        for (var i = 0; i < h2.Length; i++) ss.Cell(1, i + 1).Value = h2[i];
        row = 2;
        foreach (var s in a.Sessions)
        {
            ss.Cell(row, 1).Value = s.StartedAt.ToString("yyyy-MM-dd HH:mm");
            ss.Cell(row, 2).Value = s.Present;
            ss.Cell(row, 3).Value = s.Late;
            ss.Cell(row, 4).Value = s.Absent;
            ss.Cell(row, 5).Value = s.Rate;
            row++;
        }
        ss.Row(1).Style.Font.Bold = true;
        ss.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
