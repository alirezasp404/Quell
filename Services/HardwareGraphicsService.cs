using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Skia;
using Avalonia.Win32;
using Microsoft.Win32;

namespace Quell.Services;

/// <summary>
/// Service managing graphics hardware adapter detection, GPU vs CPU rendering mode resolution,
/// Skia memory buffer optimization, and SQLite configuration persistence.
/// </summary>
public class HardwareGraphicsService
{
    private static HardwareGraphicsService? _instance;
    public static HardwareGraphicsService Instance => _instance ??= new HardwareGraphicsService();

    public string GpuAdapterName { get; private set; } = "Detecting Graphics Adapter...";
    public bool IsGpuDetected { get; private set; }
    public string RenderingMode { get; set; } = "Auto"; // "Auto", "GPU", "CPU"
    public string EffectiveRenderingMode { get; private set; } = "GPU";
    public string ActiveRendererSummary { get; private set; } = "Skia Hardware (DirectX 11 / ANGLE)";
    public int VramCacheMb { get; private set; } = 128;

    private bool _hasDetectedHardware;

    public void DetectHardwareGpu()
    {
        if (_hasDetectedHardware) return;
        _hasDetectedHardware = true;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                DetectWindowsGpu();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                IsGpuDetected = true;
                GpuAdapterName = "Apple Metal Graphics Processor";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                bool hasDrm = Directory.Exists("/sys/class/drm") && Directory.GetDirectories("/sys/class/drm").Length > 0;
                IsGpuDetected = hasDrm;
                GpuAdapterName = hasDrm ? "Linux DRM/KMS Hardware GPU" : "Generic Display Controller";
            }
            else
            {
                IsGpuDetected = false;
                GpuAdapterName = "Standard Display Controller";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HardwareGraphicsService] DetectHardwareGpu error: {ex.Message}");
            IsGpuDetected = true; // Safe fallback: assume GPU available
            GpuAdapterName = "Standard Hardware Graphics Device";
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private void DetectWindowsGpu()
    {
        try
        {
            // Query Windows Display Adapter Class Registry Keys
            const string videoClassKey = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
            using var baseKey = Registry.LocalMachine.OpenSubKey(videoClassKey);
            if (baseKey != null)
            {
                string[] subKeys = baseKey.GetSubKeyNames();
                foreach (string sub in subKeys)
                {
                    if (sub.Length == 4 && int.TryParse(sub, out _))
                    {
                        using var adapterKey = baseKey.OpenSubKey(sub);
                        if (adapterKey != null)
                        {
                            var desc = adapterKey.GetValue("DriverDesc") as string;
                            if (!string.IsNullOrWhiteSpace(desc))
                            {
                                // Filter out Microsoft Basic Display Adapter (which is software-emulated in VMs/RDP)
                                if (!desc.Contains("Microsoft Basic Display", StringComparison.OrdinalIgnoreCase) &&
                                    !desc.Contains("Standard VGA", StringComparison.OrdinalIgnoreCase) &&
                                    !desc.Contains("VSR", StringComparison.OrdinalIgnoreCase))
                                {
                                    IsGpuDetected = true;
                                    GpuAdapterName = desc.Trim();
                                    return;
                                }
                                else
                                {
                                    GpuAdapterName = desc.Trim();
                                }
                            }
                        }
                    }
                }
            }

            // If no discrete or integrated GPU was verified
            if (string.IsNullOrWhiteSpace(GpuAdapterName) || GpuAdapterName == "Detecting Graphics Adapter...")
            {
                GpuAdapterName = "Microsoft Basic Display Adapter (Software)";
                IsGpuDetected = false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HardwareGraphicsService] Windows GPU query error: {ex.Message}");
            IsGpuDetected = true;
            GpuAdapterName = "Standard Direct3D / OpenGL Graphics";
        }
    }

    public string ResolveEffectiveMode(string mode)
    {
        DetectHardwareGpu();
        if (string.Equals(mode, "GPU", StringComparison.OrdinalIgnoreCase)) return "GPU";
        if (string.Equals(mode, "CPU", StringComparison.OrdinalIgnoreCase)) return "CPU";
        // Auto mode: GPU if detected, otherwise CPU
        return IsGpuDetected ? "GPU" : "CPU";
    }

    /// <summary>
    /// Reads initial rendering configuration from SQLite before Avalonia UI initializes.
    /// </summary>
    public string GetInitialRenderingMode()
    {
        DetectHardwareGpu();
        string mode = "Auto";
        try
        {
            string dbPath = DatabaseStorageService.Instance.DatabasePath;
            if (File.Exists(dbPath))
            {
                using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}");
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Value FROM AppSettings WHERE Key = 'GraphicsRenderingMode';";
                var res = cmd.ExecuteScalar();
                if (res != null && res != DBNull.Value)
                {
                    string? str = res.ToString();
                    if (!string.IsNullOrWhiteSpace(str)) mode = str;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HardwareGraphicsService] GetInitialRenderingMode SQLite fallback: {ex.Message}");
        }

        RenderingMode = mode;
        EffectiveRenderingMode = ResolveEffectiveMode(mode);
        return EffectiveRenderingMode;
    }

    /// <summary>
    /// Applies DirectX 11 / ANGLE / Vulkan or CPU Software platform options and Skia memory buffers to AppBuilder.
    /// </summary>
    public void ConfigureAppBuilder(AppBuilder builder)
    {
        string effective = GetInitialRenderingMode();

        if (string.Equals(effective, "GPU", StringComparison.OrdinalIgnoreCase))
        {
            VramCacheMb = 128;
            ActiveRendererSummary = "Skia Hardware Pipeline (DirectX 11 / ANGLE)";
            if (OperatingSystem.IsWindows())
            {
                builder.With(new Win32PlatformOptions
                {
                    RenderingMode = new[]
                    {
                        Win32RenderingMode.AngleEgl, // DirectX 11 via ANGLE
                        Win32RenderingMode.Wgl,      // Native OpenGL fallback
                        Win32RenderingMode.Software  // Safe CPU fallback
                    },
                    CompositionMode = new[]
                    {
                        Win32CompositionMode.LowLatencyDxgiSwapChain,
                        Win32CompositionMode.RedirectionSurface
                    }
                });
            }

            builder.With(new SkiaOptions
            {
                MaxGpuResourceSizeBytes = 128 * 1024 * 1024 // 128 MB VRAM cache for buttery smooth rendering
            });
        }
        else
        {
            VramCacheMb = 16;
            ActiveRendererSummary = "CPU Software Rasterizer (Lightweight)";
            if (OperatingSystem.IsWindows())
            {
                builder.With(new Win32PlatformOptions
                {
                    RenderingMode = new[]
                    {
                        Win32RenderingMode.Software // Force lightweight CPU rasterization
                    }
                });
            }

            builder.With(new SkiaOptions
            {
                MaxGpuResourceSizeBytes = 16 * 1024 * 1024 // 16 MB minimal memory buffer
            });
        }
    }

    /// <summary>
    /// Pre-caches graphics settings and hardware device detection during Splash Screen sequence.
    /// </summary>
    public async System.Threading.Tasks.Task InitializeAsync()
    {
        DetectHardwareGpu();
        string mode = await DatabaseStorageService.Instance.GetSettingAsync("GraphicsRenderingMode", "Auto").ConfigureAwait(false);
        RenderingMode = mode;
        EffectiveRenderingMode = ResolveEffectiveMode(mode);
        ActiveRendererSummary = string.Equals(EffectiveRenderingMode, "GPU", StringComparison.OrdinalIgnoreCase)
            ? "Skia Hardware Pipeline (DirectX 11 / ANGLE)"
            : "CPU Software Rasterizer (Lightweight)";
        VramCacheMb = string.Equals(EffectiveRenderingMode, "GPU", StringComparison.OrdinalIgnoreCase) ? 128 : 16;
    }
}
