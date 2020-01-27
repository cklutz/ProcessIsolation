// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace ProcessIsolation.Shared.CommandLine
{
#pragma warning disable CA1032 // Implement standard exception constructors
#pragma warning disable CA1064 // Exceptions should be public

    internal class CommandParsingException : CommandException
    {
        public CommandParsingException(CommandLineApplication command, string message)
            : base(message)
        {
            Command = command;
        }

        public CommandLineApplication Command { get; }
    }

    internal class CommandOptionException : CommandException
    {
        public CommandOptionException(CommandOption option, string message)
            : base(message)
        {
            Option = option;
        }

        public CommandOption Option { get; }
    }
}
