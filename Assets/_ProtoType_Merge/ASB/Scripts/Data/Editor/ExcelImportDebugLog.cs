using System;
using System.IO;
using UnityEngine;

namespace ASB.ExcelImport.Editor
{
    internal static class ExcelImportDebugLog
    {
        private const string SessionId = "3f4907";
        private const string LogFileName = "debug-3f4907.log";

        // #region agent log
        public static void Write(string hypothesisId, string location, string message, string dataJson = "{}")
        {
            try
            {
                string logPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", LogFileName));
                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                string line =
                    "{\"sessionId\":\"" + SessionId +
                    "\",\"hypothesisId\":\"" + Escape(hypothesisId) +
                    "\",\"location\":\"" + Escape(location) +
                    "\",\"message\":\"" + Escape(message) +
                    "\",\"data\":" + (string.IsNullOrWhiteSpace(dataJson) ? "{}" : dataJson) +
                    ",\"timestamp\":" + timestamp + "}";

                File.AppendAllText(logPath, line + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ExcelImportDebugLog] " + ex.Message);
            }
        }
        // #endregion

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
