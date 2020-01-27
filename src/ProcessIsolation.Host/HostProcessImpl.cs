using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.Logging;
using ProcessIsolation.Shared;
using ProcessIsolation.Shared.Ipc;

namespace ProcessIsolation.Host
{
    public sealed class HostProcessImpl : IHostProcess
    {
        private readonly ManualResetEvent m_startComplete;
        private readonly ManualResetEvent m_shutdownInitiated;
        private readonly CancellationTokenSource m_source;
        private readonly ILogger<HostProcessImpl> m_logger;

        public HostProcessImpl(
            CancellationTokenSource source,
            ManualResetEvent startupComplete,
            ManualResetEvent shutdownInitiated,
            ILogger<HostProcessImpl> logger)
        {
            m_source = source ?? throw new ArgumentNullException(nameof(source));
            m_startComplete = startupComplete ?? throw new ArgumentNullException(nameof(startupComplete));
            m_shutdownInitiated = shutdownInitiated ?? throw new ArgumentNullException(nameof(shutdownInitiated));
            m_logger = logger;
        }

        //
        // IMPORTANT: IHostProcess implementation must be public (not explicit),
        // otherwise runtime errors like "The method 'xxx' not found in interface 'xxx'."
        //

        public void SetData(string name, object value)
        {
            AppDomain.CurrentDomain.SetData(name, value);
        }

        public object GetData(string name)
        {
            return AppDomain.CurrentDomain.GetData(name);
        }

        public int GetProcessId()
        {
            return Process.GetCurrentProcess().Id;
        }

        public ResourceUsage GetResourceUsage()
        {
            return ResourceUsage.Create();
        }

        public void Abort(string reason)
        {
            Environment.FailFast(reason ?? "Fail fast triggered by RPC");
        }

        public void Quit(string reason)
        {
            m_shutdownInitiated.Set();
            m_source.Cancel();
        }

        public bool WaitForStartupComplete(TimeSpan timeout)
        {
            return m_startComplete.WaitOne(timeout);
        }

        public bool WaitForShutdownInitiation(TimeSpan timeout)
        {
            try
            {
                return m_shutdownInitiated.WaitOne(timeout);
            }
            catch (ObjectDisposedException)
            {
                // Race condition: Dispose() may have already been called.
                // Semantically, this means we assume the process is in shutdown.
                return true;
            }
        }

        public int InvokeMethod(string assemblyPath, string typeAndMethodName, string[] args)
        {
            if (string.IsNullOrEmpty(assemblyPath))
            {
                throw new InvalidParameterException(nameof(assemblyPath), assemblyPath);
            }

            if (string.IsNullOrEmpty(typeAndMethodName))
            {
                throw new InvalidParameterException(nameof(typeAndMethodName), typeAndMethodName);
            }

            int pos = typeAndMethodName.LastIndexOf('.');
            if (pos == -1)
            {
                m_logger.InvokeMethodInvalidSpecification(typeAndMethodName);
                throw new InvalidOperationException($"Invalid method/type specification '{typeAndMethodName}'");
            }

            string typeName = typeAndMethodName.Substring(0, pos);
            string methodName = typeAndMethodName.Substring(pos + 1);

            if (string.IsNullOrWhiteSpace(typeName) || string.IsNullOrWhiteSpace(methodName))
            {
                m_logger.InvokeMethodInvalidSpecification(typeAndMethodName);
                throw new InvalidOperationException($"Invalid method/type specification '{typeAndMethodName}'");
            }

            var loadContext = new LoadContext(assemblyPath, null);

            Assembly assembly;
            try
            {
                assembly = loadContext.LoadFromAssemblyName(AssemblyName.GetAssemblyName(assemblyPath));
            }
            catch (Exception ex)
            {
                m_logger.InvokeMethodAssemblyLoadFailure(assemblyPath, ex);
                throw;
            }

            Type type;
            try
            {
                type = assembly.GetType(typeName, true);
            }
            catch (Exception ex)
            {
                m_logger.InvokeMethodTypeNotFound(typeName, assemblyPath, ex);
                throw;
            }

            MethodInfo method;
            string signature = "static Int32 " + typeName + "." + methodName + "(String[])";
            try
            {
                method = type.GetMethod(methodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null,
                    new[] { typeof(string[]) }, null);

                if (method == null || method.ReturnType != typeof(int))
                {
                    throw new InvalidOperationException($"Method '{signature}' not found.");
                }
            }
            catch (Exception ex)
            {
                m_logger.InvokeMethodMethodNotFound(signature, assemblyPath, ex);
                throw;
            }

            try
            {
                return (int)method.Invoke(null, new object[] { args });
            }
            catch (Exception ex)
            {
                m_logger.InvokeMethodException(assemblyPath, typeAndMethodName, args, ex);

                if (ex.InnerException is TargetInvocationException)
                {
                    ex.RethrowInnerException();
                }

                throw;
            }
        }
    }
}
