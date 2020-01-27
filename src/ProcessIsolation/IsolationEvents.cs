using System;
using System.Diagnostics;

namespace ProcessIsolation
{
    public class IsolationEvents
    {
        public event DataReceivedEventHandler HostErrorDataReceived;
        public event DataReceivedEventHandler HostOutputDataReceived;
        public event EventHandler<HostStartedEventArgs> HostStarted;
        public event EventHandler<HostStartFailedEventArgs> HostStartFailed;
        public event EventHandler<HostRestartEventArgs> HostRestart;
        public event EventHandler<HostRestartAttemptEventArgs> HostRestartAttempt;
        public event EventHandler<HostRestartAttemptsExceededEventArgs> HostRestartAttemptsExceeded;
        public event EventHandler<HostExitWithErrorEventArgs> HostExitWithError;

        internal void OnErrorDataReceived(object sender, DataReceivedEventArgs args)
        {
            HostErrorDataReceived?.Invoke(this, args);
        }

        internal void OnOutputDataReceived(object sender, DataReceivedEventArgs args)
        {
            HostOutputDataReceived?.Invoke(this, args);
        }

        internal void OnHostStarted(int pid, string commandLine)
        {
            HostStarted?.Invoke(this, new HostStartedEventArgs(pid, commandLine));
        }

        internal bool OnHostStartFailed(string commandLine, Exception error)
        {
            var args = new HostStartFailedEventArgs(commandLine, error);

            HostStartFailed?.Invoke(this, args);

            return args.Handled;
        }

        internal void OnHostRestart(int oldPid, int newPid)
        {
            HostRestart?.Invoke(this, new HostRestartEventArgs(oldPid, newPid));
        }

        internal void OnHostRestartAttempt(string commandLine, int pid, int exitCode, int currentRestartAttempt,
            int maxRestartAttempts)
        {
            var args = new HostRestartAttemptEventArgs(commandLine, pid, exitCode, currentRestartAttempt, maxRestartAttempts);

            HostRestartAttempt?.Invoke(this, args);
        }

        internal bool OnHostRestartAttemptsExceeded(string commandLine, int pid, int exitCode, int maxRestartAttempts)
        {
            var args = new HostRestartAttemptsExceededEventArgs(commandLine, pid, exitCode, maxRestartAttempts);

            HostRestartAttemptsExceeded?.Invoke(this, args);

            return args.Handled;
        }

        internal bool OnHostExitWithError(int pid, int exitCode)
        {
            var args = new HostExitWithErrorEventArgs(pid, exitCode);

            HostExitWithError?.Invoke(this, args);

            return args.Handled;
        }
    }
}
