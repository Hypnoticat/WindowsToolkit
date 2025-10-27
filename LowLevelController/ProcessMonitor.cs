using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Management;
using System.Windows;

namespace LowLevelController;

public class ProcessMonitor(ObservableCollection<string> pCollection)
{
    private ObservableCollection<string> RunningProcs { get; set; } = pCollection;
    private readonly Dictionary<int, string> idToName = new Dictionary<int, string>();
    
    private readonly ManagementEventWatcher procRun = new("SELECT * FROM Win32_ProcessStartTrace");
    private readonly ManagementEventWatcher procTerm = new("SELECT * FROM Win32_ProcessStopTrace");

    public void MonitorProcesses()
    {
        foreach (Process proc in Process.GetProcesses().Where(proc => proc.MainWindowHandle != 0))
        {
            RunningProcs.Add(proc.ProcessName);
            idToName.Add(proc.Id, proc.ProcessName);
        }

        procRun.EventArrived += async (_, e) =>
        {
            await Task.Delay(1000);
            int procId = Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value);

            try
            {
                string procName = Process.GetProcessById(procId).ProcessName;
                idToName.Add(procId, procName);
                if (Process.GetProcessById(procId).MainWindowHandle != 0)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        RunningProcs.Add(procName);
                    });
                }
            }
            catch
            {
                // ignored
            }
        };

        procTerm.EventArrived += (_, e) =>
        {
            int procId = Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value);
            try
            {
                string procName = idToName[procId];
                if (RunningProcs.Contains(procName))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        RunningProcs.Remove(procName);
                    });
                }
            }
            catch
            {
                // ignored
            }
        };

        procRun.Start();
        procTerm.Start();
    }

    public int IdFromName(string procName)
    {
        return idToName.FirstOrDefault(x => x.Value == procName).Key;
    }
}