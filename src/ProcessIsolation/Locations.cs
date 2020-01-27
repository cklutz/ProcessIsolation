using ProcessIsolation.Shared;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ProcessIsolation
{
    internal class Locations
    {
        public Locations()
        {
            HostAssemblyPath = GetHostAssemblyPath();
            HostExecutablePath = GetHostExecutablePath();
            DotNetExecutablePath = GetDotNetExecutablePath();
            HookAssemblyPath = GetHookAssemblyPath();
        }

        public string HostAssemblyPath { get; }
        public string HostExecutablePath { get; }
        public string DotNetExecutablePath { get; }
        public string HookAssemblyPath { get; }

        private static string GetDotNetExecutablePath() =>
            DotNetMuxer.MuxerPathOrDefault();

        private static string GetHookAssemblyPath() =>
            typeof(StartupHook).Assembly.Location;

        private static string GetHostAssemblyPath() =>
            // DLL suffix is used on Windows and UNIX/MacOS alike
            Path.Combine(Path.GetDirectoryName(typeof(ProcessIsolator).Assembly.Location), "pihost.dll");

        private static string GetHostExecutablePath()
        {
            string executable = "pihost";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                executable += ".exe";
            }

            return Path.Combine(Path.GetDirectoryName(typeof(ProcessIsolator).Assembly.Location), executable);
        }
    }
}
