using CPE262_FINAL_PROJECT.Views;
using Microsoft.UI.Xaml;

namespace CPE262_FINAL_PROJECT
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 800));

            RootFrame.Navigate(typeof(LoginPage));
        }
    }
}
