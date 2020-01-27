using System;
using System.Collections.Generic;
using System.Text;

namespace ProcessIsolation.Shared.Platform.Win32
{
    public class JobObjectEventArgs : EventArgs
    {
        public JobObjectEventType EventType { get; }
        public int? ProcessId { get; }

        public bool JobEnded { get; }
        public int MessageId { get; }
        public Exception Exception { get; }
        public List<JobObjectLimitViolation> LimitViolations { get; internal set; }

        public JobObjectEventArgs(JobObjectEventType eventType, int? processId, bool jobEnded, int messageId)
            : this(eventType, processId, jobEnded, messageId, null)
        {

        }

        public JobObjectEventArgs(JobObjectEventType eventType, int? processId, bool jobEnded, int messageId, Exception exception)
        {
            EventType = eventType;
            ProcessId = processId;
            JobEnded = jobEnded;
            MessageId = messageId;
            Exception = exception;
        }

        public override string ToString()
        {
            switch (EventType)
            {
                case JobObjectEventType.InternalError:
                    return $"Internal error: {Exception}";
                case JobObjectEventType.EndOfJobTime:
                case JobObjectEventType.ActiveProcessZero:
                case JobObjectEventType.JobMemoryLimit:
                    return $"{EventType}";
                case JobObjectEventType.EndOfProcessTime:
                case JobObjectEventType.ActiveProcessLimit:
                case JobObjectEventType.NewProcess:
                case JobObjectEventType.ExitProcess:
                case JobObjectEventType.AbnormalExitProcess:
                case JobObjectEventType.ProcessMemoryLimit:
                    return $"{EventType}: ProcessID={ProcessId}";
                case JobObjectEventType.NotificationLimit:
                {
                    if (LimitViolations != null)
                    {
                        var sb = new StringBuilder();
                        foreach (var lv in LimitViolations)
                        {
                            sb.AppendLine($"{EventType}: ProcessID={ProcessId}: " + lv);
                        }

                        return sb.ToString();
                    }

                    return $"{EventType}: ProcessID={ProcessId}";
                }
                default:
                    return $"{EventType}: MessageID={MessageId}";
            }
        }
    }
}
