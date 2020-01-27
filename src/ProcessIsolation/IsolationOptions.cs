using System;
using Microsoft.Extensions.Logging;
using ProcessIsolation.Shared;

namespace ProcessIsolation
{
    public class IsolationOptions
    {
        private bool m_dieOnCrash;
        private bool m_restartAfterCrash;
        private int m_maxRestartAttempts = 10;
        private bool m_createJobObject = true;
        private TimeSpan m_startWaitTimeout = TimeSpan.Zero;
        private bool m_startWithExeLauncher;
        private IsolationLimits m_limits = new IsolationLimits();
        private IsolationEvents m_events = new IsolationEvents();
        private bool m_debug;
        private int m_listenerThreads = 2;
        private LogLevel m_logLevel = LogLevel.Warning;

        public IsolationOptions()
        {
        }

        public bool DieOnCrash
        {
            get => m_dieOnCrash;
            set
            {
                CheckReadOnly();
                m_dieOnCrash = value;
            }
        }

        public bool RestartAfterCrash
        {
            get => m_restartAfterCrash;
            set
            {
                CheckReadOnly();
                m_restartAfterCrash = value;
            }
        }

        public int MaxRestartAttempts
        {
            get => m_maxRestartAttempts;
            set
            {
                CheckReadOnly();
                m_maxRestartAttempts = value;
            }
        }

        public bool CreateJobObject
        {
            get => m_createJobObject;
            set
            {
                CheckReadOnly();
                m_createJobObject = value;
            }
        }

        public TimeSpan StartWaitTimeout
        {
            get => m_startWaitTimeout;
            set
            {
                CheckReadOnly();
                m_startWaitTimeout = value;
            }
        }

        public bool StartWithExeLauncher
        {
            get => m_startWithExeLauncher;
            set
            {
                CheckReadOnly();
                m_startWithExeLauncher = value;
            }
        }

        public IsolationLimits Limits
        {
            get => m_limits;
            set
            {
                CheckReadOnly();
                m_limits = value;
            }
        }

        public IsolationEvents Events
        {
            get => m_events;
            set
            {
                CheckReadOnly();
                m_events = value;
            }
        }

        public bool Debug
        {
            get => m_debug;
            set
            {
                CheckReadOnly();
                m_debug = value;
            }
        }

        public int ListenerThreads
        {
            get => m_listenerThreads;
            set
            {
                CheckReadOnly();
                m_listenerThreads = value;
            }
        }

        public LogLevel LogLevel
        {
            get => m_logLevel;
            set
            {
                CheckReadOnly();
                m_logLevel = value;
            }
        }

        public bool IsReadOnly { get; private set; }

        private void CheckReadOnly()
        {
            if (IsReadOnly)
            {
                throw new InvalidOperationException("Instance is read only.");
            }
        }

        internal void SanitizeAndSetReadOnly()
        {
            if (Events == null)
            {
                Events = new IsolationEvents();
            }

            if (Limits == null)
            {
                Limits = new IsolationLimits();
            }

            IsReadOnly = true;
        }
    }
}
