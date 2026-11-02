using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>튜토리얼 문구만 읽습니다. 단계·입력·진행 조건은 기존 담당자가 소유합니다.</summary>
public static partial class JcTutorialText
{
    public const string RelativePath = "Tutorial/TutorialText.ko.csv";
    public const string ManifestResource = "JcTutorialTextManifest";
    [Serializable] public sealed class Entry { public string id, text, scene, path; }
    [Serializable] public sealed class Manifest { public Entry[] entries; }
    static Manifest manifest;
    static Dictionary<string, string> current;
    static string lastFile;
    static float nextCheck;
    static readonly HashSet<string> warned = new HashSet<string>();
    public static string FilePath => Path.Combine(Application.streamingAssetsPath, RelativePath);
    public static Manifest Catalog { get { EnsureLoaded(); return manifest; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        manifest = null; current = null; lastFile = null; nextCheck = 0; warned.Clear();
        SceneManager.sceneLoaded -= BindScene;
        SceneManager.sceneLoaded += BindScene;
    }
    static void EnsureLoaded()
    {
        if (current != null) return;
        var asset = Resources.Load<TextAsset>(ManifestResource);
        manifest = asset != null ? JsonUtility.FromJson<Manifest>(asset.text) : new Manifest { entries = Array.Empty<Entry>() };
        current = manifest.entries.ToDictionary(e => e.id, e => e.text, StringComparer.Ordinal);
        Reload(true);
    }
    public static string Get(string id, string fallback = "")
    {
        EnsureLoaded(); Reload(false);
        if (current.TryGetValue(id, out var value)) return value;
        Warn("missing:" + id, "등록되지 않은 튜토리얼 문구 ID: " + id);
        return fallback;
    }
    public static string Format(string id, string fallback, params object[] args)
    {
        try { return string.Format(CultureInfo.CurrentCulture, Get(id, fallback), args); }
        catch (FormatException e) { Warn(id + e.Message, id + ": " + e.Message); return string.Format(CultureInfo.CurrentCulture, fallback, args); }
    }
    public static string Read(TMPro.TMP_Text label)
    {
        if (label == null) return "";
        var binding = label.GetComponent<JcTutorialTextBinding>();
        return binding != null ? binding.ResolvedText : label.text;
    }
    public static string SceneSource(TMPro.TMP_Text label)
    {
        if (label == null) return null;
        var binding = label.GetComponent<JcTutorialTextBinding>();
        return binding != null ? binding.SourceText : label.text;
    }
    public static bool Reload(bool force)
    {
        if (current == null) { EnsureLoaded(); return true; }
        if (!force && Time.realtimeSinceStartup < nextCheck) return true;
        nextCheck = Time.realtimeSinceStartup + .5f;
        try
        {
            // Desktop 제작/배포용 외부 대본. 파일 접근이 없는 플랫폼에서는 내장 원문을 사용합니다.
            if (!File.Exists(FilePath)) { Warn("file-missing", "외부 대본 파일을 찾을 수 없습니다."); return false; }
            string source = ReadSource(FilePath);
            if (!force && source == lastFile) return true;
            lastFile = source;
            if (!TryValidate(source, manifest, out var parsed, out var error)) { Warn(source, error); return false; }
            current = parsed;
            return true;
        }
        catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException || e is DecoderFallbackException)
        { Warn(e.Message, e.Message); return false; }
    }
    static void Warn(string key, string message)
    {
        if (warned.Add(key)) Debug.LogWarning("[튜토리얼 대본] " + message + "\n마지막 정상 문구를 유지합니다. " + FilePath);
    }
    public static bool TryParse(string source, out Dictionary<string, string> result, out string error)
    {
        result = new Dictionary<string, string>(StringComparer.Ordinal); error = null;
        if (!TryReadCsv(source, out var rows, out error)) return false;
        if (rows.Count == 0) { error = "CSV 헤더가 없습니다."; return false; }
        string[] expected = { "ID", "구간", "순서", "표시 상황", "종류", "문구", "비고" };
        var header = rows[0];
        if (header.Count != expected.Length || expected.Any(h => header.Count(c => c == h) != 1))
        { error = "CSV 헤더는 ID,구간,순서,표시 상황,종류,문구,비고가 각각 하나씩 필요합니다."; return false; }
        int idColumn = header.IndexOf("ID"), textColumn = header.IndexOf("문구");
        for (int i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.All(string.IsNullOrEmpty)) continue;
            if (row.Count != header.Count) { error = $"CSV 레코드 {i + 1}: 열 개수가 헤더와 다릅니다."; return false; }
            string id = row[idColumn];
            if (!Regex.IsMatch(id, @"^[a-zA-Z0-9_.-]+$")) { error = $"CSV 레코드 {i + 1}: ID가 비었거나 잘못되었습니다."; return false; }
            if (result.ContainsKey(id)) { error = $"CSV 레코드 {i + 1}: 중복 ID {id}"; return false; }
            result.Add(id, row[textColumn]);
        }
        return true;
    }
    static string Arguments(string text)
    {
        string.Format(CultureInfo.InvariantCulture, text, new object[32]);
        return string.Join(",", Regex.Matches(text, @"(?<!\{)\{(\d+)(?:[,}:])").Cast<Match>().Select(m => m.Groups[1].Value).Distinct().OrderBy(v => v));
    }
    public static bool TryValidate(string source, Manifest schema, out Dictionary<string, string> parsed, out string error)
    {
        if (!TryParse(source, out parsed, out error)) return false;
        foreach (var entry in schema.entries)
        {
            if (!parsed.TryGetValue(entry.id, out var text)) { error = "필수 ID 누락: " + entry.id; return false; }
            try { if (Arguments(text) != Arguments(entry.text)) { error = entry.id + ": {0} 등의 치환 번호를 유지해 주세요."; return false; } }
            catch (FormatException e) { error = entry.id + ": " + e.Message; return false; }
        }
        var expected = new HashSet<string>(schema.entries.Select(e => e.id), StringComparer.Ordinal);
        foreach (string id in parsed.Keys) if (!expected.Contains(id)) { error = "알 수 없는 ID: " + id; return false; }
        return true;
    }
    static void BindScene(Scene scene, LoadSceneMode mode)
    {
        EnsureLoaded();
        foreach (var entry in manifest.entries)
        {
            if (entry.scene != scene.name || string.IsNullOrEmpty(entry.path)) continue;
            var slash = entry.path.IndexOf('/');
            string rootName = slash < 0 ? entry.path : entry.path.Substring(0, slash);
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == rootName);
            var target = root == null ? null : slash < 0 ? root.transform : root.transform.Find(entry.path.Substring(slash + 1));
            var text = target != null ? target.GetComponent<TMPro.TMP_Text>() : null;
            if (text == null) { Warn("binding:" + entry.id, "씬 문구 연결 누락: " + entry.id + " / " + entry.path); continue; }
            var binding = text.GetComponent<JcTutorialTextBinding>() ?? text.gameObject.AddComponent<JcTutorialTextBinding>();
            binding.Initialize(entry.id, text);
        }
    }
}
