using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

internal static class RuntimeContract
{
    [ModuleInitializer]
    internal static void Validate()
    {
        if (Environment.Version.Major != 10)
            throw new InvalidOperationException("Regression requires .NET 10, got " + Environment.Version);
#if SERVICE_TEST
        var assembly = typeof(RuntimeContract).Assembly;
#else
        var assembly = typeof(ApkShellext2.AppPackageReader).Assembly;
#endif
        string target = assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
        if (target != ".NETCoreApp,Version=v10.0")
            throw new InvalidOperationException("Expected .NET 10 target, got " + target);
        Console.WriteLine("RUNTIME " + Environment.Version + " " + (Environment.Is64BitProcess ? "x64" : "x86") + " TARGET " + target + " ASSEMBLY " + assembly.Location);
    }
}
