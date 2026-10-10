using System;
using System.Drawing;
using System.Reflection;

// Exercise the implementation in the supplied production DLL, not a source copy.
namespace ApkShellext2
{
    internal static class TypeOverlay
    {
        internal static Bitmap Compose(Bitmap main, Bitmap marker, Size size)
        {
            try {
                return (Bitmap)typeof(Utility).Assembly.GetType("ApkShellext2.TypeOverlay", true)
                    .GetMethod("Compose", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { main, marker, size });
            } catch (TargetInvocationException error) {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw();
                throw;
            }
        }
    }
}
