using Microsoft.Win32;
using Microsoft.UI.Xaml.Controls;
using System;

namespace CoolCores.Pages
{
    public sealed partial class SystemPage : Page
    {
        public SystemPage()
        {
            InitializeComponent();
            ProcessorNameTextBlock.Text = GetProcessorName();
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
    }
}
