using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ProcessIsolation.Shared.Platform
{
#if PLATFORM_UNIX
    public static partial class ProcessUtils
    {
        public static string GetExitCodeDescription(int exitCode)
        {
            return exitCode.ToString(CultureInfo.InvariantCulture);
        }
    }
#endif
}
