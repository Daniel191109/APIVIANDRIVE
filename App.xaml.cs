using Microsoft.Maui.Controls;

namespace ViandriveApp
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            MainPage = new AppShell();
        }
    }
}

