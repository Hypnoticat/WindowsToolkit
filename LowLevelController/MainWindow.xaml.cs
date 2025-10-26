using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Management;

namespace LowLevelController;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public ObservableCollection<string> runningProcs { get; set; }
    private Dictionary<int, string> idToName = new Dictionary<int, string>();
    private GeneralController keyboardController;
    
    private ManagementEventWatcher procRun;
    private ManagementEventWatcher procTerm;
    
    public MainWindow()
    {
        InitializeComponent();
        runningProcs = new ObservableCollection<string>();
        this.DataContext = this;
        
        keyboardController = new GeneralController();
        keyboardController.SetDevice(GeneralController.DeviceType.Keyboard);
        MonitorProcesses();
    }

    public void MonitorProcesses()
    {
        foreach (Process proc in Process.GetProcesses().Where(proc => proc.MainWindowHandle != 0))
        {
            runningProcs.Add(proc.ProcessName);
            idToName.Add(proc.Id, proc.ProcessName);
        }
        procRun = new ManagementEventWatcher("SELECT * FROM Win32_ProcessStartTrace");
        procTerm = new ManagementEventWatcher("SELECT * FROM Win32_ProcessStopTrace");


        procRun.EventArrived += async (s, e) =>
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
                        runningProcs.Add(procName);
                    });
                }
            }
            catch{}
        };

        procTerm.EventArrived += (s, e) =>
        {
            int procId = Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value);
            try
            {
                string procName = idToName[procId];
                if (runningProcs.Contains(procName))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        runningProcs.Remove(procName);
                    });
                }
            }
            catch{}
        };

        procRun.Start();
        procTerm.Start();
    }

    public void AddHook(object sender, RoutedEventArgs e)
    {
        keyboardController.SetProcess(Process.GetProcessesByName((string)ProcessChoice.SelectedValue)[0]);
        keyboardController.AddHook();
    }
}