using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LowLevelController;

// struct definitions for sendInput
[StructLayout(LayoutKind.Sequential)]
internal struct Input
{
    public uint type;
    public InputUnion union;
    public static int Size => Marshal.SizeOf<Input>();
}

[StructLayout(LayoutKind.Explicit)]
internal struct InputUnion
{
    [FieldOffset(0)] public MouseInput mi;
    [FieldOffset(0)] public KeyboardInput ki;
    [FieldOffset(0)] public HardwareInput hi;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MouseInput
{
    public int dx;
    public int dy;
    public uint mouseData;
    public uint dwFlags;
    public uint time;
    public IntPtr dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct KeyboardInput
{
    public ushort wVk;
    public ushort wScan;
    public uint dwFlags;
    public uint time;
    public IntPtr dwExtraInfo; 
}

[StructLayout(LayoutKind.Sequential)]
internal struct HardwareInput
{
    public uint uMsg;
    public ushort wParamL;
    public ushort wParamH;
}

public partial class GeneralController
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
    private const int WM_KEYDOWN = 0x100;
    private const int WM_KEYUP = 0x101;
    
    private IntPtr hookId = IntPtr.Zero;
    private readonly InputProc hkProc;
    
    // layout of a hook call
    private delegate IntPtr InputProc(int code, IntPtr wParam, IntPtr lParam);
    
    // relevant DLL imports
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookId, InputProc hookProc, IntPtr dllHandle, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hookHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
    
    [DllImport("user32.dll", SetLastError = true)]
    private static extern void SendInput(uint numInputs, IntPtr buf, int bufSize);

    [DllImport("user32.dll")]
    private static extern void SetForegroundWindow(IntPtr hWnd);
    
    [DllImport("user32.dll", CharSet=CharSet.Ansi, SetLastError = true)]
    private static extern short VkKeyScanEx(char c, IntPtr layout);
    
    [DllImport("user32.dll")]
    public static extern IntPtr GetKeyboardLayout(uint idThread);
    
    // the current device being monitored
    private DeviceType inputDevice = DeviceType.Unset;
    private Dictionary<Process, List<int>> procToCodes = new Dictionary<Process, List<int>>();
    private GCHandle gcHandle;
    
    public GeneralController()
    {
        hkProc = HookCallback;
        gcHandle = GCHandle.Alloc(this);
    }
    
    
    /// <summary>
    /// Inserts the given hook process into the hook chain for the given process
    /// </summary>
    /// <param name="hook">The hook code</param>
    /// <returns>0 for success, 1 for process non-existent</returns>
    private IntPtr SetHook(InputProc hook)
    {
        // insert the hook into the chain
        Process curProc = Process.GetCurrentProcess();
        if (curProc.MainModule != null)
        {
            return SetWindowsHookEx((int)inputDevice, hook, GetModuleHandle(curProc.MainModule.ModuleName), 0);
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
    private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        // read input and decide what to do with it
        int vkCode = Marshal.ReadInt32(lParam);
        if (code >= 0 && inputDevice == DeviceType.Keyboard && wParam == WM_KEYDOWN && vkCode == VK_ESCAPE)
        {
            Console.WriteLine("escaped the hook");
            if(hookId != IntPtr.Zero){ return RemoveHook(); }
        }
        else
        {
            if (procToCodes.Count != 0)
            {
                /*
                uint scanCode = MapVirtualKey((uint)vkCode, 0);
                int newParams = (int)(1 & 0xFFFF
                                 | (scanCode << 16) & 0xFF
                                 | (0)
                                 | (0)
                                 | (uint)(wParam == WM_KEYUP ? 1 << 30 : 0)
                                 | (uint)(wParam == WM_KEYUP ? 1 << 31: 0));
                
                uint curThread = GetCurrentThreadId();
                uint targetThread = GetWindowThreadProcessId(targetProc.MainWindowHandle, out uint processId);
                
                AttachThreadInput(curThread, targetThread, true);
                SetForegroundWindow(targetProc.MainWindowHandle);
                AttachThreadInput(curThread, targetThread, false);
                PostMessage(targetProc.MainWindowHandle, (uint)wParam, vkCode, newParams);*/

                uint dwDevice = inputDevice switch
                {
                    DeviceType.Mouse => 0,
                    DeviceType.Keyboard => 1,
                    _ => 2
                };

                uint dwAct = wParam switch
                {
                    WM_KEYDOWN => 0,
                    WM_KEYUP => 2,
                    _ => 0
                };

                Input inp = new Input
                {
                    type = dwDevice,
                    union = new InputUnion
                    {
                        ki = new KeyboardInput
                        {
                            wVk = (ushort)vkCode,
                            wScan = 0,
                            dwFlags = dwAct,
                            time = 0,
                            dwExtraInfo = 0
                        }
                    }
                };

                foreach (KeyValuePair<Process, List<int>> proc in procToCodes)
                {
                    if (proc.Value.Contains(vkCode))
                    {
                        SetForegroundWindow(proc.Key.MainWindowHandle);
                        IntPtr inpLoc = Marshal.AllocHGlobal(Marshal.SizeOf<Input>());
                        Marshal.StructureToPtr(inp, inpLoc, false);
                        SendInput(1, inpLoc, Input.Size);
                    }
                }
            }
        }
        
        // continue the input hook chain
        //return CallNextHookEx(hookId, code, wParam, lParam);
        return 1;
    }

    /// <summary>
    /// Removes this hook from its current process
    /// </summary>
    /// <returns></returns>
    private IntPtr RemoveHook()
    {
        procToCodes.Clear();
        IntPtr success = UnhookWindowsHookEx(hookId) ? IntPtr.Zero : 1;
        hookId = IntPtr.Zero;
        return success;
    }

    /// <summary>
    /// Sets the process to attach the hook to
    /// </summary>
    /// <param name="process">The process to attach to(has type Process, DO NOT PASS THE NAME)</param>
    public void SetProcess(Process process)
    {
        if(hookId != IntPtr.Zero){ RemoveHook(); }
        
        procToCodes.Add(process, new List<int>());
    }

    public void AddKey(Process p, char c)
    {
        short key = VkKeyScanEx(c, GetKeyboardLayout(0));
        int keycode = key & 0xFF;
        procToCodes[p].Add(keycode);
    }

    /// <summary>
    /// Sets the input device to interrupt
    /// </summary>
    /// <param name="device">The type of device</param>
    public void SetDevice(DeviceType device)
    {
        if(hookId != IntPtr.Zero){ RemoveHook(); }
        inputDevice = device;
    }

    public void AddHook()
    {
        if (inputDevice != DeviceType.Unset && procToCodes.Count != 0)
        {
            if(hookId != IntPtr.Zero){ RemoveHook(); }
            hookId = SetHook(hkProc); 
        }
    }
}