using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Windowing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.UI;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CoolCores
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            ConfigureTitleBar();
        }

        private void ConfigureTitleBar()
        {
            if (!AppWindowTitleBar.IsCustomizationSupported())
            {
                return;
            }

            AppWindowTitleBar titleBar = AppWindow.TitleBar;
            Color darkBackground = Color.FromArgb(255, 0, 0, 0);
            Color hoverBackground = Color.FromArgb(255, 42, 42, 42);
            Color pressedBackground = Color.FromArgb(255, 60, 60, 60);
            Color foreground = Color.FromArgb(255, 255, 255, 255);
            Color inactiveForeground = Color.FromArgb(255, 180, 180, 180);

            titleBar.BackgroundColor = darkBackground;
            titleBar.ForegroundColor = foreground;
            titleBar.InactiveBackgroundColor = darkBackground;
            titleBar.InactiveForegroundColor = inactiveForeground;

            titleBar.ButtonBackgroundColor = darkBackground;
            titleBar.ButtonForegroundColor = foreground;
            titleBar.ButtonHoverBackgroundColor = hoverBackground;
            titleBar.ButtonHoverForegroundColor = foreground;
            titleBar.ButtonPressedBackgroundColor = pressedBackground;
            titleBar.ButtonPressedForegroundColor = foreground;
            titleBar.ButtonInactiveBackgroundColor = darkBackground;
            titleBar.ButtonInactiveForegroundColor = inactiveForeground;
        }
    }
}
