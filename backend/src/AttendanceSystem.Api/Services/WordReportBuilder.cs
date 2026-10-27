using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AttendanceSystem.Api.Services;

public static class WordReportBuilder
{
    public static byte[] Build(CourseAnalyticsDto a)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body());
            var body = main.Document.Body!;

            body.Append(Para($"{a.Code} - {a.CourseName}", true, 36));
            body.Append(Para("Attendance report - The Hashemite University", false, 24));
            body.Append(Para($"Enrolled students: {a.Enrolled}   |   Finished sessions: {a.EndedSessions}   |   Average attendance: {a.AverageRate}%   |   Alert threshold: {a.ThresholdPercent}%", false, 22));
            body.Append(Para(" ", false, 22));

            body.Append(Para("Attendance by session", true, 28));
            body.Append(MakeTable(new[] { "Date (UTC)", "Present", "Late", "Absent", "Rate %" },
                a.Sessions.Select(s => new[] { s.StartedAt.ToString("yyyy-MM-dd HH:mm"), s.Present.ToString(), s.Late.ToString(), s.Absent.ToString(), s.Rate.ToString("0.#") })));
            body.Append(Para(" ", false, 22));

            body.Append(Para("Students", true, 28));
            body.Append(MakeTable(new[] { "ID", "Name", "Attended", "Late", "Total", "%", "At risk" },
                a.Students.Select(s => new[] { s.UniversityId, s.FullName, s.Attended.ToString(), s.Late.ToString(), s.Total.ToString(), s.Percent.ToString("0.#"), s.AtRisk ? "Yes" : "No" })));

            main.Document.Save();
        }
        return ms.ToArray();
    }

    private static Paragraph Para(string text, bool bold, int halfPoints)
    {
        var rp = new RunProperties();
        if (bold) rp.Append(new Bold());
        rp.Append(new FontSize { Val = halfPoints.ToString() });
        return new Paragraph(new Run(rp, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static Table MakeTable(string[] headers, IEnumerable<string[]> rows)
    {
        var table = new Table(new TableProperties(new TableBorders(
            new TopBorder { Val = BorderValues.Single, Size = 4 },
            new BottomBorder { Val = BorderValues.Single, Size = 4 },
            new LeftBorder { Val = BorderValues.Single, Size = 4 },
            new RightBorder { Val = BorderValues.Single, Size = 4 },
            new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
            new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 })));
        table.Append(Row(headers, true));
        foreach (var r in rows) table.Append(Row(r, false));
        return table;
    }

    private static TableRow Row(string[] cells, bool header)
    {
        var tr = new TableRow();
        foreach (var c in cells)
        {
            var rp = new RunProperties();
            if (header) rp.Append(new Bold());
            tr.Append(new TableCell(new Paragraph(new Run(rp, new Text(c)))));
        }
        return tr;
    }
}
