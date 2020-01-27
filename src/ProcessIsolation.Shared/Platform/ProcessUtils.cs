using System;
using System.Collections.Generic;
using System.Text;

namespace ProcessIsolation.Shared.Platform
{
    public static partial class ProcessUtils
    {
        public static bool ArgumentMustBeQuoted(string arg)
        {
            if (arg != null)
            {
                if (arg.Length > 1 && arg[0] == '"' && arg[arg.Length - 1] == '"')
                {
                    return false;
                }

                for (int i = 0; i < arg.Length; i++)
                {
                    if (char.IsWhiteSpace(arg[i]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static string QuoteArgumentIfRequired(string arg)
        {
            if (ArgumentMustBeQuoted(arg))
            {
                return "\"" + arg + "\"";
            }

            return arg;
        }
    }
}
