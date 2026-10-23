using System;
using System.Linq;
using System.Runtime.InteropServices;
class IsolationManaged
{
 [UnmanagedFunctionPointer(CallingConvention.Cdecl,CharSet=CharSet.Unicode)]
 delegate int Run([MarshalAs(UnmanagedType.LPWStr)]string bridge,[MarshalAs(UnmanagedType.LPWStr)]string apk,int major,int failure);
 [STAThread] static int Main(string[] args)
 {
  int major=Environment.Version.Major;
  Console.WriteLine($"MANAGED HOST {Environment.Version}");
  IntPtr library=NativeLibrary.Load(args[0]);
  try {
   var run=Marshal.GetDelegateForFunctionPointer<Run>(NativeLibrary.GetExport(library,"RunIsolation"));
   int mode=0;foreach(string option in args.Skip(3)){if(option=="--registered")mode|=4;else if(option=="--disabled")mode|=2;else if(option=="--fault")mode|=1;else throw new ArgumentException(option);}
   int result=run(args[1],args[2],major,mode);
   var forbidden=AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.GetName().Name.StartsWith("ApkShellext2",StringComparison.OrdinalIgnoreCase)||a.GetName().Name.Contains("Skia",StringComparison.OrdinalIgnoreCase)).ToArray();
   if(forbidden.Length>0){Console.Error.WriteLine("FAIL managed host contaminated: "+string.Join(", ",forbidden.Select(a=>a.FullName)));return 1;}
   Console.WriteLine("PASS managed assembly isolation");return result;
  } finally {NativeLibrary.Free(library);}
 }
}
