using System;
using System.IO;
using UnityEngine;

public class LogWriter : MonoBehaviour
{
    private void OnEnable()
    {
        Application.logMessageReceived += WriteLog;
    }
    private void OnDisable()
    {
        Application.logMessageReceived -= WriteLog;
    }
    private void WriteLog(string logText, string stackTrace, LogType type)
    {
        string logType = "";
        switch(type)
        {
            case LogType.Exception:
            case LogType.Error:
                logType = "ERROR";
                break;
            case LogType.Warning:
                logType = "WARN";
                break;
            case LogType.Assert:
                logType = "Assert";
                break;
            default:
                logType = "LOG";
                break;
        }
        string fileName = $"{DateTime.Now:yyyyMMdd}.log";
        string filePath = Path.Combine(Application.persistentDataPath, fileName);
        try
        {
            using(StreamWriter writer = new StreamWriter(filePath, true))
            {
                writer.WriteLine($"[{logType}][{DateTime.Now:yyyy-MM-dd HH:mm:ss fff}] {logText}:{stackTrace}");
            }
        }
        catch(Exception ex)
        {
            Debug.LogError($"ログ書き込みでエラーが発生しました {ex.Message}");
        }
    }
}
