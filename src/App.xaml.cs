using ControlzEx.Theming;
using MahApps.Metro.Theming;
using System.Windows;
using Application = System.Windows.Application;

namespace VSSuite
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var theme = ThemeManager.Current.AddLibraryTheme(new LibraryTheme(new Uri("pack://application:,,,/VSSuite;component/Resources/Themes/Dark.DarkOliveGreen.xaml"), MahAppsLibraryThemeProvider.DefaultInstance));

            ThemeManager.Current.ChangeTheme(this, theme);
        }
    }
}