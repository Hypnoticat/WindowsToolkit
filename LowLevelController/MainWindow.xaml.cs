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
        
        keyboardController.SetDevice(GeneralController.DeviceType.Keyboard);
        DataContext = this;
    }

    private void AddHook(object sender, RoutedEventArgs e)
    {
        int procId = processMonitor.IdFromName((string)ProcessChoice.SelectedValue);
        int procIdTwo = processMonitor.IdFromName((string)ChoiceTwo.SelectedValue);
        if (procId != 0 && procIdTwo != 0)
        {
            Process procOne  = Process.GetProcessById(procId);
            Process procTwo = Process.GetProcessById(procIdTwo);
            
            keyboardController.SetProcess(procOne);
            keyboardController.SetProcess(procTwo);
            
            keyboardController.AddKey(procOne, 'h');
            keyboardController.AddKey(procOne, 'l');
            
            keyboardController.AddKey(procTwo, 'e');
            keyboardController.AddKey(procTwo, 'o');
            
            keyboardController.AddHook();
        }
    }
}