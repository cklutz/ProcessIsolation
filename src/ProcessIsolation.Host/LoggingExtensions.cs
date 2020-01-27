using System;
using Microsoft.Extensions.Logging;
using ProcessIsolation.Shared;
using ProcessIsolation.Shared.Platform;

namespace ProcessIsolation.Host
{
    internal static class LoggingExtensions
    {
        public static void HostProcessStart(this ILogger logger, int processId)
        {
            logger?.LogInformation(EventIds.HostProcessStart,
                "Host process starting. Process ID is {ProcessId}",
                processId);
        }

        public static void ProcessLimitSet(this ILogger logger, string limit, object value)
        {
            string str;
            if (value is IntPtr ip && limit == nameof(IsolationLimits.AffinityMask))
            {
                str = ProcessAffinity.ToString(ip, Environment.ProcessorCount);
            }
            else if (value is long ll && limit == nameof(IsolationLimits.MaxMemory))
            {
                str = Utils.FormatBytes(ll);
            }
            else if (value is int i && limit == nameof(IsolationLimits.MaxCpuUsage))
            {
                str = i + "%";
            }
            else
            {
                str = value?.ToString() ?? "(null)";
            }

            logger?.LogInformation(EventIds.ProcessLimitSet,
                "Process limit {LimitName} set to {LimitValue}",
                limit,
                str);
        }

        public static void IpcServiceHostStarting(this ILogger logger, string pipeName)
        {
            logger?.LogInformation(EventIds.IpcServiceHostStarting,
                "Starting IPC service host for pipe {PipeName}", pipeName);
        }

        public static void InvokeMethodException(this ILogger logger, string assemblyPath, string typeAndMethodName, string[] args, Exception ex)
        {
            logger?.LogInformation(EventIds.InvokeMethodException, ex,
                "Invoking {MethodName}({Arguments}) from {Assembly} failed",
                typeAndMethodName, args, assemblyPath);
        }

        public static void InvokeMethodInvalidSpecification(this ILogger logger, string typeAndMethodName)
        {
            logger?.LogInformation(EventIds.InvokeMethodInvalidSpecification,
                "Invalid method/type specification {TypeAndMethodName}", typeAndMethodName);
        }

        public static void InvokeMethodAssemblyLoadFailure(this ILogger logger, string assemblyPath, Exception ex)
        {
            logger?.LogInformation(EventIds.InvokeMethodAssemblyLoadFailure, ex,
                "Failed to load the assembly from {AssemblyPath}", assemblyPath);
        }

        public static void InvokeMethodTypeNotFound(this ILogger logger, string typeName, string assemblyPath, Exception ex)
        {
            logger?.LogInformation(EventIds.InvokeMethodTypeNotFound, ex,
                "Failed to locate the type {TypeName} in {AssemblyPath}", typeName, assemblyPath);
        }

        public static void InvokeMethodMethodNotFound(this ILogger logger, string signature, string assemblyPath, Exception ex)
        {
            logger?.LogInformation(EventIds.InvokeMethodMethodNotFound, ex,
                "Failed to locate the method {MethodSignature} in {AssemblyPath}", signature, assemblyPath);
        }

    }
}
