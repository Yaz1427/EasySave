using System;
using System.Diagnostics;
using System.Linq;

namespace EasySave.Services
{
    public class BusinessSoftwareService
    {
        private string _processName;

        public BusinessSoftwareService(string processName)
        {
            _processName = processName;
        }

        public string ProcessName
        {
            get => _processName;
            set => _processName = value;
        }

        /// <summary>
        /// Checks if the business software is currently running.
        /// </summary>
        public bool IsRunning()
        {
            if (string.IsNullOrWhiteSpace(_processName))
                return false;

            try
            {
                var processes = Process.GetProcessesByName(_processName);
                bool found = processes.Length > 0;
                foreach (var p in processes)
                    p.Dispose();
                return found;
            }
            catch
            {
                return false;
            }
        }
    }
}
