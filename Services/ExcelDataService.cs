using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Quell.Services;

public class SpreadsheetData
{
    public List<string> SheetNames { get; set; } = new();
    public string ActiveSheet { get; set; } = string.Empty;
    public List<string> Headers { get; set; } = new();
    public List<List<string>> Rows { get; set; } = new();

    public int TotalRowCount => Rows.Count;
    public int TotalColumnCount => Headers.Count;
}

/// <summary>
/// High-performance cross-platform service for reading and creating Excel (.xlsx),
/// CSV, TSV, and SpreadsheetML files with zero external dependencies.
/// </summary>
public static class ExcelDataService
{
    private static readonly XNamespace MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    /// <summary>
    /// Reads a spreadsheet file (.xlsx, .csv, .tsv, .xml) and returns parsed headers and row records.
    /// </summary>
    public static SpreadsheetData ReadSpreadsheet(string filePath, string? targetSheet = null, bool firstRowIsHeader = true)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        string ext = Path.GetExtension(filePath).ToLowerInvariant();

        if (ext is ".xlsx" or ".xlsm")
        {
            return ReadXlsx(filePath, targetSheet, firstRowIsHeader);
        }
        else if (ext is ".csv" or ".tsv" or ".txt")
        {
            return ReadCsv(filePath, firstRowIsHeader);
        }
        else if (ext is ".xml")
        {
            return ReadSpreadsheetXml(filePath, firstRowIsHeader);
        }
        else
        {
            // Fallback: try CSV first, then XLSX
            try
            {
                return ReadCsv(filePath, firstRowIsHeader);
            }
            catch
            {
                return ReadXlsx(filePath, targetSheet, firstRowIsHeader);
            }
        }
    }

    /// <summary>
    /// Reads an OpenXML .xlsx file using ZipArchive and LINQ to XML.
    /// </summary>
    public static SpreadsheetData ReadXlsx(string filePath, string? targetSheet = null, bool firstRowIsHeader = true)
    {
        var data = new SpreadsheetData();

        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        // 1. Read Shared Strings (xl/sharedStrings.xml)
        var sharedStrings = new List<string>();
        var sharedStringsEntry = archive.GetEntry("xl/sharedStrings.xml");
        if (sharedStringsEntry != null)
        {
            using var ssStream = sharedStringsEntry.Open();
            var ssDoc = XDocument.Load(ssStream);
            foreach (var si in ssDoc.Descendants(MainNs + "si"))
            {
                var textElements = si.Descendants(MainNs + "t").Select(t => t.Value);
                sharedStrings.Add(string.Concat(textElements));
            }
        }

        // 2. Discover Sheets (xl/workbook.xml)
        var sheetEntries = new List<(string Name, string SheetId, string RelId)>();
        var workbookEntry = archive.GetEntry("xl/workbook.xml");
        if (workbookEntry != null)
        {
            using var wbStream = workbookEntry.Open();
            var wbDoc = XDocument.Load(wbStream);
            foreach (var s in wbDoc.Descendants(MainNs + "sheet"))
            {
                string name = s.Attribute("name")?.Value ?? "Sheet";
                string sheetId = s.Attribute("sheetId")?.Value ?? "1";
                string relId = s.Attribute(RelNs + "id")?.Value ?? string.Empty;
                sheetEntries.Add((name, sheetId, relId));
                data.SheetNames.Add(name);
            }
        }

        if (sheetEntries.Count == 0)
        {
            data.SheetNames.Add("Sheet1");
            sheetEntries.Add(("Sheet1", "1", "rId1"));
        }

        // 3. Resolve target worksheet path
        string chosenSheetName = !string.IsNullOrWhiteSpace(targetSheet) && data.SheetNames.Contains(targetSheet)
            ? targetSheet
            : sheetEntries[0].Name;

        data.ActiveSheet = chosenSheetName;

        int sheetIndex = sheetEntries.FindIndex(s => s.Name.Equals(chosenSheetName, StringComparison.OrdinalIgnoreCase));
        if (sheetIndex < 0) sheetIndex = 0;

        // Try standard naming convention first, or fallback by index
        var wsEntry = archive.GetEntry($"xl/worksheets/sheet{sheetIndex + 1}.xml")
                      ?? archive.Entries.FirstOrDefault(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) && e.FullName.EndsWith(".xml"));

        if (wsEntry == null)
        {
            return data;
        }

        // 4. Parse Worksheet Rows and Cells
        using var wsStream = wsEntry.Open();
        var wsDoc = XDocument.Load(wsStream);

        var rawRows = new List<List<string>>();

        foreach (var rowElem in wsDoc.Descendants(MainNs + "row"))
        {
            var cellDict = new Dictionary<int, string>();
            int maxCol = 0;

            foreach (var c in rowElem.Elements(MainNs + "c"))
            {
                string cellRef = c.Attribute("r")?.Value ?? string.Empty;
                int colIdx = ParseColumnIndexFromRef(cellRef);
                maxCol = Math.Max(maxCol, colIdx);

                string type = c.Attribute("t")?.Value ?? string.Empty;
                string value = string.Empty;

                if (type == "s")
                {
                    // Shared String lookup
                    if (int.TryParse(c.Element(MainNs + "v")?.Value, out int sIndex) && sIndex >= 0 && sIndex < sharedStrings.Count)
                    {
                        value = sharedStrings[sIndex];
                    }
                }
                else if (type == "inlineStr")
                {
                    value = c.Element(MainNs + "is")?.Element(MainNs + "t")?.Value ?? string.Empty;
                }
                else
                {
                    // Number, date, boolean, or direct formula string
                    value = c.Element(MainNs + "v")?.Value ?? string.Empty;
                }

                cellDict[colIdx] = value?.Trim() ?? string.Empty;
            }

            if (cellDict.Count > 0)
            {
                var rowList = new List<string>();
                for (int i = 0; i <= maxCol; i++)
                {
                    rowList.Add(cellDict.TryGetValue(i, out var val) ? val : string.Empty);
                }
                rawRows.Add(rowList);
            }
        }

        if (rawRows.Count == 0) return data;

        // Normalize row widths
        int maxRowWidth = rawRows.Max(r => r.Count);
        foreach (var r in rawRows)
        {
            while (r.Count < maxRowWidth) r.Add(string.Empty);
        }

        if (firstRowIsHeader)
        {
            var headerRow = rawRows[0];
            for (int i = 0; i < headerRow.Count; i++)
            {
                string h = headerRow[i];
                if (string.IsNullOrWhiteSpace(h)) h = $"Column {IndexToColumnLetter(i)}";
                data.Headers.Add(h);
            }

            for (int i = 1; i < rawRows.Count; i++)
            {
                // Only include if row is not completely empty
                if (rawRows[i].Any(c => !string.IsNullOrWhiteSpace(c)))
                {
                    data.Rows.Add(rawRows[i]);
                }
            }
        }
        else
        {
            for (int i = 0; i < maxRowWidth; i++)
            {
                data.Headers.Add($"Column {IndexToColumnLetter(i)}");
            }

            foreach (var r in rawRows)
            {
                if (r.Any(c => !string.IsNullOrWhiteSpace(c)))
                {
                    data.Rows.Add(r);
                }
            }
        }

        return data;
    }

    /// <summary>
    /// Reads a CSV or TSV file with quote handling and automatic delimiter detection.
    /// </summary>
    public static SpreadsheetData ReadCsv(string filePath, bool firstRowIsHeader = true)
    {
        var data = new SpreadsheetData
        {
            ActiveSheet = Path.GetFileNameWithoutExtension(filePath),
            SheetNames = { Path.GetFileNameWithoutExtension(filePath) }
        };

        var allLines = File.ReadAllLines(filePath, Encoding.UTF8);
        if (allLines.Length == 0) return data;

        char delimiter = DetectCsvDelimiter(allLines);
        var rawRows = new List<List<string>>();

        foreach (var line in allLines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var tokens = ParseCsvLine(line, delimiter);
            rawRows.Add(tokens);
        }

        if (rawRows.Count == 0) return data;

        int maxCols = rawRows.Max(r => r.Count);
        foreach (var r in rawRows)
        {
            while (r.Count < maxCols) r.Add(string.Empty);
        }

        if (firstRowIsHeader)
        {
            var headerRow = rawRows[0];
            for (int i = 0; i < headerRow.Count; i++)
            {
                string h = headerRow[i];
                if (string.IsNullOrWhiteSpace(h)) h = $"Column {IndexToColumnLetter(i)}";
                data.Headers.Add(h);
            }

            for (int i = 1; i < rawRows.Count; i++)
            {
                if (rawRows[i].Any(c => !string.IsNullOrWhiteSpace(c)))
                {
                    data.Rows.Add(rawRows[i]);
                }
            }
        }
        else
        {
            for (int i = 0; i < maxCols; i++)
            {
                data.Headers.Add($"Column {IndexToColumnLetter(i)}");
            }
            foreach (var r in rawRows)
            {
                if (r.Any(c => !string.IsNullOrWhiteSpace(c)))
                {
                    data.Rows.Add(r);
                }
            }
        }

        return data;
    }

    /// <summary>
    /// Reads Excel SpreadsheetML 2003 XML format.
    /// </summary>
    public static SpreadsheetData ReadSpreadsheetXml(string filePath, bool firstRowIsHeader = true)
    {
        var data = new SpreadsheetData();
        XNamespace ssNs = "urn:schemas-microsoft-com:office:spreadsheet";

        var doc = XDocument.Load(filePath);
        var worksheets = doc.Descendants(ssNs + "Worksheet").ToList();

        foreach (var ws in worksheets)
        {
            string name = ws.Attribute(ssNs + "Name")?.Value ?? "Sheet";
            data.SheetNames.Add(name);
        }

        if (data.SheetNames.Count == 0) data.SheetNames.Add("Sheet1");
        data.ActiveSheet = data.SheetNames[0];

        var activeWs = worksheets.FirstOrDefault() ?? doc.Descendants(ssNs + "Worksheet").FirstOrDefault();
        if (activeWs == null) return data;

        var rawRows = new List<List<string>>();
        foreach (var row in activeWs.Descendants(ssNs + "Row"))
        {
            var rowVals = new List<string>();
            foreach (var cell in row.Elements(ssNs + "Cell"))
            {
                string text = cell.Element(ssNs + "Data")?.Value ?? string.Empty;
                rowVals.Add(text.Trim());
            }
            if (rowVals.Any(v => !string.IsNullOrWhiteSpace(v)))
            {
                rawRows.Add(rowVals);
            }
        }

        if (rawRows.Count == 0) return data;

        int maxCols = rawRows.Max(r => r.Count);
        foreach (var r in rawRows)
        {
            while (r.Count < maxCols) r.Add(string.Empty);
        }

        if (firstRowIsHeader)
        {
            for (int i = 0; i < rawRows[0].Count; i++)
            {
                string h = rawRows[0][i];
                if (string.IsNullOrWhiteSpace(h)) h = $"Column {IndexToColumnLetter(i)}";
                data.Headers.Add(h);
            }
            for (int i = 1; i < rawRows.Count; i++)
            {
                data.Rows.Add(rawRows[i]);
            }
        }
        else
        {
            for (int i = 0; i < maxCols; i++)
            {
                data.Headers.Add($"Column {IndexToColumnLetter(i)}");
            }
            foreach (var r in rawRows) data.Rows.Add(r);
        }

        return data;
    }

    /// <summary>
    /// Generates an empty Excel template matching a database table schema for clients to fill out.
    /// </summary>
    public static void CreateExcelTemplate(
        string filePath,
        string tableName,
        IEnumerable<(string ColumnName, string DataType, bool IsRequired)> columns)
    {
        var colList = columns.ToList();
        var headers = colList.Select(c => c.ColumnName).ToList();

        // Sample / Hint row indicating expected data types and required constraints
        var hintRow = colList.Select(c =>
        {
            string req = c.IsRequired ? "Required" : "Optional";
            return $"[{c.DataType.ToUpperInvariant()}] {req}";
        }).ToList();

        var rows = new List<List<string>> { hintRow };

        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext is ".csv")
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", headers.Select(EscapeCsv)));
            sb.AppendLine(string.Join(",", hintRow.Select(EscapeCsv)));
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }
        else
        {
            // Create genuine .xlsx file
            CreateXlsx(filePath, string.IsNullOrWhiteSpace(tableName) ? "Template" : tableName, headers, rows);
        }
    }

    /// <summary>
    /// Writes a complete, valid OpenXML Excel workbook (.xlsx).
    /// </summary>
    public static void CreateXlsx(string filePath, string sheetName, List<string> headers, List<List<string>> rows)
    {
        string dir = Path.GetDirectoryName(filePath) ?? string.Empty;
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        string cleanSheetName = string.IsNullOrWhiteSpace(sheetName) ? "Sheet1" : Regex.Replace(sheetName, @"[\\/*?:\[\]]", "_");
        if (cleanSheetName.Length > 31) cleanSheetName = cleanSheetName.Substring(0, 31);

        using var zipStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

        // 1. [Content_Types].xml
        var ctEntry = archive.CreateEntry("[Content_Types].xml");
        using (var writer = new StreamWriter(ctEntry.Open(), Encoding.UTF8))
        {
            writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
  <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
  <Default Extension=""xml"" ContentType=""application/xml""/>
  <Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/>
  <Override PartName=""/xl/worksheets/sheet1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>
  <Override PartName=""/xl/styles.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml""/>
  <Override PartName=""/xl/sharedStrings.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml""/>
</Types>");
        }

        // 2. _rels/.rels
        var rootRelsEntry = archive.CreateEntry("_rels/.rels");
        using (var writer = new StreamWriter(rootRelsEntry.Open(), Encoding.UTF8))
        {
            writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/>
</Relationships>");
        }

        // 3. xl/_rels/workbook.xml.rels
        var wbRelsEntry = archive.CreateEntry("xl/_rels/workbook.xml.rels");
        using (var writer = new StreamWriter(wbRelsEntry.Open(), Encoding.UTF8))
        {
            writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet1.xml""/>
  <Relationship Id=""rId2"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"" Target=""styles.xml""/>
  <Relationship Id=""rId3"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings"" Target=""sharedStrings.xml""/>
</Relationships>");
        }

        // 4. xl/workbook.xml
        var wbEntry = archive.CreateEntry("xl/workbook.xml");
        using (var writer = new StreamWriter(wbEntry.Open(), Encoding.UTF8))
        {
            writer.Write($@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">
  <sheets>
    <sheet name=""{cleanSheetName}"" sheetId=""1"" r:id=""rId1""/>
  </sheets>
</workbook>");
        }

        // 5. xl/styles.xml
        var stylesEntry = archive.CreateEntry("xl/styles.xml");
        using (var writer = new StreamWriter(stylesEntry.Open(), Encoding.UTF8))
        {
            writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<styleSheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
  <fonts count=""2"">
    <font><name val=""Segoe UI""/><sz val=""11""/></font>
    <font><b/><name val=""Segoe UI""/><sz val=""11""/></font>
  </fonts>
  <fills count=""2"">
    <fill><patternFill patternType=""none""/></fill>
    <fill><patternFill patternType=""gray125""/></fill>
  </fills>
  <borders count=""1""><border/></borders>
  <cellStyleXfs count=""1""><xf/></cellStyleXfs>
  <cellXfs count=""2"">
    <xf fontId=""0"" fillId=""0"" borderId=""0""/>
    <xf fontId=""1"" fillId=""0"" borderId=""0"" applyFont=""1""/>
  </cellXfs>
</styleSheet>");
        }

        // 6. Build Shared Strings Table
        var stringToIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        var sharedStrings = new List<string>();

        int GetStringIndex(string str)
        {
            if (stringToIndex.TryGetValue(str, out int idx)) return idx;
            idx = sharedStrings.Count;
            sharedStrings.Add(str);
            stringToIndex[str] = idx;
            return idx;
        }

        var headerIndices = headers.Select(GetStringIndex).ToList();
        var rowIndices = new List<List<int>>();
        foreach (var r in rows)
        {
            rowIndices.Add(r.Select(GetStringIndex).ToList());
        }

        // 7. Write xl/sharedStrings.xml
        var ssEntry = archive.CreateEntry("xl/sharedStrings.xml");
        using (var writer = new StreamWriter(ssEntry.Open(), Encoding.UTF8))
        {
            writer.Write($@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<sst xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" count=""{sharedStrings.Count}"" uniqueCount=""{sharedStrings.Count}"">");
            foreach (var s in sharedStrings)
            {
                string escaped = System.Security.SecurityElement.Escape(s) ?? string.Empty;
                writer.Write($@"<si><t>{escaped}</t></si>");
            }
            writer.Write("</sst>");
        }

        // 8. Write xl/worksheets/sheet1.xml
        var wsEntry = archive.CreateEntry("xl/worksheets/sheet1.xml");
        using (var writer = new StreamWriter(wsEntry.Open(), Encoding.UTF8))
        {
            writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
  <sheetData>");

            // Row 1: Headers (bold style s="1")
            writer.Write(@"<row r=""1"">");
            for (int c = 0; c < headerIndices.Count; c++)
            {
                string cellRef = $"{IndexToColumnLetter(c)}1";
                writer.Write($@"<c r=""{cellRef}"" t=""s"" s=""1""><v>{headerIndices[c]}</v></c>");
            }
            writer.Write("</row>");

            // Rows 2..N: Data
            for (int r = 0; r < rowIndices.Count; r++)
            {
                int rowNum = r + 2;
                writer.Write($@"<row r=""{rowNum}"">");
                for (int c = 0; c < rowIndices[r].Count; c++)
                {
                    string cellRef = $"{IndexToColumnLetter(c)}{rowNum}";
                    writer.Write($@"<c r=""{cellRef}"" t=""s""><v>{rowIndices[r][c]}</v></c>");
                }
                writer.Write("</row>");
            }

            writer.Write(@"</sheetData>
</worksheet>");
        }
    }

    #region Helper Methods

    private static char DetectCsvDelimiter(string[] lines)
    {
        char[] candidates = { ',', ';', '\t', '|' };
        var sample = lines.Take(5).ToList();

        char bestDelim = ',';
        int maxCount = -1;

        foreach (var c in candidates)
        {
            int total = sample.Sum(line => line.Count(ch => ch == c));
            if (total > maxCount)
            {
                maxCount = total;
                bestDelim = c;
            }
        }

        return maxCount > 0 ? bestDelim : ',';
    }

    private static List<string> ParseCsvLine(string line, char delimiter)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];

            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++; // skip escaped quote
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == delimiter && !inQuotes)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        result.Add(current.ToString().Trim());
        return result;
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    public static int ParseColumnIndexFromRef(string cellRef)
    {
        if (string.IsNullOrEmpty(cellRef)) return 0;
        int idx = 0;
        foreach (char c in cellRef)
        {
            if (char.IsLetter(c))
            {
                idx = idx * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
            }
            else break;
        }
        return Math.Max(0, idx - 1);
    }

    public static string IndexToColumnLetter(int colIndex)
    {
        colIndex++;
        string col = string.Empty;
        while (colIndex > 0)
        {
            int rem = (colIndex - 1) % 26;
            col = (char)('A' + rem) + col;
            colIndex = (colIndex - 1) / 26;
        }
        return col;
    }

    #endregion
}
