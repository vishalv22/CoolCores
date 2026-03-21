using Microsoft.Win32;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using Windows.Devices.Power;
using Windows.Management.Deployment;

namespace CoolCores.Pages
{
    public sealed partial class SystemPage : Page
    {
        private readonly DispatcherTimer _metricsTimer = new() { Interval = TimeSpan.FromSeconds(1) };
        private ulong _previousIdleTime;
        private ulong _previousKernelTime;
        private ulong _previousUserTime;
        private bool _hasPreviousCpuSample;
        private int _metricsTickCount;
        private readonly List<PerformanceCounter> _gpuUsageCounters = new();

        public SystemPage()
        {
            InitializeComponent();
            ProcessorNameTextBlock.Text = GetProcessorName();
            InitializeLiveMetrics();
        }

        private void InitializeLiveMetrics()
        {
            InitializeGpuUsageCounters();
            UpdateBatteryStats();
            UpdateGraphicsStats();
            UpdateServiceStats();
            UpdateAppStats();
            UpdateLiveMetrics();
            _metricsTimer.Tick += MetricsTimer_Tick;
            _metricsTimer.Start();
            Unloaded += SystemPage_Unloaded;
        }

        private void MetricsTimer_Tick(object? sender, object e)
        {
            UpdateLiveMetrics();
        }

        private void SystemPage_Unloaded(object sender, RoutedEventArgs e)
        {
            DisposeGpuUsageCounters();
            _metricsTimer.Stop();
            _metricsTimer.Tick -= MetricsTimer_Tick;
            Unloaded -= SystemPage_Unloaded;
        }

        private void UpdateLiveMetrics()
        {
            _metricsTickCount++;

            string speed = GetCurrentSpeedText();
            string usage = GetCpuUsageText();
            UsageStatsTextBlock.Text = $"{usage} ({speed})";

            if (TryGetMemoryStatus(out MemoryStatusEx memoryStatus))
            {
                double totalMemoryGb = BytesToGb(memoryStatus.TotalPhys);
                double usedMemoryGb = BytesToGb(memoryStatus.TotalPhys - memoryStatus.AvailPhys);

                MemorySummaryTextBlock.Text = $"{totalMemoryGb:F1} GB";
                MemoryUsageStatsTextBlock.Text = $"{memoryStatus.MemoryLoad}% ({usedMemoryGb:F1}/{totalMemoryGb:F1}GB)";
            }
            else
            {
                MemorySummaryTextBlock.Text = "Memory details unavailable";
                MemoryUsageStatsTextBlock.Text = "--% (--/--GB)";
            }

            if (TryGetStorageStatus(out double totalStorageGb, out double usedStorageGb, out double storageUsage))
            {
                StorageSummaryTextBlock.Text = $"{totalStorageGb:F1} GB";
                StorageUsageStatsTextBlock.Text = $"{Math.Round(storageUsage):F0}% ({usedStorageGb:F1}/{totalStorageGb:F1}GB)";
            }
            else
            {
                StorageSummaryTextBlock.Text = "Storage details unavailable";
                StorageUsageStatsTextBlock.Text = "--% (--/--GB)";
            }

            UpdateGraphicsStats();

            if (_metricsTickCount % 5 == 0)
            {
                UpdateBatteryStats();
                UpdateServiceStats();
            }

            if (_metricsTickCount % 30 == 0)
            {
                UpdateAppStats();
            }
        }

        private void UpdateGraphicsStats()
        {
            if (TryGetGraphicsAdapterStats(out string primaryAdapterName, out _))
            {
                GraphicsSummaryTextBlock.Text = primaryAdapterName;
                GraphicsStatsTextBlock.Text = GetGpuUsageText();
            }
            else
            {
                GraphicsSummaryTextBlock.Text = "Graphics details unavailable";
                GraphicsStatsTextBlock.Text = "--";
            }
        }

        private void InitializeGpuUsageCounters()
        {
            DisposeGpuUsageCounters();

            try
            {
                PerformanceCounterCategory category = new("GPU Engine");

                foreach (string instanceName in category.GetInstanceNames())
                {
                    if (!instanceName.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        PerformanceCounter counter = new(
                            categoryName: "GPU Engine",
                            counterName: "Utilization Percentage",
                            instanceName: instanceName,
                            readOnly: true);

                        _ = counter.NextValue();
                        _gpuUsageCounters.Add(counter);
                    }
                    catch
                    {
                        // Ignore invalid counters and keep any valid ones.
                    }
                }
            }
            catch
            {
                // GPU counters are unavailable on some systems/drivers.
            }
        }

        private string GetGpuUsageText()
        {
            if (_gpuUsageCounters.Count == 0)
            {
                return "--";
            }

            float highestUsage = 0f;
            bool hasSample = false;

            foreach (PerformanceCounter counter in _gpuUsageCounters)
            {
                try
                {
                    float sample = counter.NextValue();
                    if (float.IsNaN(sample) || float.IsInfinity(sample))
                    {
                        continue;
                    }

                    sample = Math.Clamp(sample, 0f, 100f);
                    highestUsage = Math.Max(highestUsage, sample);
                    hasSample = true;
                }
                catch
                {
                    // Counter can disappear after driver changes or sleep/wake.
                }
            }

            return hasSample ? $"{Math.Round(highestUsage):F0}%" : "--";
        }

        private void DisposeGpuUsageCounters()
        {
            foreach (PerformanceCounter counter in _gpuUsageCounters)
            {
                counter.Dispose();
            }

            _gpuUsageCounters.Clear();
        }

        private static bool TryGetGraphicsAdapterStats(out string primaryAdapterName, out int adapterCount)
        {
            primaryAdapterName = string.Empty;
            adapterCount = 0;

            uint deviceIndex = 0;
            while (true)
            {
                DisplayDevice displayDevice = DisplayDevice.Create();
                if (!EnumDisplayDevices(null, deviceIndex, ref displayDevice, 0))
                {
                    break;
                }

                bool isMirroringDriver = (displayDevice.StateFlags & DisplayDeviceStateFlags.MirroringDriver) != 0;
                if (!isMirroringDriver && !string.IsNullOrWhiteSpace(displayDevice.DeviceString))
                {
                    adapterCount++;

                    bool isPrimary = (displayDevice.StateFlags & DisplayDeviceStateFlags.PrimaryDevice) != 0;
                    if (isPrimary)
                    {
                        primaryAdapterName = displayDevice.DeviceString.Trim();
                    }
                    else if (string.IsNullOrWhiteSpace(primaryAdapterName))
                    {
                        primaryAdapterName = displayDevice.DeviceString.Trim();
                    }
                }

                deviceIndex++;
            }

            return adapterCount > 0 && !string.IsNullOrWhiteSpace(primaryAdapterName);
        }

        private void UpdateServiceStats()
        {
            try
            {
                ServiceController[] services = ServiceController.GetServices();
                int totalCount = services.Length;
                int runningCount = services.Count(service => service.Status == ServiceControllerStatus.Running);

                foreach (ServiceController service in services)
                {
                    service.Dispose();
                }

                ServicesSummaryTextBlock.Text = $"{runningCount} Running";
                ServicesStatsTextBlock.Text = $"{runningCount}/{totalCount} Active";
            }
            catch
            {
                ServicesSummaryTextBlock.Text = "Service stats unavailable";
                ServicesStatsTextBlock.Text = "--";
            }
        }

        private void UpdateAppStats()
        {
            int desktopApps = GetDesktopAppCount();
            int storeApps = GetStoreAppCount();
            int totalApps = desktopApps + storeApps;

            if (totalApps <= 0)
            {
                AppsSummaryTextBlock.Text = "App stats unavailable";
                AppsStatsTextBlock.Text = "--";
                return;
            }

            AppsSummaryTextBlock.Text = $"{totalApps} Installed Apps";
            AppsStatsTextBlock.Text = $"Desktop {desktopApps} | Store {storeApps}";
        }

        private void UpdateBatteryStats()
        {
            if (!TryGetBatteryDetails(
                out double? designCapacityWh,
                out double? fullChargeCapacityWh,
                out double? remainingCapacityWh,
                out double? batteryTemperatureCelsius))
            {
                BatterySummaryTextBlock.Text = "Battery details unavailable";
                BatteryStatsTextBlock.Text = "--";
                return;
            }

            string originalCapacity = FormatCapacityWh(designCapacityWh);
            string currentCapacity = FormatCapacityWh(fullChargeCapacityWh);
            string temperature = batteryTemperatureCelsius.HasValue
                ? $"{batteryTemperatureCelsius.Value:F1}°C"
                : "--";

            BatterySummaryTextBlock.Text = $"Original: {originalCapacity}  Current: {currentCapacity}\nTemp: {temperature}";

            if (remainingCapacityWh.HasValue && fullChargeCapacityWh.HasValue && fullChargeCapacityWh.Value > 0)
            {
                double chargePercentage = Math.Clamp(
                    remainingCapacityWh.Value * 100.0 / fullChargeCapacityWh.Value,
                    0.0,
                    100.0);

                BatteryStatsTextBlock.Text = $"{Math.Round(chargePercentage):F0}% ({remainingCapacityWh.Value:F1}/{fullChargeCapacityWh.Value:F1}Wh)";
                return;
            }

            BatteryStatsTextBlock.Text = "--";
        }

        private static bool TryGetBatteryDetails(
            out double? designCapacityWh,
            out double? fullChargeCapacityWh,
            out double? remainingCapacityWh,
            out double? batteryTemperatureCelsius)
        {
            designCapacityWh = null;
            fullChargeCapacityWh = null;
            remainingCapacityWh = null;
            batteryTemperatureCelsius = null;

            try
            {
                BatteryReport? report = Battery.AggregateBattery.GetReport();
                if (report != null)
                {
                    designCapacityWh = MilliwattHoursToWattHours(report.DesignCapacityInMilliwattHours);
                    fullChargeCapacityWh = MilliwattHoursToWattHours(report.FullChargeCapacityInMilliwattHours);
                    remainingCapacityWh = MilliwattHoursToWattHours(report.RemainingCapacityInMilliwattHours);
                }
            }
            catch
            {
                // Battery report is unavailable on some desktops/VMs.
            }

            batteryTemperatureCelsius = TryGetBatteryTemperatureCelsius();

            return designCapacityWh.HasValue ||
                   fullChargeCapacityWh.HasValue ||
                   remainingCapacityWh.HasValue ||
                   batteryTemperatureCelsius.HasValue;
        }

        private static double? TryGetBatteryTemperatureCelsius()
        {
            try
            {
                using ManagementObjectSearcher searcher = new(
                    scope: @"root\wmi",
                    queryString: "SELECT Temperature FROM BatteryTemperature");

                using ManagementObjectCollection results = searcher.Get();
                foreach (ManagementObject result in results)
                {
                    object? value = result["Temperature"];
                    if (value == null)
                    {
                        continue;
                    }

                    double temperatureTenthsKelvin = Convert.ToDouble(value);
                    if (temperatureTenthsKelvin <= 0)
                    {
                        continue;
                    }

                    return (temperatureTenthsKelvin / 10.0) - 273.15;
                }
            }
            catch
            {
                // Temperature sensor is often unavailable for many systems.
            }

            return null;
        }

        private static double? MilliwattHoursToWattHours(int? capacityInMilliwattHours)
        {
            if (!capacityInMilliwattHours.HasValue || capacityInMilliwattHours.Value <= 0)
            {
                return null;
            }

            return capacityInMilliwattHours.Value / 1000.0;
        }

        private static string FormatCapacityWh(double? capacityWh)
        {
            return capacityWh.HasValue ? $"{capacityWh.Value:F1}Wh" : "--";
        }

        private static int GetDesktopAppCount()
        {
            HashSet<string> appNames = new(StringComparer.OrdinalIgnoreCase);

            AddUninstallApps(
                RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                    .OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                appNames);
            AddUninstallApps(
                RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32)
                    .OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                appNames);
            AddUninstallApps(
                RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64)
                    .OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                appNames);
            AddUninstallApps(
                RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32)
                    .OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                appNames);

            return appNames.Count;
        }

        private static void AddUninstallApps(RegistryKey? uninstallRoot, HashSet<string> appNames)
        {
            if (uninstallRoot == null)
            {
                return;
            }

            using (uninstallRoot)
            {
                foreach (string subKeyName in uninstallRoot.GetSubKeyNames())
                {
                    using RegistryKey? appKey = uninstallRoot.OpenSubKey(subKeyName);
                    if (appKey == null)
                    {
                        continue;
                    }

                    string? displayName = appKey.GetValue("DisplayName") as string;
                    if (string.IsNullOrWhiteSpace(displayName))
                    {
                        continue;
                    }

                    object? systemComponentValue = appKey.GetValue("SystemComponent");
                    if (systemComponentValue is int systemComponent && systemComponent == 1)
                    {
                        continue;
                    }

                    appNames.Add(displayName.Trim());
                }
            }
        }

        private static int GetStoreAppCount()
        {
            try
            {
                PackageManager packageManager = new();
                return packageManager
                    .FindPackagesForUser(string.Empty)
                    .Count(package => !package.IsFramework && !package.IsResourcePackage);
            }
            catch
            {
                return 0;
            }
        }

        private static string GetCurrentSpeedText()
        {
            if (!TryGetAverageCurrentMhz(out int currentMhz) || currentMhz <= 0)
            {
                return "--GHz";
            }

            return $"{currentMhz / 1000.0:F2}GHz";
        }

        private string GetCpuUsageText()
        {
            if (!GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user))
            {
                return "--%";
            }

            ulong currentIdle = ToUInt64(idle);
            ulong currentKernel = ToUInt64(kernel);
            ulong currentUser = ToUInt64(user);

            if (!_hasPreviousCpuSample)
            {
                _previousIdleTime = currentIdle;
                _previousKernelTime = currentKernel;
                _previousUserTime = currentUser;
                _hasPreviousCpuSample = true;
                return "0%";
            }

            ulong idleDelta = currentIdle - _previousIdleTime;
            ulong kernelDelta = currentKernel - _previousKernelTime;
            ulong userDelta = currentUser - _previousUserTime;
            ulong totalDelta = kernelDelta + userDelta;

            _previousIdleTime = currentIdle;
            _previousKernelTime = currentKernel;
            _previousUserTime = currentUser;

            if (totalDelta == 0)
            {
                return "0%";
            }

            double usage = (totalDelta - idleDelta) * 100.0 / totalDelta;
            usage = Math.Clamp(usage, 0.0, 100.0);
            return $"{Math.Round(usage):F0}%";
        }

        private static bool TryGetAverageCurrentMhz(out int averageMhz)
        {
            int processorCount = Environment.ProcessorCount;
            int structSize = Marshal.SizeOf<ProcessorPowerInformation>();
            int bufferSize = structSize * processorCount;
            IntPtr buffer = Marshal.AllocHGlobal(bufferSize);

            try
            {
                uint status = CallNtPowerInformation(
                    PowerInformationLevel.ProcessorInformation,
                    IntPtr.Zero,
                    0,
                    buffer,
                    (uint)bufferSize);

                if (status != 0)
                {
                    averageMhz = 0;
                    return false;
                }

                long totalMhz = 0;
                int samples = 0;

                for (int i = 0; i < processorCount; i++)
                {
                    IntPtr itemPtr = IntPtr.Add(buffer, i * structSize);
                    ProcessorPowerInformation info = Marshal.PtrToStructure<ProcessorPowerInformation>(itemPtr);

                    if (info.CurrentMhz == 0)
                    {
                        continue;
                    }

                    totalMhz += info.CurrentMhz;
                    samples++;
                }

                if (samples == 0)
                {
                    averageMhz = 0;
                    return false;
                }

                averageMhz = (int)Math.Round(totalMhz / (double)samples);
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static string GetProcessorName()
        {
            try
            {
                const string keyPath = @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0";
                const string valueName = "ProcessorNameString";

                string? processorName = Registry.GetValue(keyPath, valueName, null) as string;
                if (!string.IsNullOrWhiteSpace(processorName))
                {
                    return processorName.Trim();
                }
            }
            catch
            {
                // Fall back below if registry lookup fails.
            }

            string? fallback = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
            return string.IsNullOrWhiteSpace(fallback) ? "Unknown Processor" : fallback.Trim();
        }

        private static ulong ToUInt64(FileTime fileTime)
        {
            return ((ulong)fileTime.HighDateTime << 32) | fileTime.LowDateTime;
        }

        private static bool TryGetMemoryStatus(out MemoryStatusEx memoryStatus)
        {
            memoryStatus = new MemoryStatusEx
            {
                Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
            };

            return GlobalMemoryStatusEx(ref memoryStatus);
        }

        private static double BytesToGb(ulong valueInBytes)
        {
            return valueInBytes / (1024d * 1024d * 1024d);
        }

        private static bool TryGetStorageStatus(out double totalStorageGb, out double usedStorageGb, out double storageUsagePercent)
        {
            totalStorageGb = 0;
            usedStorageGb = 0;
            storageUsagePercent = 0;

            try
            {
                ulong totalBytes = 0;
                ulong freeBytes = 0;

                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady || drive.DriveType != DriveType.Fixed)
                    {
                        continue;
                    }

                    totalBytes += (ulong)drive.TotalSize;
                    freeBytes += (ulong)drive.TotalFreeSpace;
                }

                if (totalBytes == 0)
                {
                    return false;
                }

                ulong usedBytes = totalBytes - freeBytes;

                totalStorageGb = BytesToGb(totalBytes);
                usedStorageGb = BytesToGb(usedBytes);
                storageUsagePercent = usedBytes * 100.0 / totalBytes;
                return true;
            }
            catch
            {
                return false;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileTime
        {
            public uint LowDateTime;
            public uint HighDateTime;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MemoryStatusEx
        {
            public uint Length;
            public uint MemoryLoad;
            public ulong TotalPhys;
            public ulong AvailPhys;
            public ulong TotalPageFile;
            public ulong AvailPageFile;
            public ulong TotalVirtual;
            public ulong AvailVirtual;
            public ulong AvailExtendedVirtual;
        }

        [Flags]
        private enum DisplayDeviceStateFlags : int
        {
            PrimaryDevice = 0x4,
            MirroringDriver = 0x8
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DisplayDevice
        {
            public int Cb;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;

            public DisplayDeviceStateFlags StateFlags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceId;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;

            public static DisplayDevice Create()
            {
                return new DisplayDevice
                {
                    Cb = Marshal.SizeOf<DisplayDevice>(),
                    DeviceName = string.Empty,
                    DeviceString = string.Empty,
                    DeviceId = string.Empty,
                    DeviceKey = string.Empty
                };
            }
        }

        private enum PowerInformationLevel
        {
            ProcessorInformation = 11
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessorPowerInformation
        {
            public uint Number;
            public uint MaxMhz;
            public uint CurrentMhz;
            public uint MhzLimit;
            public uint MaxIdleState;
            public uint CurrentIdleState;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(
            out FileTime idleTime,
            out FileTime kernelTime,
            out FileTime userTime);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx memoryStatus);

        [DllImport("powrprof.dll", SetLastError = true)]
        private static extern uint CallNtPowerInformation(
            PowerInformationLevel informationLevel,
            IntPtr inputBuffer,
            uint inputBufferLength,
            IntPtr outputBuffer,
            uint outputBufferLength);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumDisplayDevices(
            string? device,
            uint deviceNumber,
            ref DisplayDevice displayDevice,
            uint flags);
    }
}
