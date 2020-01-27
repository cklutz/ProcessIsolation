// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace ProcessIsolation.Shared.CommandLine
{
    internal static class CommandLineApplicationExtensions
    {
        public static CommandOption Option(this CommandLineApplication command, string template, string description)
            => command.Option(
                template,
                description,
                template.IndexOf('<', StringComparison.Ordinal) != -1
                    ? template.EndsWith(">...", StringComparison.Ordinal)
                        ? CommandOptionType.MultipleValue
                        : CommandOptionType.SingleValue
                    : CommandOptionType.NoValue);

        public static string GetString(this CommandOption option, string defaultValue = default)
        {
            if (option.HasValue())
            {
                return option.Value();
            }

            return defaultValue;
        }

        public static int GetInt32(this CommandOption option, int defaultValue = default)
        {
            if (option.HasValue())
            {
                if (!int.TryParse(option.Value(), out int result))
                {
                    throw new CommandOptionException(option,
                        $"For option '{option.LongName}': cannot convert '{option.Value()}' to type '{typeof(int)}'.");
                }

                return result;
            }

            return defaultValue;
        }

        public static long GetInt64(this CommandOption option, long defaultValue = default)
        {
            if (option.HasValue())
            {
                if (!long.TryParse(option.Value(), out long result))
                {
                    throw new CommandOptionException(option,
                        $"For option '{option.LongName}': cannot convert '{option.Value()}' to type '{typeof(long)}'.");
                }

                return result;
            }

            return defaultValue;
        }
    }
}
