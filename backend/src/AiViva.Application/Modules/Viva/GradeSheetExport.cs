using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace AiViva.Application.Modules.Viva;

/// <summary>
/// Writes the session grade sheet in the layout used for official score submission:
/// STT, MSSV, Họ và tên, per-question lecturer marks, AI suggestion, final score.
/// XLSX avoids the decimal/list-separator problems CSV has in Vietnamese Excel locales.
/// </summary>
public static class GradeSheetExport
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string ToCsv(GradeSheetDto sheet)
    {
        var lines = new List<string> { string.Join(",", Header(sheet, includeSession: true).Select(Csv)) };
        foreach (var row in sheet.Rows)
        {
            var cells = new List<string> { row.No.ToString(Invariant), row.StudentCode ?? "", row.FullName ?? "", row.Email, sheet.Title };
            cells.AddRange(row.QuestionScores.Select(Number));
            cells.AddRange([Number(row.AiScore), Number(row.FinalScore), row.Status, Date(row.CompletedAt), row.LecturerFeedback ?? ""]);
            lines.Add(string.Join(",", cells.Select(Csv)));
        }
        return string.Join("\r\n", lines);
    }

    public static byte[] ToXlsx(GradeSheetDto sheet)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Part(zip, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>
                """);
            Part(zip, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
                """);
            Part(zip, "xl/workbook.xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Bang diem" sheetId="1" r:id="rId1"/></sheets></workbook>
                """);
            Part(zip, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>
                """);
            // Styles: 0 normal, 1 header (bold, shaded, bordered), 2 title, 3 score "0.00" bordered, 4 text bordered.
            Part(zip, "xl/styles.xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><numFmts count="1"><numFmt numFmtId="164" formatCode="0.00"/></numFmts><fonts count="3"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="14"/><name val="Calibri"/></font></fonts><fills count="3"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FFDDEBF7"/><bgColor indexed="64"/></patternFill></fill></fills><borders count="2"><border><left/><right/><top/><bottom/><diagonal/></border><border><left style="thin"><color auto="1"/></left><right style="thin"><color auto="1"/></right><top style="thin"><color auto="1"/></top><bottom style="thin"><color auto="1"/></bottom><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="5"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="2" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1"/><xf numFmtId="0" fontId="2" fillId="0" borderId="0" xfId="0" applyFont="1"/><xf numFmtId="164" fontId="0" fillId="0" borderId="1" xfId="0" applyNumberFormat="1" applyBorder="1"/><xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyBorder="1"/></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>
                """);
            using var writer = XmlWriter.Create(zip.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Optimal).Open(),
                new XmlWriterSettings { Encoding = new UTF8Encoding(false), CloseOutput = true });
            WriteSheet(writer, sheet);
        }
        return stream.ToArray();
    }

    private static void WriteSheet(XmlWriter writer, GradeSheetDto sheet)
    {
        const string ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var header = Header(sheet, includeSession: false);
        writer.WriteStartDocument(true);
        writer.WriteStartElement("worksheet", ns);
        writer.WriteStartElement("cols", ns);
        for (var i = 0; i < header.Count; i++)
        {
            var width = header[i] switch
            {
                "STT" => 6,
                "MSSV" => 14,
                "Họ và tên" => 28,
                "Email" => 32,
                "Nhận xét giảng viên" => 48,
                "Trạng thái" or "Hoàn thành lúc" => 20,
                _ => 13
            };
            writer.WriteStartElement("col", ns);
            writer.WriteAttributeString("min", (i + 1).ToString(Invariant));
            writer.WriteAttributeString("max", (i + 1).ToString(Invariant));
            writer.WriteAttributeString("width", width.ToString(Invariant));
            writer.WriteAttributeString("customWidth", "1");
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteStartElement("sheetData", ns);

        var rowNumber = 0;
        void Row(Action<Action<object?, int>> cells)
        {
            rowNumber++;
            var column = 0;
            writer.WriteStartElement("row", ns);
            writer.WriteAttributeString("r", rowNumber.ToString(Invariant));
            cells((value, style) =>
            {
                var reference = ColumnName(column++) + rowNumber.ToString(Invariant);
                writer.WriteStartElement("c", ns);
                writer.WriteAttributeString("r", reference);
                if (style != 0) writer.WriteAttributeString("s", style.ToString(Invariant));
                if (value is decimal or int)
                {
                    writer.WriteElementString("v", ns, Convert.ToDecimal(value, Invariant).ToString(Invariant));
                }
                else if (value is string text && text.Length > 0)
                {
                    writer.WriteAttributeString("t", "inlineStr");
                    writer.WriteStartElement("is", ns);
                    writer.WriteElementString("t", ns, XmlSafe(text));
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            });
            writer.WriteEndElement();
        }

        Row(cell => cell("BẢNG ĐIỂM THI VẤN ĐÁP", 2));
        Row(cell => cell($"Môn học: {sheet.SubjectName ?? "—"}", 0));
        Row(cell => cell($"Phiên thi: {sheet.Title}", 0));
        Row(cell => cell($"Ngày xuất: {Date(sheet.GeneratedAt)} (GMT+7) · Số câu hỏi chính: {sheet.QuestionCount}", 0));
        Row(_ => { });
        Row(cell => header.ForEach(title => cell(title, 1)));
        foreach (var item in sheet.Rows)
        {
            Row(cell =>
            {
                cell(item.No, 4);
                cell(item.StudentCode ?? "", 4);
                cell(item.FullName ?? "", 4);
                cell(item.Email, 4);
                foreach (var score in item.QuestionScores) cell(score, 3);
                cell(item.AiScore, 3);
                cell(item.FinalScore, 3);
                cell(item.Status, 4);
                cell(Date(item.CompletedAt), 4);
                cell(item.LecturerFeedback ?? "", 4);
            });
        }
        Row(_ => { });
        Row(cell => cell("Điểm chốt do giảng viên quyết định; điểm AI chỉ mang tính gợi ý.", 0));
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static List<string> Header(GradeSheetDto sheet, bool includeSession)
    {
        var header = new List<string> { "STT", "MSSV", "Họ và tên", "Email" };
        if (includeSession) header.Add("Phiên thi");
        header.AddRange(Enumerable.Range(1, sheet.QuestionCount).Select(i => $"Câu {i}"));
        header.AddRange(["Điểm AI gợi ý", "Điểm chốt", "Trạng thái", "Hoàn thành lúc", "Nhận xét giảng viên"]);
        return header;
    }

    private static void Part(ZipArchive zip, string name, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
        writer.Write(content.Trim());
    }

    private static string ColumnName(int index)
    {
        var name = "";
        for (index++; index > 0; index = (index - 1) / 26) name = (char)('A' + (index - 1) % 26) + name;
        return name;
    }

    private static string Number(decimal? value) => value?.ToString("0.##", Invariant) ?? "";

    private static string Date(DateTimeOffset? value) =>
        value?.ToOffset(VietnamOffset).ToString("dd/MM/yyyy HH:mm", Invariant) ?? "";

    // XML 1.0 forbids most control characters; drop them instead of failing the export.
    private static string XmlSafe(string value) =>
        new(value.Where(c => c is '\t' or '\n' or '\r' || (c >= 0x20 && c is not ('￾' or '￿'))).ToArray());

    // Prevent spreadsheet formula execution from user-provided titles, names and emails.
    private static string Csv(string value)
    {
        var trimmed = value.TrimStart();
        if (trimmed.StartsWith('=') || trimmed.StartsWith('+') || trimmed.StartsWith('-') || trimmed.StartsWith('@'))
            value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
