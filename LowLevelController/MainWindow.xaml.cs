using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;

namespace LowLevelController;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    // ReSharper disable once MemberCanBePrivate.Global
    public ObservableCollection<string> RunningProcs { get; set; }
    private readonly GeneralController keyboardController;
    private readonly ProcessMonitor processMonitor;
    
    public MainWindow()
    {
        InitializeComponent();
        keyboardController = new GeneralController();

        RunningProcs = [];
        processMonitor = new ProcessMonitor(RunningProcs);
        processMonitor.MonitorProcesses();
        
        GeneralController.SetDevice(GeneralController.DeviceType.Keyboard);
        DataContext = this;
    }

    private void AddHook(object sender, RoutedEventArgs e)
    {
        int procId = processMonitor.IdFromName((string)ProcessChoice.SelectedValue);
        if (procId != 0)
        {
            GeneralController.SetProcess(Process.GetProcessById(procId));
            GeneralController.AddHook();
        }
    }
}