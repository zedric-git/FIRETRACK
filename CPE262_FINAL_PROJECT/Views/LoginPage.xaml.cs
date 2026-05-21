using CPE262_FINAL_PROJECT.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;

namespace CPE262_FINAL_PROJECT.Views
{
    public sealed partial class LoginPage : Page
    {
        public LoginViewModel ViewModel { get; } = new();

        private static readonly SolidColorBrush _focusBg     = new(Colors.White);
        private static readonly SolidColorBrush _focusBorder = new(ColorHelper.FromArgb(255, 255, 128, 112));
        private static readonly SolidColorBrush _focusIcon   = new(ColorHelper.FromArgb(255, 255, 128, 112));
        private static readonly SolidColorBrush _normalBg     = new(ColorHelper.FromArgb(255, 30, 45, 61));
        private static readonly SolidColorBrush _normalBorder = new(ColorHelper.FromArgb(255, 42, 63, 82));
        private static readonly SolidColorBrush _normalIcon   = new(ColorHelper.FromArgb(255, 74, 107, 138));

        public LoginPage()
        {
            InitializeComponent();
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            ViewModel.LoginSucceeded  += OnLoginSucceeded;

            if (!string.IsNullOrWhiteSpace(App.DatabaseStartupError))
            {
                ViewModel.ErrorMessage = App.DatabaseStartupError;
                ViewModel.HasError = true;
            }

            var timer = Microsoft.UI.Dispatching.DispatcherQueue
                            .GetForCurrentThread().CreateTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += (_, _) => ClockText.Text = $"TIME: {DateTime.Now:HH:mm:ss} PST";
            timer.Start();
        }

        private void EmailBox_GotFocus(object sender, RoutedEventArgs e)
        {
            EmailBorder.Background  = _focusBg;
            EmailBorder.BorderBrush = _focusBorder;
            EmailIcon.Foreground    = _focusIcon;
            EmailBox.Foreground     = new SolidColorBrush(ColorHelper.FromArgb(255, 26, 26, 26));
        }

        private void EmailBox_LostFocus(object sender, RoutedEventArgs e)
        {
            EmailBorder.Background  = _normalBg;
            EmailBorder.BorderBrush = _normalBorder;
            EmailIcon.Foreground    = _normalIcon;
            EmailBox.Foreground     = new SolidColorBrush(Colors.White);
        }

        private void PasswordBox_GotFocus(object sender, RoutedEventArgs e)
        {
            PasswordBorder.Background  = _focusBg;
            PasswordBorder.BorderBrush = _focusBorder;
            PasswordIcon.Foreground    = _focusIcon;
            PasswordBox.Foreground     = new SolidColorBrush(ColorHelper.FromArgb(255, 26, 26, 26));
        }

        private void PasswordBox_LostFocus(object sender, RoutedEventArgs e)
        {
            PasswordBorder.Background  = _normalBg;
            PasswordBorder.BorderBrush = _normalBorder;
            PasswordIcon.Foreground    = _normalIcon;
            PasswordBox.Foreground     = new SolidColorBrush(Colors.White);
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
            => ViewModel.Password = PasswordBox.Password;

        private void ViewModel_PropertyChanged(object? sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ViewModel.IsLoading):
                    NormalState.Visibility  = ViewModel.IsLoading ? Visibility.Collapsed : Visibility.Visible;
                    LoadingState.Visibility = ViewModel.IsLoading ? Visibility.Visible   : Visibility.Collapsed;
                    LoginBtn.IsEnabled      = !ViewModel.IsLoading;
                    break;

                case nameof(ViewModel.HasError):
                case nameof(ViewModel.ErrorMessage):
                    ErrorBanner.Visibility = ViewModel.HasError ? Visibility.Visible : Visibility.Collapsed;
                    ErrorText.Text         = ViewModel.ErrorMessage;
                    break;
            }
        }

        private void OnLoginSucceeded(string role)
        {
            Frame.Navigate(typeof(LoadingPage), role);
            App.LaunchBackgroundLoginWindow();
        }

        private void RegisterLink_Click(object sender, RoutedEventArgs e)
            => Frame.Navigate(typeof(RegisterPage));
    }
}
