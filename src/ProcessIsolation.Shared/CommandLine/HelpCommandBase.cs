using System;

namespace ProcessIsolation.Shared.CommandLine
{
    internal class HelpCommandBase : CommandBase
    {
        private CommandLineApplication m_application;

        public override void Configure(CommandLineApplication command, CommandContext context)
        {
            m_application = command ?? throw new ArgumentNullException(nameof(command));
            base.Configure(command, context);
        }

        protected override int Execute(CommandContext context)
        {
            m_application.ShowHelp();
            return 64;
        }
    }
}
