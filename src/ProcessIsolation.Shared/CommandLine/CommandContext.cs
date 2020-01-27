using System;

namespace ProcessIsolation.Shared.CommandLine
{
    internal class CommandContext
    {
        public CommandContext(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public IServiceProvider ServiceProvider { get; }
    }
}
