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
    
    private static IntPtr hookId = IntPtr.Zero;
    private static readonly InputProc hkProc = HookCallback;
    
    // layout of a hook call
    private delegate IntPtr InputProc(int code, IntPtr wParam, IntPtr lParam);
    
    // relevant DLL imports
    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial IntPtr SetWindowsHookEx(int hookId, InputProc hookProc, IntPtr dllHandle, uint threadId);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnhookWindowsHookEx(IntPtr hookHandle);

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr GetModuleHandle(string lpModuleName);
    
    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial void SendInput(uint numInputs, IntPtr buf, int bufSize);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial void SetForegroundWindow(IntPtr hWnd);
    
    // the current device being monitored
    private static DeviceType inputDevice = DeviceType.Unset;
    
    private static Process? targetProc;
    
    /// <summary>
    /// Inserts the given hook process into the hook chain for the given process
    /// </summary>
    /// <param name="hook">The hook code</param>
    /// <returns>0 for success, 1 for process non-existent</returns>
    private static IntPtr SetHook(InputProc hook)
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
    private static IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        // read input and decide what to do with it
        int vkCode = Marshal.ReadInt32(lParam);
        if (code >= 0 && inputDevice == DeviceType.Keyboard && wParam == WM_KEYDOWN && vkCode == VK_ESCAPE)
        {
            Console.WriteLine("escaped the hook");
            if(hookId != 0){ return RemoveHook(); }
        }
        else
        {
            if (targetProc != null)
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
                
                SetForegroundWindow(targetProc.MainWindowHandle);
                
                IntPtr inpLoc = Marshal.AllocHGlobal(Marshal.SizeOf<Input>());
                Marshal.StructureToPtr(inp, inpLoc, false);
                SendInput(1, inpLoc, Input.Size);
            }
        }
        Console.WriteLine($"hook was called");
        
        // continue the input hook chain
        //return CallNextHookEx(hookId, code, wParam, lParam);
        return 1;
    }

    /// <summary>
    /// Removes this hook from its current process
    /// </summary>
    /// <returns></returns>
    private static IntPtr RemoveHook()
    {
        return UnhookWindowsHookEx(hookId) ? IntPtr.Zero : 1;
    }

    /// <summary>
    /// Sets the process to attach the hook to
    /// </summary>
    /// <param name="process">The process to attach to(has type Process, DO NOT PASS THE NAME)</param>
    public static void SetProcess(Process? process)
    {
        if(hookId != 0){ RemoveHook(); }
        targetProc = process;
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

    public static void AddHook()
    {
        if (inputDevice != DeviceType.Unset && targetProc != null)
        {
            if(hookId != 0){ RemoveHook(); }
            hookId = SetHook(hkProc); 
        }
    }
}