using System;
using System.IO;
using System.Text;
using UnityEngine;

// Lightweight runtime logger for CampfireVR. Writes JSONL events to
//   Application.persistentDataPath/debug-logs/campfirevr-log-YYYYMMDD-HHMMSS.jsonl
// so two-headset test sessions can be reconstructed by pulling logs from
// both Quests over adb and diffing by timestamp.
//
// Design constraints (per docs/debug-logging.md):
//   - boring + reliable: no analytics service, no cloud upload, no PII
//   - per-event cost: one JSON line, one StreamWriter.WriteLine — sub-ms
//   - crash-safe: AutoFlush=true so a crash mid-session still leaves usable logs
//   - file rotation at 5 MB, retention of 10 most-recent logs
//   - boots before any scene loads via [RuntimeInitializeOnLoadMethod]
//   - swallows its own write exceptions — never crashes the game over a log
//
// Always-on for sideloaded Quest builds. Editor + Quest both write to the
// same path under their respective Application.persistentDataPath.
public class DebugLogger : MonoBehaviour
{
    private const long MaxFileSizeBytes = 5L * 1024 * 1024;  // 5 MB
    private const int MaxFileCount = 10;
    private const string LogDirName = "debug-logs";
    private const string LogFilePrefix = "campfirevr-log";
    private const string LogFileSuffix = ".jsonl";

    private static DebugLogger _instance;
    public static DebugLogger Instance => _instance;

    private string _logFilePath;
    private StreamWriter _writer;
    private readonly object _writeLock = new object();
    private int _eventCount;

    public int EventCount => _eventCount;
    public string CurrentLogFile => _logFilePath;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("DebugLogger");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<DebugLogger>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        OpenLogFile();
        RotateOldFiles();
        Application.logMessageReceived += OnUnityLog;
        WriteHeader();
    }

    void OnDestroy()
    {
        Application.logMessageReceived -= OnUnityLog;
        CloseLogFile();
    }

    void OnApplicationQuit()
    {
        Log("app_quit", "Application shutting down");
        CloseLogFile();
    }

    private void OpenLogFile()
    {
        try
        {
            var dir = Path.Combine(Application.persistentDataPath, LogDirName);
            Directory.CreateDirectory(dir);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            _logFilePath = Path.Combine(dir, $"{LogFilePrefix}-{stamp}{LogFileSuffix}");
            _writer = new StreamWriter(_logFilePath, append: true, Encoding.UTF8) { AutoFlush = true };
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DebugLogger] Could not open log file: {e.Message}");
            _writer = null;
        }
    }

    private void CloseLogFile()
    {
        lock (_writeLock)
        {
            try { _writer?.Flush(); _writer?.Dispose(); } catch { }
            _writer = null;
        }
    }

    private void RotateOldFiles()
    {
        try
        {
            var dir = Path.GetDirectoryName(_logFilePath);
            if (string.IsNullOrEmpty(dir)) return;
            var files = Directory.GetFiles(dir, $"{LogFilePrefix}-*{LogFileSuffix}");
            if (files.Length <= MaxFileCount) return;
            Array.Sort(files);  // filenames are sortable by timestamp prefix
            for (int i = 0; i < files.Length - MaxFileCount; i++)
            {
                try { File.Delete(files[i]); } catch { }
            }
        }
        catch { }
    }

    // Mirrors scripts/build-quest.sh `generate_build_info`. Loaded once at
    // session start so every log file's first event identifies which APK
    // produced it (version, build timestamp, git commit, dirty flag).
    [Serializable]
    private class BuildInfo
    {
        public string version;
        public int versionCode;
        public string buildTime;
        public string gitCommit;
        public string gitCommitLong;
        public string gitBranch;
        public bool gitDirty;
        public string apkName;
        public string changelogVersion;
        public string[] changelogSummary;
    }

    private void WriteHeader()
    {
        string buildVersion = "unknown";
        int buildVersionCode = 0;
        string buildTime = "unknown";
        string gitCommit = "unknown";
        string gitBranch = "unknown";
        bool gitDirty = false;
        string apkName = "unknown";

        try
        {
            var infoTxt = Resources.Load<TextAsset>("build-info");
            if (infoTxt != null)
            {
                var info = JsonUtility.FromJson<BuildInfo>(infoTxt.text);
                if (info != null)
                {
                    buildVersion     = string.IsNullOrEmpty(info.version) ? buildVersion : info.version;
                    buildVersionCode = info.versionCode;
                    buildTime        = string.IsNullOrEmpty(info.buildTime) ? buildTime   : info.buildTime;
                    gitCommit        = string.IsNullOrEmpty(info.gitCommit) ? gitCommit   : info.gitCommit;
                    gitBranch        = string.IsNullOrEmpty(info.gitBranch) ? gitBranch   : info.gitBranch;
                    gitDirty         = info.gitDirty;
                    apkName          = string.IsNullOrEmpty(info.apkName) ? apkName       : info.apkName;
                }
            }
        }
        catch (Exception e)
        {
            // Never crash the game over a log header. Build-info is best-effort.
            Debug.LogWarning($"[DebugLogger] Could not parse build-info.json: {e.Message}");
        }

        Log("app_started", "DebugLogger initialised",
            ("product_name", Application.productName),
            ("version", Application.version),
            ("platform", Application.platform.ToString()),
            ("device_model", SystemInfo.deviceModel),
            ("device_name", SystemInfo.deviceName),
            ("install_mode", Application.installMode.ToString()),
            ("log_file", Path.GetFileName(_logFilePath)),
            ("build_version", buildVersion),
            ("build_version_code", buildVersionCode),
            ("build_time", buildTime),
            ("git_commit", gitCommit),
            ("git_branch", gitBranch),
            ("git_dirty", gitDirty),
            ("apk_name", apkName));
    }

    // -------- Public API ----------------------------------------------

    public static void Log(string eventName, string message = null)
    {
        _instance?.WriteInternal(eventName, message, null);
    }

    public static void Log(string eventName, string message, params (string key, object value)[] details)
    {
        _instance?.WriteInternal(eventName, message, details);
    }

    public static void Marker(string note = null)
    {
        _instance?.WriteInternal("MANUAL_MARKER", note ?? "marker", null);
    }

    // -------- Internal write ------------------------------------------

    private void WriteInternal(string eventName, string message, (string key, object value)[] details)
    {
        if (_writer == null) return;

        var sb = new StringBuilder(256);
        sb.Append('{');
        sb.Append("\"ts\":\"").Append(DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff")).Append("\"");
        sb.Append(",\"mono\":").Append(Time.realtimeSinceStartup.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
        sb.Append(",\"event\":\"").Append(Escape(eventName)).Append("\"");
        if (!string.IsNullOrEmpty(message))
        {
            sb.Append(",\"msg\":\"").Append(Escape(message)).Append("\"");
        }
        if (details != null && details.Length > 0)
        {
            foreach (var pair in details)
            {
                sb.Append(",\"").Append(Escape(pair.key)).Append("\":");
                AppendValue(sb, pair.value);
            }
        }
        sb.Append('}');

        lock (_writeLock)
        {
            if (_writer == null) return;
            try
            {
                _writer.WriteLine(sb.ToString());
                _eventCount++;
                if (_writer.BaseStream != null && _writer.BaseStream.Length > MaxFileSizeBytes)
                {
                    CloseLogFile();
                    OpenLogFile();
                    RotateOldFiles();
                }
            }
            catch
            {
                // Never crash the game over a logging failure.
            }
        }
    }

    private static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
    }

    private static void AppendValue(StringBuilder sb, object v)
    {
        if (v == null) { sb.Append("null"); return; }
        switch (v)
        {
            case bool b: sb.Append(b ? "true" : "false"); break;
            case int i: sb.Append(i); break;
            case long l: sb.Append(l); break;
            case float f: sb.Append(f.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)); break;
            case double d: sb.Append(d.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)); break;
            default: sb.Append('"').Append(Escape(v.ToString())).Append('"'); break;
        }
    }

    // Capture Unity-side errors and exceptions only — warnings and info
    // flood the log too easily and add little signal.
    private void OnUnityLog(string condition, string stackTrace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        WriteInternal("unity_error", condition, new[]
        {
            ((string)"type", (object)type.ToString())
        });
    }
}
