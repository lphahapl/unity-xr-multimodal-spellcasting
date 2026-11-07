using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NarutoVR.SealCombat.Editor
{
    internal static class NarrationTable
    {
        // Consume one whole field and its delimiter, including escaped quotes and embedded newlines.
        static readonly Regex CsvField = new Regex(@"\G(?:""((?:[^""]|"""")*)""|([^"",\r\n]*))(,|\r\n|\r|\n|\z)");
        static readonly XNamespace Sheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        public static List<NarrationLine> Read(string path, IEnumerable<NarrationLine> previous)
        {
            IEnumerable<List<string>> rows;
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".csv": rows = ReadCsv(File.ReadAllText(path)); break;
                case ".xlsx": rows = ReadWorkbook(path); break;
                default: throw new InvalidDataException("请选择 UTF-8 CSV 或 .xlsx 工作簿。");
            }

            // 按音频与说话者复用原句，重排或插行时仍保留 Inspector 的模式和事件绑定。
            var bindings = previous.Where(line => line != null)
                .GroupBy(line => (line.audioFileName, line.speaker))
                .ToDictionary(group => group.Key, group => new Queue<NarrationLine>(group));
            var result = new List<NarrationLine>();
            var delays = new List<float>();
            int audio = -1, speaker = -1, delay = -1, rowNumber = 0;
            foreach (var row in rows)
            {
                ++rowNumber;
                if (row.All(string.IsNullOrWhiteSpace)) continue;
                if (audio < 0)
                {
                    audio = Header(row, "audioFileName");
                    speaker = Header(row, "speaker");
                    delay = Header(row, "delayAfter", required: false);
                    continue;
                }
                string file = row.ElementAtOrDefault(audio)?.Trim();
                string who = row.ElementAtOrDefault(speaker)?.Trim();
                if (string.IsNullOrEmpty(file) || string.IsNullOrEmpty(who))
                    throw new InvalidDataException($"第 {rowNumber} 条记录缺少 audioFileName 或 speaker。");
                if (delay >= 0)
                {
                    string value = row.ElementAtOrDefault(delay)?.Trim();
                    float seconds = 0;
                    if (!string.IsNullOrEmpty(value) &&
                        (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) ||
                        !float.IsFinite(seconds) || seconds < 0))
                        throw new InvalidDataException($"第 {rowNumber} 条记录的 delayAfter 必须是非负有限秒数（例如 0、0.5、2），小数使用英文句点。");
                    delays.Add(seconds); // 有此列但单元格为空时明确按 0 导入。
                }
                result.Add(bindings.TryGetValue((file, who), out var matches) && matches.Count > 0
                    ? matches.Dequeue() : new NarrationLine { audioFileName = file, speaker = who });
            }
            if (result.Count == 0) throw new InvalidDataException("表格没有旁白数据。");
            // 整张表验证成功后才修改复用的对象，避免后面坏行导致前面的旧配置被部分改写。
            // 旧表没有 delayAfter 列时保留已有间隔，新句依旧默认 0。
            if (delay >= 0)
                for (int i = 0; i < result.Count; ++i) result[i].delayAfter = delays[i];
            return result;
        }

        static int Header(List<string> row, string name, bool required = true)
        {
            bool Matches(string cell) => string.Equals(cell?.Trim(), name, StringComparison.OrdinalIgnoreCase);
            int first = row.FindIndex(Matches);
            if (first < 0 && !required) return -1;
            if (first < 0 || first != row.FindLastIndex(Matches))
                throw new InvalidDataException($"表头必须包含唯一的 {name} 列。");
            return first;
        }

        static IEnumerable<List<string>> ReadCsv(string text)
        {
            var row = new List<string>();
            int position = 0;
            while (true)
            {
                Match m = CsvField.Match(text, position);
                if (!m.Success) throw new InvalidDataException($"CSV 第 {position + 1} 个字符附近的引号格式错误。");
                row.Add(m.Groups[1].Success ? m.Groups[1].Value.Replace("\"\"", "\"") : m.Groups[2].Value);
                position += m.Length;
                string delimiter = m.Groups[3].Value;
                if (delimiter == ",") continue;
                yield return row;
                if (position == text.Length) yield break;
                row = new List<string>();
            }
        }

        static IEnumerable<List<string>> ReadWorkbook(string path)
        {
            using var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var zip = new ZipArchive(file, ZipArchiveMode.Read);
            XDocument Part(string name)
            {
                var entry = zip.GetEntry(name) ?? throw new InvalidDataException($"工作簿缺少 {name}。");
                using var stream = entry.Open();
                return XDocument.Load(stream);
            }
            string Resolve(string target) => new Uri(new Uri("file:///xl/workbook.xml"), target).AbsolutePath.TrimStart('/');
            var relations = Part("xl/_rels/workbook.xml.rels").Root.Elements()
                .ToDictionary(e => (string)e.Attribute("Id"));
            var worksheet = Part("xl/workbook.xml").Descendants(Sheet + "sheet")
                .Select(e => relations[(string)e.Attribute(Rel + "id")])
                .FirstOrDefault(e => (string)e.Attribute("Type") == Rel.NamespaceName + "/worksheet");
            if (worksheet == null) throw new InvalidDataException("工作簿没有工作表。");
            string Text(XElement e) => e == null ? "" : string.Concat(e.Elements(Sheet + "t")
                .Concat(e.Elements(Sheet + "r").Elements(Sheet + "t")).Select(t => t.Value));
            var stringsPart = relations.Values.FirstOrDefault(e => (string)e.Attribute("Type") == Rel.NamespaceName + "/sharedStrings");
            var strings = stringsPart == null ? Array.Empty<string>() : Part(Resolve((string)stringsPart.Attribute("Target")))
                .Root.Elements(Sheet + "si").Select(Text).ToArray();
            foreach (var row in Part(Resolve((string)worksheet.Attribute("Target"))).Descendants(Sheet + "row"))
            {
                var values = new List<string>();
                foreach (var cell in row.Elements(Sheet + "c"))
                {
                    string address = (string)cell.Attribute("r");
                    int column = 0;
                    if (address != null)
                    {
                        foreach (char c in address.TakeWhile(c => c >= 'A' && c <= 'Z'))
                            column = checked(column * 26 + c - 'A' + 1);
                        if (column <= values.Count || column > 16384)
                            throw new InvalidDataException($"无效的单元格位置 {address}。");
                    }
                    while (values.Count + 1 < column) values.Add("");
                    string value = (string)cell.Element(Sheet + "v") ?? "";
                    switch ((string)cell.Attribute("t"))
                    {
                        case "inlineStr": value = Text(cell.Element(Sheet + "is")); break;
                        case "s": value = strings[int.Parse(value)]; break;
                    }
                    values.Add(value);
                }
                yield return values;
            }
        }
    }
}
