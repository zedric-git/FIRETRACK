using CPE262_FINAL_PROJECT.Models;
using CPE262_FINAL_PROJECT.Services;
using CPE262_FINAL_PROJECT.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using Microsoft.UI.Xaml.Navigation;

namespace CPE262_FINAL_PROJECT.Views
{
    public sealed partial class AdminDashboardPage : Page
    {
        public AdminDashboardViewModel ViewModel { get; } = new();
        private Window? _parentWindow = null;

        public AdminDashboardPage()
        {
            InitializeComponent();
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            var timer = Microsoft.UI.Dispatching.DispatcherQueue
                            .GetForCurrentThread().CreateTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += (_, _) => { ClockText.Text = $"TIME: {DateTime.Now:HH:mm:ss}"; ViewModel.Tick(); };
            timer.Start();
        }

        private void ViewModel_PropertyChanged(object? sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ViewModel.IsPanelOpen):
                    SidePanel.Visibility = ViewModel.IsPanelOpen ? Visibility.Visible : Visibility.Collapsed;
                    if (!ViewModel.IsPanelOpen) PanelPasswordBox.Password = string.Empty;
                    break;

                case nameof(ViewModel.IsEditMode):
                    PasswordSection.Visibility = ViewModel.IsEditMode ? Visibility.Collapsed : Visibility.Visible;
                    SaveBtnText.Text = ViewModel.IsEditMode ? "SAVE CHANGES" : "CREATE OPERATOR";
                    break;

                case nameof(ViewModel.FormHasError):
                case nameof(ViewModel.FormError):
                    PanelErrorBanner.Visibility = ViewModel.FormHasError ? Visibility.Visible : Visibility.Collapsed;
                    PanelErrorText.Text = ViewModel.FormError;
                    break;

                case nameof(ViewModel.FormIsLoading):
                    SaveBtn.IsEnabled = !ViewModel.FormIsLoading;
                    SaveBtnText.Text  = ViewModel.FormIsLoading ? "SAVING..." :
                                        (ViewModel.IsEditMode ? "SAVE CHANGES" : "CREATE OPERATOR");
                    break;
            }
        }

        private void UserSearch_TextChanged(object sender, TextChangedEventArgs e)
            => ViewModel.UserSearchQuery = UserSearchBox.Text;

        private void LogSearch_TextChanged(object sender, TextChangedEventArgs e)
            => ViewModel.LogSearchQuery = LogSearchBox.Text;

        private async void PanelField_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
                await ViewModel.SaveUserCommand.ExecuteAsync(null);
        }

        private void CreateUser_Click(object sender, RoutedEventArgs e)
            => ViewModel.OpenCreatePanelCommand.Execute(null);

        private void EditUser_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is User user)
                ViewModel.OpenEditPanelCommand.Execute(user);
        }

        private void ToggleActive_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is User user)
                ViewModel.ToggleActiveCommand.Execute(user);
        }

        private async void DeleteUser_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not User user) return;

            bool confirmed = await FireTrackDialog.ShowConfirmAsync(
                this,
                "CONFIRM DELETE",
                $"Permanently delete:\n\n{user.FullName}\n{user.Email}\n\nThis cannot be undone.",
                "DELETE PERMANENTLY",
                danger: true);

            if (confirmed)
                ViewModel.ConfirmDelete(user);
        }

        private async void ResetPassword_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not User user) return;

            var stack = new StackPanel { Spacing = 10 };
            stack.Children.Add(new TextBlock
            {
                Text         = $"New password for:\n{user.FullName}",
                FontFamily   = new FontFamily("Consolas"),
                FontSize     = 11,
                Foreground   = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 200, 216, 232)),
                TextWrapping = TextWrapping.Wrap
            });
            var pwBox = new PasswordBox
            {
                PlaceholderText = "Min. 6 characters",
                FontFamily      = new FontFamily("Consolas"),
                Background      = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 18, 31, 46)),
                BorderBrush     = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 42, 63, 82)),
                Foreground      = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 200, 216, 232)),
                Padding         = new Thickness(12, 10, 12, 10)
            };
            stack.Children.Add(pwBox);

            bool confirmed = await FireTrackDialog.ShowCustomConfirmAsync(
                this,
                "RESET PASSWORD",
                stack,
                "RESET");

            if (confirmed && pwBox.Password.Length >= 6)
                ViewModel.ConfirmResetPassword(user, pwBox.Password);
        }

        private void ClosePanel_Click(object sender, RoutedEventArgs e)
            => ViewModel.ClosePanelCommand.Execute(null);

        private void PanelPassword_Changed(object sender, RoutedEventArgs e)
            => ViewModel.FormPassword = PanelPasswordBox.Password;

        private async void SaveUser_Click(object sender, RoutedEventArgs e)
            => await ViewModel.SaveUserCommand.ExecuteAsync(null);

        private void RefreshLogs_Click(object sender, RoutedEventArgs e)
            => ViewModel.LoadAll();

        private async void Backup_Click(object sender, RoutedEventArgs e)
            => await ViewModel.BackupDatabaseCommand.ExecuteAsync(null);

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _parentWindow = ((App)Application.Current).MainWindow;
            if (_parentWindow != null)
                _parentWindow.Activated += Window_Activated;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            if (_parentWindow != null)
                _parentWindow.Activated -= Window_Activated;
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs e)
        {
            if (e.WindowActivationState != WindowActivationState.Deactivated)
                ViewModel.LoadAll();
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            SessionManager.Logout();
            Frame.Navigate(typeof(LoginPage));
        }
    }
}
