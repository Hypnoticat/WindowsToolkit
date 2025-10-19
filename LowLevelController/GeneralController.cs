using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LowLevelController;



public class GeneralController
{
    /// <summary>
    /// The device the controller will manage
    /// </summary>
    public enum DeviceType
    {
        Keyboard = 13,
        Mouse = 14,
        Unset = 2 // keyboard keystrokes
    }
    
    private const int VK_ESCAPE = 0x1B;
    
    private static IntPtr hookId = IntPtr.Zero;
    private static InputProc hkProc = HookCallback;
    
    // layout of a hook call
    private delegate IntPtr InputProc(int code, IntPtr wParam, IntPtr lParam);
    
    // relevant DLL imports
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookId, InputProc hookProc, IntPtr dllHandle, uint threadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hookHandle);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    // the current device being monitored
    private static DeviceType inputDevice = DeviceType.Unset;
    
    /// <summary>
    /// Inserts the given hook process into the hook chain for the given process
    /// </summary>
    /// <param name="hkProc">The hook code</param>
    /// <param name="targetProc">The process to insert the hook into</param>
    /// <returns>0 for success, 1 for process non-existent</returns>
    private static IntPtr SetHook(InputProc hkProc, Process targetProc)
    {
        // insert the hook into the chain
        if (targetProc.MainModule != null)
        {
            ProcessModule module = targetProc.MainModule;
            return SetWindowsHookEx((int)inputDevice, hkProc, GetModuleHandle(module.ModuleName), 0);
        }
        return 1;
    }

    /// <summary>
    ///  Determines what is done with the input
    /// </summary>
    /// <param name="code">Less than 0 indicates no action</param>
    /// <param name="wParam">Defined differently for different input types</param>
    /// <param name="lParam">Defined differently for different input types</param>
    /// <returns>0 for success, 1 for hook non-existent</returns>
    private static IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        // read input and decide what to do with it
        if (code >= 0 && inputDevice == DeviceType.Keyboard && wParam == VK_ESCAPE)
        {
            if(hookId != 0){ return RemoveHook(); }
        }
        
        // continue the input hook chain
        return CallNextHookEx(hookId, code, wParam, lParam);
    }

    /// <summary>
    /// Removes this hook from its current process
    /// </summary>
    /// <returns></returns>
    public static IntPtr RemoveHook()
    {
        return UnhookWindowsHookEx(hookId) ? IntPtr.Zero : 1;
    }

    /// <summary>
    /// Sets the process to attach the hook to
    /// </summary>
    /// <param name="process">The process to attach to(has type Process, DO NOT PASS THE NAME)</param>
    public static void SetProcess(Process process)
    {
        if (inputDevice != DeviceType.Unset)
        {
            if(hookId != 0){ RemoveHook(); }
            hookId = SetHook(hkProc, process); 
        }
    }

    /// <summary>
    /// Sets the input device to interrupt
    /// </summary>
    /// <param name="device">The type of device</param>
    public static void SetDevice(DeviceType device)
    {
        if(hookId != 0){ RemoveHook(); }
        inputDevice = device;
    }
}