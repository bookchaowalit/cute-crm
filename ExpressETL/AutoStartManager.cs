using Microsoft.Win32;

namespace ExpressETL;

/// <summary>
/// จัดการ Auto-start ผ่าน Windows Registry Run key
/// </summary>
public static class AutoStartManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "ExpressETL";

    /// <summary>
    /// เปิด auto-start — เพิ่ม entry ใน Registry Run
    /// </summary>
    public static void Enable()
    {
        try
        {
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath)) return;

            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            key?.SetValue(AppName, $"\"{exePath}\" /minimized");
        }
        catch { }
    }

    /// <summary>
    /// ปิด auto-start — ลบ entry ออกจาก Registry
    /// </summary>
    public static void Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            key?.DeleteValue(AppName, false);
        }
        catch { }
    }

    /// <summary>
    /// ตรวจสอบว่า auto-start เปิดอยู่หรือไม่
    /// </summary>
    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            var value = key?.GetValue(AppName);
            return value != null;
        }
        catch
        {
            return false;
        }
    }
}
