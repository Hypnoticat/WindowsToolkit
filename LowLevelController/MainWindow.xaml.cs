using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Management;

namespace LowLevelController;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    List<Process> runningProcs = new List<Process>();
    private GeneralController keyboardController;
    
    public MainWindow()
    {
        InitializeComponent();
        
        keyboardController = new GeneralController();
        keyboardController.SetDevice(GeneralController.DeviceType.Keyboard);
        MonitorProcesses();
    }

    public void MonitorProcesses()
    {
        foreach (Process proc in Process.GetProcesses().Where(proc => proc.MainWindowHandle != 0))
        {
            ProcessChoice.Items.Add(proc.ProcessName);
        }
        ManagementEventWatcher procRun = new ManagementEventWatcher("SELECT * FROM Win32_ProcessStartTrace");
        ManagementEventWatcher procTerm = new ManagementEventWatcher("SELECT * FROM Win32_ProcessStopTrace");


        procRun.EventArrived += (s, e) =>
        {
            Process proc = (Process) e.NewEvent.Properties["Process"].Value;
            if (proc != null && proc.MainWindowHandle != 0)
            {
                ProcessChoice.Items.Add(proc.ProcessName);
            }
        };

        procTerm.EventArrived += (s, e) =>
        {
            Process proc = (Process) e.NewEvent.Properties["Process"].Value;
            if (proc != null && runningProcs.Contains(proc) && proc.MainWindowHandle != 0)
            {
                ProcessChoice.Items.Remove(proc.ProcessName);
            }
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