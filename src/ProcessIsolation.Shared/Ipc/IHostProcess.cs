using System;

namespace ProcessIsolation.Shared.Ipc
{
    public interface IHostProcess
    {
        int GetProcessId();

        void Abort(string reason);

        void Quit(string reason);

        bool WaitForStartupComplete(TimeSpan timeout);

        bool WaitForShutdownInitiation(TimeSpan timeout);

        int InvokeMethod(string assemblyPath, string typeAndMethodName, string[] args);

        void SetData(string name, object value);

        object GetData(string name);

        ResourceUsage GetResourceUsage();
    }
}

