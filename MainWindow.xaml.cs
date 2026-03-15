using CoolCores.Pages;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using Windows.Graphics;
using Windows.UI;

namespace CoolCores
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private readonly Dictionary<string, Type> _pages = new(StringComparer.OrdinalIgnoreCase)
        {
            ["system"] = typeof(SystemPage),
            ["performance"] = typeof(PerformancePage),
            ["ai"] = typeof(AiPage),
            ["settings"] = typeof(SettingsPage)
        };

        public MainWindow()
        {
            InitializeComponent();
            ConfigureDefaultWindowSize();
            ConfigureTitleBar();
            InitializeNavigation();
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

        private void ConfigureDefaultWindowSize()
        {
            const double scale = 0.7;
            SizeInt32 currentSize = AppWindow.Size;

            int width = Math.Max(1, (int)Math.Round(currentSize.Width * scale));
            int height = Math.Max(1, (int)Math.Round(currentSize.Height * scale));

            AppWindow.Resize(new SizeInt32(width, height));

            DisplayArea displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            RectInt32 workArea = displayArea.WorkArea;

            int centeredX = workArea.X + Math.Max(0, (workArea.Width - width) / 2);
            int centeredY = workArea.Y + Math.Max(0, (workArea.Height - height) / 2);

            AppWindow.Move(new PointInt32(centeredX, centeredY));
        }

        private void InitializeNavigation()
        {
            if (AppNavigationView.MenuItems.Count == 0)
            {
                return;
            }

            AppNavigationView.SelectedItem = AppNavigationView.MenuItems[0];
            NavigateToPage("system");
        }

        private void AppNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                NavigateToPage("settings");
                return;
            }

            if (args.SelectedItemContainer?.Tag is string tag)
            {
                NavigateToPage(tag);
            }
        }

        private void NavigateToPage(string pageKey)
        {
            if (!_pages.TryGetValue(pageKey, out Type? pageType))
            {
                return;
            }

            if (ContentFrame.CurrentSourcePageType == pageType)
            {
                return;
            }

            ContentFrame.Navigate(pageType);
        }
    }
}
