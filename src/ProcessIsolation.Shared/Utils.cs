using System;
using System.Globalization;

namespace ProcessIsolation.Shared
{
    internal static class Utils
    {
        public static string FormatBytes(long size, bool fractions = true)
        {
            if (size < 0)
            {
                return "-" + FormatBytes((ulong)(-1 * size), fractions);
            }

            return FormatBytes((ulong)size, fractions);
        }

        public static string FormatBytes(ulong size, bool fractions = true, CultureInfo cultureInfo = null)
        {
            const double kiloByte = 1024.0;
            const double megaByte = kiloByte * kiloByte;
            const double gigaByte = megaByte * kiloByte;
            const double teraByte = gigaByte * kiloByte;

            cultureInfo = cultureInfo ?? CultureInfo.CurrentCulture;

            if (size <= 0)
            {
                return "0 KB";
            }
            if (size < (kiloByte * 10) && fractions)
            {
                return string.Format(cultureInfo, "{0:N2} KB", (size / kiloByte));
            }
            if (size < megaByte)
            {
                return string.Format(cultureInfo, "{0:N0} KB", (size / kiloByte));
            }
            if (size < (megaByte * 10) && fractions)
            {
                return string.Format(cultureInfo, "{0:N2} MB", (size / megaByte));
            }
            if (size < gigaByte)
            {
                return string.Format(cultureInfo, "{0:N0} MB", (size / megaByte));
            }
            if (size < (gigaByte * 10) && fractions)
            {
                return string.Format(cultureInfo, "{0:N2} GB", (size / gigaByte));
            }
            if (size < teraByte)
            {
                return string.Format(cultureInfo, "{0:N0} GB", (size / gigaByte));
            }

            return string.Format(cultureInfo, "{0:N0} TB", (size / teraByte));
        }

        public static long ParseBytes(string str, CultureInfo cultureInfo = null)
        {
            if (str == null)
                throw new ArgumentNullException(nameof(str));

            if (string.IsNullOrEmpty(str))
                throw new ArgumentException("Empty value", nameof(str));

            const double kiloByte = 1024.0;
            const double megaByte = kiloByte * kiloByte;
            const double gigaByte = megaByte * kiloByte;
            const double teraByte = gigaByte * kiloByte;

            cultureInfo = cultureInfo ?? CultureInfo.CurrentCulture;

            str = str.Replace(" ", "", StringComparison.Ordinal);
            int pos = str.ToUpperInvariant().IndexOfAny(new[] { 'B', 'K', 'M', 'G', 'T' });
            if (pos == -1)
            {
                return long.Parse(str, cultureInfo);
            }

            double number = double.Parse(str.Substring(0, pos), cultureInfo);
            switch (str[pos])
            {
                case 'B': return (long)number;
                case 'K': return (long)(number * kiloByte);
                case 'M': return (long)(number * megaByte);
                case 'G': return (long)(number * gigaByte);
                case 'T': return (long)(number * teraByte);
            }

            throw new FormatException($"Invalid specification '{str}'");
        }

        public static void RethrowInnerException(this Exception ex)
        {
            if (ex == null)
                throw new ArgumentNullException(nameof(ex));

            if (ex.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
        }

        public static bool IsFatal(this Exception ex)
        {
            if (ex == null)
                throw new ArgumentNullException(nameof(ex));

            while (ex != null)
            {
                if (ex is OutOfMemoryException && !(ex is InsufficientMemoryException) ||
                    ex is System.Threading.ThreadAbortException ||
                    ex is AccessViolationException ||
                    ex is System.Runtime.InteropServices.SEHException)
                {
                    return true;
                }

                if (ex is AggregateException agg && agg.InnerExceptions != null)
                {
                    // Avoid allocating memory; don't use LINQ here.

                    for (int i = 0; i < agg.InnerExceptions.Count; i++)
                    {
                        if (IsFatal(agg.InnerExceptions[i]))
                            return true;
                    }

                    return false;
                }

                ex = ex.InnerException;
            }
            return false;
        }
    }
}
