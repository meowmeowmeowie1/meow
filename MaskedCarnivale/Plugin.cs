using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Timers;
using System.Runtime.InteropServices;
using System.Diagnostics;

using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using Dalamud.Utility.Signatures;
using Dalamud.Hooking;

using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;

using SharpDX.Direct3D11;
using SharpDX.Mathematics.Interop;
using KeyedMutex = SharpDX.DXGI.KeyedMutex;
using SwapChain11 = SharpDX.DXGI.SwapChain;
using Texture2D = SharpDX.Direct3D11.Texture2D;
using Device11 = SharpDX.Direct3D11.Device5;
using DeviceContext11 = SharpDX.Direct3D11.DeviceContext4;

using MaskedCarnivale.Windows;
using MaskedCarnivale.Structures;
using MaskedCarnivale.DirectX.ShaderData;
using MaskedCarnivale.DirectX.RenderObjects;

using MemoryManager.Structures;

namespace MaskedCarnivale;

public unsafe class Plugin : IDalamudPlugin
{
    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static IFramework? Framework { get; private set; } = null!;
    [PluginService] public static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static IPluginLog? Log { get; private set; } = null!;
    [PluginService] public static IGameInteropProvider Interop { get; private set; } = null!;

    public string Name => "MaskedCarnivale";
    private const string CommandName = "/carnivale";

    public Configuration cfg { get; init; }

    public readonly WindowSystem WindowSystem = new("MaskedCarnivale");
    private ConfigWindow ConfigWindow { get; init; }

    private HookManager hookManager = new HookManager();
    private Texture2D? sharedTexture { get; set; } = null!;
    private KeyedMutex sharedMutex { get; set; } = null!;
    private RenderTargetView? sharedRTV { get; set; } = null!;
    private ShaderResourceView? sharedSRV { get; set; } = null!;
    private ShaderResourceView? selectedSRV { get; set; } = null!;
    private int oldRenderIndex = -1;
    // Last game texture we built selectedSRV from; when it changes we rebuild the view so the
    // mirror never samples a recycled/freed render target (the "works then goes black" bug).
    private unsafe void* lastCapturedTex = null;

    private RenderTargetManager* renderTargetManager = RenderTargetManager.Instance();

    private SamplerState? pSamplerState { get; set; } = null;
    private ShaderData psTexture = new ps_Texture();
    private ShaderData vsTexture = new vs_Texture();
    private RenderObject? orthogSquare { get; set; } = null;
    private bool isEnabled = false;
    private int shareMemType = 0;
    // Flat indices into the RenderTargetManager Texture* table (base + 0x20 + 0x8*index).
    // These drift when the game's RenderTargetManager layout changes across patches, so if the
    // mirror goes black after an update, enable "Override index" in the config and use "Dump
    // render targets" to find the new values (look for CANDIDATE entries in /xllog).
    //   204 = final composited frame WITH the game HUD (alternate: 106), overlay-free.
    //    71 = clean 3D scene, no HUD (the game's HDR scene buffer).
    private int gameWindowWithUI = 204;
    private int gameWindowWithoutUI = 71;

    // Output-window (outputwindow.exe) process + one-shot rename bookkeeping. The native
    // window title is baked into the prebuilt exe, so we relabel it at runtime to
    // "FINAL FANTASY XIV" (also makes Discord/Medal list it as the game window).
    private const string OutputWindowTitle = "FINAL FANTASY XIV";
    private Process? outputProcess = null;
    private bool outputRenamed = false;

    // One-shot marker so we log the first time the Present detour actually fires.
    private bool loggedDetourActive = false;

    // ---- Win32 interop for renaming the output window at runtime ----
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetWindowTextW(IntPtr hWnd, string lpString);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    // ---- Safe memory probing (kernel32!VirtualQuery) ----
    // We validate every render-target pointer before dereferencing it. Blindly walking the
    // RenderTargetManager table and dereferencing whatever is found causes native access
    // violations (uncatchable in .NET) that crash the game. VirtualQuery lets us confirm an
    // address is committed + readable first.
    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public uint __alignment1;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
        public uint __alignment2;
    }

    [DllImport("kernel32.dll")]
    private static extern UIntPtr VirtualQuery(IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, UIntPtr dwLength);

    private const uint MEM_COMMIT = 0x1000;
    private const uint PAGE_NOACCESS = 0x01;
    private const uint PAGE_GUARD = 0x100;
    // Readable page protections (mask off GUARD/NOCACHE/WRITECOMBINE high bits with & 0xFF first).
    private const uint PAGE_READABLE_MASK =
        0x02 /*READONLY*/ | 0x04 /*READWRITE*/ | 0x08 /*WRITECOPY*/ |
        0x20 /*EXECUTE_READ*/ | 0x40 /*EXECUTE_READWRITE*/ | 0x80 /*EXECUTE_WRITECOPY*/;

    private static unsafe bool IsReadable(IntPtr p)
    {
        if (p == IntPtr.Zero || (ulong)p < 0x10000)
            return false;
        if (VirtualQuery(p, out MEMORY_BASIC_INFORMATION mbi, (UIntPtr)(uint)sizeof(MEMORY_BASIC_INFORMATION)) == UIntPtr.Zero)
            return false;
        if (mbi.State != MEM_COMMIT)
            return false;
        uint prot = mbi.Protect;
        if ((prot & PAGE_GUARD) != 0)
            return false;
        uint baseProt = prot & 0xFF;
        if (baseProt == PAGE_NOACCESS || baseProt == 0)
            return false;
        return (baseProt & PAGE_READABLE_MASK) != 0;
    }

    // Returns the render target at the given flat index (base + 0x20 + 0x8*index), or null if
    // the slot / texture pointer is out of the struct or not safely readable. This is the ONLY
    // path that dereferences a render-target pointer, so both the dump and the render loop are
    // crash-proof against bad indices / layout drift.
    private unsafe Texture* SafeGetTexture(int index)
    {
        if (index < 0)
            return null;

        RenderTargetManager* rtm = RenderTargetManager.Instance();
        if (rtm == null)
            return null;

        // Bound the slot offset to within the struct so the slot read itself is on committed
        // memory. Fall back to a conservative cap if sizeof is unexpectedly small.
        int structSize = sizeof(RenderTargetManager);
        int maxOffset = structSize > 0x40 ? structSize : 0x2000;
        ulong slotOffset = 0x20UL + (ulong)(0x8 * index);
        if (slotOffset + 8 > (ulong)maxOffset)
            return null;

        byte* slotPtr = ((byte*)rtm) + slotOffset;
        if (!IsReadable((IntPtr)(void*)slotPtr))
            return null;

        Texture* tex = *(Texture**)slotPtr;
        if (!IsReadable((IntPtr)(void*)tex))
            return null;

        // Ensure the D3D11Texture2D field (offset 0x68 in Texture) is itself in readable memory
        // before the caller touches it.
        if (!IsReadable((IntPtr)(void*)(&tex->D3D11Texture2D)))
            return null;

        return tex;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct OutputWindowSetup
    {
        [MarshalAs(UnmanagedType.I1)]
        public byte isOutputActive;
        [MarshalAs(UnmanagedType.I1)]
        public byte isGameActive;
        [MarshalAs(UnmanagedType.I1)]
        public bool updateWindow;
        [MarshalAs(UnmanagedType.I1)]
        public bool resetSharedHandle;
        [MarshalAs(UnmanagedType.I1)]
        public bool doClose;
        [MarshalAs(UnmanagedType.I1)]
        public byte topmost;
        [MarshalAs(UnmanagedType.I1)]
        public byte newTopmost;
        [MarshalAs(UnmanagedType.I1)]
        public bool t7;
        public IntPtr sharedHandle;
        public int top;
        public int left;
        public int width;
        public int height;
        public int newTop;
        public int newLeft;
        public int newWidth;
        public int newHeight;
        

        public OutputWindowSetup()
        {
            Reset();
        }
        public void Reset()
        { 
            isOutputActive = 0;
            isGameActive = 0;
            updateWindow = false;
            resetSharedHandle = false;
            doClose = false;
            sharedHandle = 0;
            top = 0;
            left = 0;
            width = 100;
            height = 100;
            topmost = 0;
            newTop = 0;
            newLeft = 0;
            newWidth = 0;
            newHeight = 0;
            newTopmost = 0;

            t7 = false;
        }
    }
    private SharedMemoryManager smm = new SharedMemoryManager();
    private int sharedBufferSize = 1024;
    private OutputWindowSetup* outputWindowData = null;

    public unsafe Plugin()
    {
        cfg = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        cfg.enable = false;
        cfg.doUpdate = false;
        cfg.renderIndex = gameWindowWithUI;

        ConfigWindow = new ConfigWindow(this);

        WindowSystem.AddWindow(ConfigWindow);

        CommandManager.RemoveHandler(CommandName);
        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "[enable|disable]"
        });

        Framework!.Update += Update;
        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi += ToggleConfigUI;

        Initialize();
    }


    public void Dispose()
    {
        Destroy();

        Framework!.Update -= Update;
        PluginInterface.UiBuilder.Draw -= DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleConfigUI;

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string argument)
    {
        if (string.IsNullOrEmpty(argument))
        {
            ToggleConfigUI();
            return;
        }
        var regex = Regex.Match(argument, "^(\\w+) ?(.*)");
        var subcommand = regex.Success && regex.Groups.Count > 1 ? regex.Groups[1].Value : string.Empty;

        switch (subcommand.ToLower())
        {
            case "enable":
                {
                    Enable();
                    break;
                }
            case "disable":
                {
                    Disable();
                    break;
                }
        }
    }

    private void DrawUI() => WindowSystem.Draw();
    public void ToggleConfigUI() => ConfigWindow.Toggle();

    private void Update(IFramework framework)
    {
        TryRenameOutputWindow();

        if (!isEnabled && cfg.enable && outputWindowData != null && outputWindowData->isOutputActive == 0)
        {
            isEnabled = cfg.enable;
            Enable();
        }
        else if (isEnabled && !cfg.enable && outputWindowData != null && outputWindowData->isOutputActive != 0)
        {
            isEnabled = cfg.enable;
            Disable();
        }
        else if (!isEnabled && !cfg.enable && outputWindowData != null && outputWindowData->isOutputActive != 0)
        {
            isEnabled = true;
            cfg.enable = true;
        }
        else if (isEnabled && cfg.enable && outputWindowData != null && outputWindowData->isOutputActive == 0)
        {
            isEnabled = false;
            cfg.enable = false;
        }

        if (cfg.doUpdate && outputWindowData != null)
        {
            cfg.doUpdate = false;
            FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device* ffxivDevice = FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device.Instance();
            SwapChain11 swapChain11 = (SwapChain11)(IntPtr)ffxivDevice->SwapChain->DXGISwapChain;

            outputWindowData->isGameActive = (byte)shareMemType;
            outputWindowData->newTop = cfg.yPosition;
            outputWindowData->newLeft = cfg.xPosition;
            outputWindowData->newWidth = (int)ffxivDevice->SwapChain->Width;
            outputWindowData->newHeight = (int)ffxivDevice->SwapChain->Height;
            outputWindowData->newTopmost = (byte)cfg.orderStatus;
            outputWindowData->updateWindow = true;
        }
    }

    private static class Signatures
    {
        internal const string DXGIPresent = "E8 ?? ?? ?? ?? C6 43 79 00";
    }

    private void Initialize()
    {
        Interop.InitializeFromAttributes(this);

        hookManager.SetFunctionHandles(this);
        hookManager.EnableFunctionHandles();

        // Diagnostics: confirm the Present hook resolved. If this logs address 0 / null,
        // the byte signature is stale for the current game build and the mirror will stay
        // black because the detour never runs.
        if (DXGIPresentHook == null)
            Log!.Warning("MaskedCarnivale: DXGIPresent hook FAILED to resolve (signature stale). Mirror will be black.");
        else
            Log!.Info($"MaskedCarnivale: DXGIPresent hook resolved at 0x{DXGIPresentHook.Address:X}.");


        shareMemType = smm.OpenSharedMemory(sharedBufferSize, "DebugTextureOutputWindow");
        if (shareMemType == 0)
        {
            *outputWindowData = new OutputWindowSetup();
        }
        else
        {
            byte* ptrAddress = null;
            smm.mmvAccessor!.SafeMemoryMappedViewHandle.AcquirePointer(ref ptrAddress);
            outputWindowData = (OutputWindowSetup*)ptrAddress;
            if (outputWindowData->width == 0)
                outputWindowData->Reset();
        }

        FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device* ffxivDevice = FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device.Instance();
        Device11 dxDevice11 = (Device11)(IntPtr)ffxivDevice->D3D11Forwarder;
        DeviceContext11 dxDevCon11 = (DeviceContext11)(IntPtr)ffxivDevice->D3D11DeviceContext;
        
        outputWindowData->isGameActive = (byte)shareMemType;
        outputWindowData->newTop = cfg.yPosition;
        outputWindowData->newLeft = cfg.xPosition;
        outputWindowData->newWidth = (int)ffxivDevice->SwapChain->Width;
        outputWindowData->newHeight = (int)ffxivDevice->SwapChain->Height;
        outputWindowData->newTopmost = (byte)cfg.orderStatus;
        outputWindowData->updateWindow = true;

        CreateShaders(dxDevice11);
        CreateBuffers(dxDevice11, dxDevCon11);
        CreateTextures(dxDevice11, dxDevCon11);

        // Hook the real IDXGISwapChain::Present (vtable slot 8) for backbuffer/HUD capture.
        // Read the COM object's vtable and take entry 8 (QI,AddRef,Release,SetPrivateData,
        // SetPrivateDataInterface,GetPrivateData,GetParent,GetDevice, [8]=Present).
        try
        {
            IntPtr swapChainPtr = (IntPtr)ffxivDevice->SwapChain->DXGISwapChain;
            if (swapChainPtr != IntPtr.Zero)
            {
                IntPtr vtbl = Marshal.ReadIntPtr(swapChainPtr);
                IntPtr presentAddr = Marshal.ReadIntPtr(vtbl, 8 * IntPtr.Size);
                presentHook = Interop.HookFromAddress<DXGISwapChainPresentDg>(presentAddr, PresentDetour);
                presentHook.Enable();
                Log!.Info($"MaskedCarnivale: IDXGISwapChain::Present hook installed at 0x{presentAddr:X}.");
            }
        }
        catch (Exception e)
        {
            Log!.Warning($"MaskedCarnivale: failed to install Present hook ({e.Message}); backbuffer/HUD mode unavailable.");
        }
    }

    private void Destroy()
    {
        outputWindowData->isGameActive = 0;
        outputWindowData->doClose = true;

        DestroyTextures();
        DestroyBuffers();
        DestroyShaders();

        hookManager.DisableFunctionHandles();
        hookManager.DisposeFunctionHandles();

        presentHook?.Disable();
        presentHook?.Dispose();
        presentHook = null;

        smm.CloseSharedMemory();
    }

    private void Enable()
    {
        string OutputWindow = Path.Combine(PluginInterface!.AssemblyLocation.DirectoryName!, "outputwindow.exe");
        if (File.Exists(OutputWindow))
        {
            outputRenamed = false;
            outputProcess = Process.Start(OutputWindow);
        }
        else
            Log!.Error($"Can not find 'outputwindow.exe' in directory {PluginInterface!.AssemblyLocation.DirectoryName!}");
    }

    private void Disable()
    {
        outputWindowData->doClose = true;
        outputRenamed = false;
        outputProcess = null;
    }

    // Relabels the output window to OutputWindowTitle. The native window is a separate
    // process whose title is baked in, so we find its top-level window by process id and
    // call SetWindowTextW. Runs from Update() until it succeeds once per Enable().
    private void TryRenameOutputWindow()
    {
        if (outputRenamed || outputProcess == null)
            return;

        try
        {
            if (outputProcess.HasExited)
            {
                outputProcess = null;
                return;
            }
        }
        catch (Exception) { return; }

        uint pid = (uint)outputProcess.Id;
        IntPtr found = IntPtr.Zero;
        EnumWindows((hWnd, lParam) =>
        {
            GetWindowThreadProcessId(hWnd, out uint wpid);
            if (wpid == pid && IsWindowVisible(hWnd))
            {
                found = hWnd;
                return false; // stop enumeration
            }
            return true;
        }, IntPtr.Zero);

        if (found != IntPtr.Zero)
        {
            SetWindowTextW(found, OutputWindowTitle);
            outputRenamed = true;
        }
    }


    private bool CreateShaders(Device11 dxDevice11)
    {
        psTexture.CompileShaderFromString(dxDevice11);
        vsTexture.CompileShaderFromString(dxDevice11);
        return true;
    }

    private void DestroyShaders()
    {
        psTexture.Release();
        vsTexture.Release();
    }

    private bool CreateBuffers(Device11 dxDevice11, DeviceContext11 dxDevCon11)
    {
        orthogSquare = new RenderSquare(dxDevice11, dxDevCon11);
        orthogSquare.SetShadersLayout(vsTexture.Layout!, vsTexture.VS!, psTexture.PS!);
        return true;
    }

    private void DestroyBuffers()
    {
        orthogSquare?.Release();
    }

    private bool CreateTextures(Device11 dxDevice11, DeviceContext11 dxDevCon11)
    {
        //Texture2D BackBuffer = swapChain11.GetBackBuffer<Texture2D>(0);
        //SharpDX.Direct3D11.Resource tmpResourceBB = BackBuffer.QueryInterface<SharpDX.Direct3D11.Resource>();
        //RenderTargetView backbufferRTV = new RenderTargetView(dxDevice11, tmpResourceBB);
        //tmpResourceBB.Dispose();

        pSamplerState = new SamplerState(dxDevice11, new SamplerStateDescription()
        {
            Filter = Filter.MinMagMipLinear,
            AddressU = TextureAddressMode.Clamp,
            AddressV = TextureAddressMode.Clamp,
            AddressW = TextureAddressMode.Clamp,
            MipLodBias = 0.0f,
            MaximumAnisotropy = 1,
            ComparisonFunction = Comparison.Always,
            BorderColor = new RawColor4(0, 0, 0, 0),
            MinimumLod = float.MinValue,
            MaximumLod = float.MaxValue,
        });

        if (!CreateTexturesShared(dxDevice11, dxDevCon11))
            return false;

        return true;
    }

    private void DestroyTextures()
    {
        pSamplerState?.Dispose();
        selectedSRV?.Dispose();
    }

    private bool CreateTexturesShared(Device11 dxDevice11, DeviceContext11 dxDevCon11)
    {
        if (outputWindowData->isOutputActive > 0 && outputWindowData->sharedHandle > 0)
            sharedTexture = dxDevice11.OpenSharedResource<Texture2D>(outputWindowData->sharedHandle);

        if (sharedTexture != null)
        {
            //Resource sharedResource = sharedTexture.QueryInterface<Resource>();
            //Log!.Info($"dxDevice: {dxDevice11.NativePointer:x} | cont: {dxDevCon11.NativePointer:x} | SharedHandle: {sharedResource.SharedHandle:x} | SharedMutex: {sharedMutex}");
            //outputWindowData->sharedHandle = sharedResource.SharedHandle;
            //sharedResource.Dispose();

            SharpDX.Direct3D11.Resource tmpResource = sharedTexture.QueryInterface<SharpDX.Direct3D11.Resource>();
            sharedRTV = new RenderTargetView(dxDevice11, tmpResource);
            sharedSRV = new ShaderResourceView(dxDevice11, tmpResource);
            tmpResource.Dispose();
        }

        return true;
    }

    private unsafe void DestroyTexturesShared()
    {
        sharedRTV?.Dispose();
        sharedSRV?.Dispose();
        sharedTexture?.Dispose();
        sharedTexture = null;
        selectedSRV?.Dispose();
        selectedSRV = null;
        lastCapturedTex = null;
    }


    // Logs every populated, safely-readable render-target slot to /xllog. Use this after a game
    // patch to find which flat index holds the composited game frame: look for entries whose
    // dimensions match the swapchain (flagged CANDIDATE). Set that value via the "Override index"
    // control. This walks the table through SafeGetTexture and reads dimensions/format straight
    // from the FFXIVClientStructs Texture struct (no COM calls), so it cannot crash the game.
    public unsafe void DumpRenderTargets()
    {
        RenderTargetManager* rtm = RenderTargetManager.Instance();
        if (rtm == null)
        {
            Log!.Warning("MaskedCarnivale: RenderTargetManager instance is null; cannot dump.");
            return;
        }

        FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device* ffxivDevice = FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device.Instance();
        int scWidth = (int)ffxivDevice->SwapChain->Width;
        int scHeight = (int)ffxivDevice->SwapChain->Height;

        int maxIndex = (sizeof(RenderTargetManager) - 0x20) / 0x8;
        Log!.Info($"MaskedCarnivale: ==== render target dump (swapchain {scWidth}x{scHeight}, scanning 0..{maxIndex}) ====");

        // Named anchor: SwapChainBackBuffer (public field). Its flat index is a reliable
        // reference point across patches.
        Texture* backBuffer = rtm->SwapChainBackBuffer;
        if (IsReadable((IntPtr)(void*)backBuffer))
        {
            int bbIndex = (0x570 - 0x20) / 0x8; // = 170 on the current layout
            Log!.Info($"MaskedCarnivale: SwapChainBackBuffer @ flat idx {bbIndex} | {backBuffer->ActualWidth}x{backBuffer->ActualHeight} | Format {backBuffer->TextureFormat} | hasSRV {(backBuffer->D3D11ShaderResourceView != null)}");
        }

        int found = 0;
        for (int i = 0; i <= maxIndex; i++)
        {
            Texture* rendText = SafeGetTexture(i);
            if (rendText == null || rendText->D3D11Texture2D == null)
                continue;

            uint w = rendText->ActualWidth;
            uint h = rendText->ActualHeight;
            bool hasSrv = rendText->D3D11ShaderResourceView != null;
            bool full = w == (uint)scWidth && h == (uint)scHeight;
            string tag = (full && hasSrv) ? "  <== CANDIDATE (matches swapchain + has SRV)" : "";
            Log!.Info($"MaskedCarnivale: idx {i,3} | {w}x{h} | Format {rendText->TextureFormat} | hasSRV {hasSrv}{tag}");
            found++;
        }
        Log!.Info($"MaskedCarnivale: ==== dump complete, {found} populated slots ====");
    }

    // Jumps renderIndex to the next/previous full-resolution render target that has a usable
    // shader-resource view (dir = +1 forward, -1 back), wrapping around. Lets the user click
    // through only the plausible full-screen candidates instead of typing indices one by one.
    // Turns on manual override so the pick sticks.
    public unsafe void StepCandidate(int dir)
    {
        RenderTargetManager* rtm = RenderTargetManager.Instance();
        if (rtm == null)
            return;

        FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device* dev = FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device.Instance();
        uint sw = dev->SwapChain->Width;
        uint sh = dev->SwapChain->Height;

        int max = (sizeof(RenderTargetManager) - 0x20) / 0x8;
        int span = max + 1;
        for (int step = 1; step <= max; step++)
        {
            int i = cfg.renderIndex + dir * step;
            i = ((i % span) + span) % span; // wrap into [0, max]

            Texture* t = SafeGetTexture(i);
            if (t == null || t->D3D11Texture2D == null || t->D3D11ShaderResourceView == null)
                continue;
            if (t->ActualWidth != sw || t->ActualHeight != sh)
                continue;

            cfg.manualIndex = true;
            cfg.renderIndex = i;
            cfg.Save();
            Log!.Info($"MaskedCarnivale: candidate -> idx {i} | {t->ActualWidth}x{t->ActualHeight} | {t->TextureFormat}");
            return;
        }
    }

    // Human-readable description of the currently-selected render target, for the config window.
    public unsafe string GetCurrentIndexInfo()
    {
        Texture* t = SafeGetTexture(cfg.renderIndex);
        if (t == null || t->D3D11Texture2D == null)
            return $"idx {cfg.renderIndex}: (empty / not readable)";
        return $"idx {cfg.renderIndex} | {t->ActualWidth}x{t->ActualHeight} | {t->TextureFormat} | SRV {(t->D3D11ShaderResourceView != null)}";
    }

    //----
    // DXGIPresent
    //----
    private delegate void DXGIPresentDg(UInt64 a, UInt64 b);
    [Signature(Signatures.DXGIPresent, DetourName = nameof(DXGIPresentFn))]
    private Hook<DXGIPresentDg>? DXGIPresentHook = null;

    [HandleStatus("DXGIPresent")]
    public void DXGIPresentStatus(bool status, bool dispose)
    {
        if (dispose)
            DXGIPresentHook?.Dispose();
        else
        {
            if (status)
                DXGIPresentHook?.Enable();
            else
                DXGIPresentHook?.Disable();
        }
    }

    //----
    // IDXGISwapChain::Present (the real end-of-frame present). Hooked separately so backbuffer
    // mode can copy the finished frame (which includes the HUD). Because we install this hook
    // after Dalamud loads, our detour runs before Dalamud's own present detour draws its ImGui
    // overlays -> copying here yields HUD without WrathCombo/Splatoon (if that ordering holds).
    //----
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DXGISwapChainPresentDg(IntPtr pSwapChain, uint syncInterval, uint flags);
    private Hook<DXGISwapChainPresentDg>? presentHook = null;
    private bool loggedBackbufferCopyFail = false;
    private bool loggedBackbufferCopyOk = false;

    // "Show UI" is the master toggle: ON = capture the final backbuffer (game + HUD),
    // OFF = mirror the clean 3D scene render target (no HUD). "Override index" (manualIndex)
    // is an advanced escape hatch that forces a specific render-target index and wins over both.
    private bool UseBackbuffer => cfg.showUI && !cfg.manualIndex;

    private int PresentDetour(IntPtr pSwapChain, uint syncInterval, uint flags)
    {
        if (UseBackbuffer
            && outputWindowData != null
            && outputWindowData->isOutputActive > 0
            && sharedTexture != null)
        {
            try
            {
                FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device* ffxivDevice = FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device.Instance();
                DeviceContext11 dxDevCon11 = (DeviceContext11)(IntPtr)ffxivDevice->D3D11DeviceContext;
                SwapChain11 swapChain11 = (SwapChain11)(IntPtr)ffxivDevice->SwapChain->DXGISwapChain;

                // Copy the just-finished backbuffer (game + HUD) into the shared texture. Backbuffer
                // and shared texture are both full-screen 32bpp BGRA-family, so CopyResource is legal.
                using Texture2D backBuffer = swapChain11.GetBackBuffer<Texture2D>(0);
                dxDevCon11.CopyResource(backBuffer, sharedTexture);

                if (!loggedBackbufferCopyOk)
                {
                    loggedBackbufferCopyOk = true;
                    Log!.Info("MaskedCarnivale: backbuffer copy active (HUD mode).");
                }
            }
            catch (Exception e)
            {
                if (!loggedBackbufferCopyFail)
                {
                    loggedBackbufferCopyFail = true;
                    Log!.Warning($"MaskedCarnivale: backbuffer copy failed ({e.Message}). Formats/sizes may be incompatible.");
                }
            }
        }

        return presentHook!.Original(pSwapChain, syncInterval, flags);
    }

    private unsafe void DXGIPresentFn(UInt64 a, UInt64 b)
    {
        if (!loggedDetourActive)
        {
            loggedDetourActive = true;
            Log!.Info("MaskedCarnivale: DXGIPresent detour is active (hook firing).");
        }

        FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device* ffxivDevice = FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device.Instance();
        Device11 dxDevice11 = (Device11)(IntPtr)ffxivDevice->D3D11Forwarder;
        DeviceContext11 dxDevCon11 = (DeviceContext11)(IntPtr)ffxivDevice->D3D11DeviceContext;
        SwapChain11 swapChain11 = (SwapChain11)(IntPtr)ffxivDevice->SwapChain->DXGISwapChain;

        //----
        // If the window is open and we havent connected to the shared texture yet, connect to it
        //----
        if (outputWindowData->isOutputActive > 0 && outputWindowData->sharedHandle > 0 && sharedTexture == null)
        {
            outputWindowData->isGameActive = (byte)shareMemType;
            outputWindowData->newTop = cfg.yPosition;
            outputWindowData->newLeft = cfg.xPosition;
            outputWindowData->newWidth = (int)ffxivDevice->SwapChain->Width;
            outputWindowData->newHeight = (int)ffxivDevice->SwapChain->Height;
            outputWindowData->newTopmost = (byte)cfg.orderStatus;
            outputWindowData->updateWindow = true;

            CreateTexturesShared(dxDevice11, dxDevCon11);
        }
        //----
        // If the window is open and we have connected to the shared texture, render to it
        //----
        else if (outputWindowData->isOutputActive > 0 && outputWindowData->sharedHandle > 0 && sharedTexture != null)
        {
            if(outputWindowData->resetSharedHandle)
            {
                outputWindowData->resetSharedHandle = false;
                DestroyTexturesShared();
                CreateTexturesShared(dxDevice11, dxDevCon11);
            }

            // In backbuffer/HUD mode the swapchain-present hook (PresentDetour) is the sole
            // writer of the shared texture, so skip the render-target-index shader path here to
            // avoid fighting over the shared texture each frame.
            if (!UseBackbuffer)
            {
                // Show UI OFF (and not manually overridden) => mirror the clean scene buffer.
                if (!cfg.manualIndex)
                    cfg.renderIndex = gameWindowWithoutUI;

                cfg.renderIndex = Math.Min(Math.Max(cfg.renderIndex, 0), 511);

                // Validated lookup: never dereferences an out-of-struct / unreadable pointer, so a
                // bad manual index yields a black frame instead of crashing the game.
                Texture* rendText = SafeGetTexture(cfg.renderIndex);
                void* curTexPtr = rendText != null ? rendText->D3D11Texture2D : null;

                // Re-acquire the shader-resource view whenever the underlying D3D texture changes
                // (the user picked a new index, OR the game recycled the render target at this
                // index). Caching only on index-change left a stale view pointing at a freed
                // texture -> the mirror went black after working for a moment. Tracking the actual
                // texture pointer fixes that while still avoiding a rebuild every single frame.
                if (curTexPtr != lastCapturedTex)
                {
                    lastCapturedTex = curTexPtr;
                    selectedSRV?.Dispose();
                    selectedSRV = null;

                    if (rendText != null && curTexPtr != null)
                    {
                        try
                        {
                            Texture2DDescription rt0 = ((Texture2D)(IntPtr)curTexPtr).Description;

                            if ((rt0.BindFlags & BindFlags.ShaderResource) == BindFlags.ShaderResource)
                            {
                                SharpDX.Direct3D11.Resource tmpResource = ((Texture2D)(IntPtr)curTexPtr).QueryInterface<SharpDX.Direct3D11.Resource>();
                                selectedSRV = new ShaderResourceView(dxDevice11, tmpResource);
                                tmpResource.Dispose();
                            }
                        }
                        catch (Exception)
                        {
                            selectedSRV = null;
                        }
                    }
                }

                RawColor4 color = new RawColor4(0, 0, 0, 0);

                dxDevCon11.ClearRenderTargetView(sharedRTV, color);
                dxDevCon11.OutputMerger.SetRenderTargets(sharedRTV);
                dxDevCon11.Rasterizer.SetViewport(0f, 0f, ffxivDevice->SwapChain->Width, ffxivDevice->SwapChain->Height, 0f, 1f);
                dxDevCon11.PixelShader.SetSampler(0, pSamplerState);
                if (selectedSRV != null)
                    dxDevCon11.PixelShader.SetShaderResource(0, selectedSRV);
                dxDevCon11.InputAssembler.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.TriangleList;
                if (orthogSquare != null)
                    orthogSquare.Render();
            }
        }
        //----
        // If the window is not open and we have connected to the shared texture, disconnect from it
        //----
        else if (outputWindowData->isOutputActive == 0 && outputWindowData->sharedHandle == 0 && sharedTexture != null)
        {
            DestroyTexturesShared();
        }

        DXGIPresentHook!.Original(a, b);
    }
}
