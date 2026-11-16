using Microsoft.UI.Xaml;
using System;

namespace WindowsCleaner
{
    public partial class App : Application
    {
        public App()
        {
            Environment.SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY", AppContext.BaseDirectory);
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();
            m_window.Activate();
        }

        private Window m_window = null!;
    }
}