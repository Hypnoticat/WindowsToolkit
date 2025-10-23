using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LowLevelController;

// struct definitions for sendInput
[StructLayout(LayoutKind.Sequential)]
struct Input
{
    public uint type;
    public InputUnion union;
    public static int Size => Marshal.SizeOf<Input>();
}

[StructLayout(LayoutKind.Explicit)]
struct InputUnion
{
    [FieldOffset(0)] public MouseInput mi;
    [FieldOffset(0)] public KeyboardInput ki;
    [FieldOffset(0)] public HardwareInput hi;
}

[StructLayout(LayoutKind.Sequential)]
struct MouseInput
{
    public int dx;
    public int dy;
    public uint mouseData;
    public uint dwFlags;
    public uint time;
    public IntPtr dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
struct KeyboardInput
{
    public ushort wVk;
    public ushort wScan;
    public uint dwFlags;
    public uint time;
    public IntPtr dwExtraInfo; 
}

[StructLayout(LayoutKind.Sequential)]
struct HardwareInput
{
    public uint uMsg;
    public ushort wParamL;
    public ushort wParamH;
}

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
    private const int WM_KEYDOWN = 0x100;
    private const int WM_KEYUP = 0x101;

    private static int[] heldKeys = new int[255];
    
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
    
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SendInput(uint numInputs, IntPtr buf, int bufSize);
    
    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);
    
    // the current device being monitored
    private static DeviceType inputDevice = DeviceType.Unset;
    
    private static Process? targetProc = null;
    
    /// <summary>
    /// Inserts the given hook process into the hook chain for the given process
    /// </summary>
    /// <param name="hkProc">The hook code</param>
    /// <param name="targetProc">The process to insert the hook into</param>
    /// <returns>0 for success, 1 for process non-existent</returns>
    private static IntPtr SetHook(InputProc hkProc)
    {
        // insert the hook into the chain
        Process curProc = Process.GetCurrentProcess();
        if (curProc.MainModule != null)
        {
            return SetWindowsHookEx((int)inputDevice, hkProc, GetModuleHandle(curProc.MainModule.ModuleName), 0);
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
                
                uint dwDevice;
                switch(inputDevice)
                {
                    case DeviceType.Mouse:
                        dwDevice = 0;
                        break;
                    case DeviceType.Keyboard:
                        dwDevice = 1;
                        break;
                    default:
                        dwDevice = 2;
                        break;
                }

                uint dwAct;
                switch (wParam)
                {
                    case WM_KEYDOWN:
                        dwAct = 0;
                        break;
                    case WM_KEYUP:
                        dwAct = 2;
                        break;
                    default:
                        dwAct = 0;
                        break;
                }

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
                
                if(vkCode > 255 || vkCode < 0){ return 1; }
                else if (wParam == WM_KEYDOWN)
                {
                    heldKeys[vkCode] = 1;
                }
                else if (wParam == WM_KEYUP)
                {
                    heldKeys[vkCode] = 0;
                }
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
    public static IntPtr RemoveHook()
    {
        return UnhookWindowsHookEx(hookId) ? IntPtr.Zero : 1;
    }

    /// <summary>
    /// Sets the process to attach the hook to
    /// </summary>
    /// <param name="process">The process to attach to(has type Process, DO NOT PASS THE NAME)</param>
    public void SetProcess(Process? process)
    {
        if(hookId != 0){ RemoveHook(); }
        targetProc = process;
    }

    /// <summary>
    /// Sets the input device to interrupt
    /// </summary>
    /// <param name="device">The type of device</param>
    public void SetDevice(DeviceType device)
    {
        if(hookId != 0){ RemoveHook(); }
        inputDevice = device;
    }

    public void AddHook()
    {
        if (inputDevice != DeviceType.Unset && targetProc != null)
        {
            if(hookId != 0){ RemoveHook(); }
            hookId = SetHook(hkProc); 
        }
    }
}