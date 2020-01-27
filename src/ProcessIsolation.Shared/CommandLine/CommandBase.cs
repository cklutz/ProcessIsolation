using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ProcessIsolation.Shared.CommandLine
{
    internal abstract class CommandBase
    {
        public virtual void Configure(CommandLineApplication command, CommandContext context)
        {
            command.VersionOption("--version", command.LongVersionGetter, command.ShortVersionGetter);
            command.HelpOption("-h|--help");
            command.OnExecute(() =>
            {
                Validate(context);
                return Execute(context);
            });
        }

        protected virtual void Validate(CommandContext context)
        {
        }

        protected virtual int Execute(CommandContext context)
        {
            return 0;
        }
    }
}
