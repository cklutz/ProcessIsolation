using System;
using System.Diagnostics;
using System.Threading;

// NOTE: This class cannot be put into a namespace.

/// <summary>
/// Startup hooks are pieces of code that will run before a users program main executes
/// See: https://github.com/dotnet/core-setup/blob/master/Documentation/design-docs/host-startup-hook.md
/// The type must be named StartupHook without any namespace, and should be internal.
/// </summary>
internal class StartupHook
{
    /// <summary>
    /// Startup hooks are pieces of code that will run before a users program main executes
    /// See: https://github.com/dotnet/core-setup/blob/master/Documentation/design-docs/host-startup-hook.md
    /// </summary>
    public static void Initialize()
    {
        // To make use of this, the caller is expected to set the IsolationOptions.Debug property to true.
        if (Environment.GetEnvironmentVariable("PROCISO_DEBUG_ENABLED") == "true")
        {
            Console.Error.WriteLine("**** Waiting for debugger to attach (process ID {0})...",
                Process.GetCurrentProcess().Id);

            while (!Debugger.IsAttached)
            {
                Thread.Sleep(1000);
            }
        }
    }
}
