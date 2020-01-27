using System;
using System.Threading;
using ProcessIsolation.Shared.CommandLine;

namespace ProcessIsolation.Host
{
    internal class HostCommandContext : CommandContext
    {
        private readonly ManualResetEvent m_startComplete;
        private readonly CancellationTokenSource m_tokenSource;

        public HostCommandContext(
            IServiceProvider serviceProvider,
            ManualResetEvent startComplete,
            CancellationTokenSource tokenSource)
            : base(serviceProvider)
        {
            m_startComplete = startComplete ?? throw new ArgumentNullException(nameof(startComplete));
            m_tokenSource = tokenSource ?? throw new ArgumentNullException(nameof(tokenSource));
        }

        public void SetStartComplete() => m_startComplete.Set();
        public CancellationToken Token => m_tokenSource.Token;
    }
}
