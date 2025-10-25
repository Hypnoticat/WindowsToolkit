using System.Collections.ObjectModel;
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
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace LowLevelController;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary
public partial class MainWindow : Window
{
    private ManagementEventWatcher procRun;
    private ManagementEventWatcher procTerm;
    
    public ObservableCollection<String> MainProcs { get; set; }
    public ObservableCollection<String> ChildProcs { get; set; }
    private GeneralController keyboardController;
    
    public MainWindow()
    {
        InitializeComponent();
        this.DataContext = this;
        MainProcs = new ObservableCollection<String>();
        ChildProcs = new ObservableCollection<String>();
        
        keyboardController = new GeneralController();
        keyboardController.SetDevice(GeneralController.DeviceType.Keyboard);
        MonitorProcesses();
    }

    public void MonitorProcesses()
    {
        foreach (Process proc in Process.GetProcesses().Where(proc => proc.MainWindowHandle != 0))
        {
            MainProcs.Add(proc.ProcessName);
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
                if (Process.GetProcessById(procId).MainWindowHandle != 0)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MainProcs.Add(procName);
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
                string procName = Process.GetProcessById(procId).ProcessName;
                if (MainProcs.Contains(procName))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MainProcs.Remove(procName);
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
        keyboardController.SetProcess(Process.GetProcessesByName((string)ProcessChild.SelectedValue).FirstOrDefault());
        keyboardController.AddHook();
    }

    private void FindControl(object sender, SelectionChangedEventArgs e)
    {
        ChildProcs.Clear();
        
        Process? parent = Process.GetProcessesByName((string)ProcessChoice.SelectedValue).FirstOrDefault();
        AutomationElement element = AutomationElement.FromHandle(parent.MainWindowHandle);
        AutomationElement control = element.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document));

        if (control != null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ChildProcs.Add(control.Current.Name);
            });
        }
    }
}