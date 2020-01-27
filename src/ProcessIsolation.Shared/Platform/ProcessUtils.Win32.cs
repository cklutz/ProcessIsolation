using System;
using System.Globalization;

namespace ProcessIsolation.Shared.Platform
{
#if PLATFORM_WINDOWS
    public static partial class ProcessUtils
    {
        private const uint STATUS_BAD_CURRENT_DIRECTORY = 0x40000007;
        private const uint STATUS_FATAL_APP_EXIT = 0x40000015;
        private const uint STATUS_CONTROL_C_EXIT = 0xC000013A;
        private const uint STATUS_DLL_NOT_FOUND = 0xC0000135;
        private const uint STATUS_RESOURCE_NOT_OWNED = 0xC0000264;
        private const uint STATUS_UNHANDLED_EXCEPTION = 0xC0000144;
        private const uint STATUS_APP_INIT_FAILURE = 0xC0000145;
        private const uint STATUS_APPHELP_BLOCK = 0xC000035D;
        private const uint STATUS_STACK_BUFFER_OVERRUN = 0xC0000409;

        private const uint UnhandledComPlusException = 0xE0434352;
        private const uint UnhandledComPlusException2 = 0xE0434F4D;
        private const uint UnhandledComPlusExceptionPal = 0xE0524F54;

        public static string GetExitCodeDescription(int exitCode)
        {
            if (exitCode >= -255 && exitCode <= 255)
            {
                return exitCode.ToString(CultureInfo.InvariantCulture);
            }

            // If value is beyond the range of "traditional" exit codes, it is most
            // likely some OS error/status code, or, by convention the HRESULT of
            // an exception. Those are commonly displayed as hex values.
            // This helps searching for them on the internet.
            string str = "0x" + exitCode.ToString("X8", CultureInfo.InvariantCulture);

            return ((uint)exitCode) switch
            {
                STATUS_BAD_CURRENT_DIRECTORY => str + " (" + nameof(STATUS_BAD_CURRENT_DIRECTORY) + ")",
                STATUS_FATAL_APP_EXIT => str + " (" + nameof(STATUS_FATAL_APP_EXIT) + ")",
                STATUS_CONTROL_C_EXIT => str + " (" + nameof(STATUS_CONTROL_C_EXIT) + ")",
                STATUS_DLL_NOT_FOUND => str + " (" + nameof(STATUS_DLL_NOT_FOUND) + ")",
                STATUS_RESOURCE_NOT_OWNED => str + " (" + nameof(STATUS_RESOURCE_NOT_OWNED) + ")",
                STATUS_UNHANDLED_EXCEPTION => str + " (" + nameof(STATUS_UNHANDLED_EXCEPTION) + ")",
                STATUS_APP_INIT_FAILURE => str + " (" + nameof(STATUS_APP_INIT_FAILURE) + ")",
                STATUS_APPHELP_BLOCK => str + " (" + nameof(STATUS_APPHELP_BLOCK) + ")",
                STATUS_STACK_BUFFER_OVERRUN => str + " (" + nameof(STATUS_STACK_BUFFER_OVERRUN) + ")",
                UnhandledComPlusException => str + " (Unhandled COM+/managed exception, check Windows EventLog for '.NET Runtime' errors)",
                UnhandledComPlusException2 => str + " (Unhandled COM+/managed exception II, check Windows EventLog for '.NET Runtime' errors)",
                UnhandledComPlusExceptionPal => str + " (Unhandled COM+/managed exception PAL, check Windows EventLog for '.NET Runtime' errors)",
                _ => str,
            };
        }
    }
#endif
}
