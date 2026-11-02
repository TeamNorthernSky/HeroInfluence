using System.Collections.Generic;
using System.IO;
using System.Text;

public static partial class JcTutorialText
{
    public static string ReadSource(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 3 || bytes[0] != 0xEF || bytes[1] != 0xBB || bytes[2] != 0xBF)
            throw new InvalidDataException("CSV를 UTF-8 BOM 형식으로 저장해 주세요.");
        return new UTF8Encoding(false, true).GetString(bytes, 3, bytes.Length - 3);
    }

    // 따옴표 안의 쉼표/줄바꿈과 이중 따옴표 이스케이프를 지원합니다.
    static bool TryReadCsv(string source, out List<List<string>> rows, out string error)
    {
        rows = new List<List<string>>(); error = null;
        source = source.TrimStart('\uFEFF').Replace("\r\n", "\n").Replace('\r', '\n');
        var row = new List<string>(); var cell = new StringBuilder();
        bool quoted = false, closed = false, started = false;
        int line = 1;
        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < source.Length && source[i + 1] == '"') { cell.Append('"'); i++; }
                    else { quoted = false; closed = true; }
                }
                else { cell.Append(c); if (c == '\n') line++; }
                continue;
            }
            if (c == ',' || c == '\n')
            {
                row.Add(cell.ToString()); cell.Clear(); closed = started = false;
                if (c == '\n') { rows.Add(row); row = new List<string>(); line++; }
                continue;
            }
            if (closed) { error = $"CSV {line}행: 닫는 따옴표 뒤에는 쉼표 또는 줄바꿈만 올 수 있습니다."; return false; }
            if (c == '"')
            {
                if (started) { error = $"CSV {line}행: 셀 안의 따옴표는 전체 셀을 따옴표로 감싸고 두 번 써야 합니다."; return false; }
                quoted = started = true;
            }
            else { cell.Append(c); started = true; }
        }
        if (quoted) { error = $"CSV {line}행: 닫히지 않은 따옴표가 있습니다."; return false; }
        if (started || closed || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
        return true;
    }
}
