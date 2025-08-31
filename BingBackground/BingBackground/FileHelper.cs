namespace BingBackground;

using System;
using System.Diagnostics;
using System.Reflection;

public class FileHelper
{
    public static string GetExecutingFileName()
    {
        var fileName = Assembly.GetEntryAssembly()?.Location ?? Process.GetCurrentProcess().MainModule?.FileName;
        return fileName ?? throw new Exception("Could not determine current executable path.");
    }
}
