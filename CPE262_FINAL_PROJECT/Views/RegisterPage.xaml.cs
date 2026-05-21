using CPE262_FINAL_PROJECT.Repositories;
using CPE262_FINAL_PROJECT.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CPE262_FINAL_PROJECT.Views
{
    public sealed partial class RegisterPage : Page
    {
        public RegisterViewModel ViewModel { get; } = new();

        private static readonly SolidColorBrush _focusBg      = new(Colors.White);
        private static readonly SolidColorBrush _focusBorder  = new(ColorHelper.FromArgb(255, 255, 128, 112));
        private static readonly SolidColorBrush _normalBg     = new(ColorHelper.FromArgb(255, 30, 45, 61));
        private static readonly SolidColorBrush _normalBorder = new(ColorHelper.FromArgb(255, 42, 63, 82));

        public RegisterPage()
        {
            InitializeComponent();
            ViewModel.PropertyChanged   += ViewModel_PropertyChanged;
            ViewModel.RegisterSucceeded += OnRegisterSucceeded;
            LoadBarangays();
        }

        private void LoadBarangays()
        {
            try
            {
                var repo  = new BarangayRepository();
                var names = repo.GetAllNames();
                foreach (var name in names)
                    BarangayCombo.Items.Add(name);
            }
            catch {  }
        }

        private void BarangayCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ViewModel.AssignedBarangay = BarangayCombo.SelectedItem?.ToString() ?? string.Empty;

            BarangayBorder.BorderBrush = string.IsNullOrEmpty(ViewModel.AssignedBarangay)
                ? _normalBorder
                : _focusBorder;
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (ReferenceEquals(sender, NameBox))
            {
                NameBorder.Background  = _focusBg;
                NameBorder.BorderBrush = _focusBorder;
                NameBox.Foreground     = new SolidColorBrush(ColorHelper.FromArgb(255, 26, 26, 26));
            }
            if (ReferenceEquals(sender, PhoneBox))
            {
                PhoneBorder.Background  = _focusBg;
                PhoneBorder.BorderBrush = _focusBorder;
                PhoneBox.Foreground     = new SolidColorBrush(ColorHelper.FromArgb(255, 26, 26, 26));
            }
            if (ReferenceEquals(sender, EmailBox))
            {
                EmailBorder.Background  = _focusBg;
                EmailBorder.BorderBrush = _focusBorder;
                EmailBox.Foreground     = new SolidColorBrush(ColorHelper.FromArgb(255, 26, 26, 26));
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (ReferenceEquals(sender, NameBox))
            {
                NameBorder.Background  = _normalBg;
                NameBorder.BorderBrush = _normalBorder;
                NameBox.Foreground     = new SolidColorBrush(Colors.White);
            }
            if (ReferenceEquals(sender, PhoneBox))
            {
                PhoneBorder.Background  = _normalBg;
                PhoneBorder.BorderBrush = _normalBorder;
                PhoneBox.Foreground     = new SolidColorBrush(Colors.White);
            }
            if (ReferenceEquals(sender, EmailBox))
            {
                EmailBorder.Background  = _normalBg;
                EmailBorder.BorderBrush = _normalBorder;
                EmailBox.Foreground     = new SolidColorBrush(Colors.White);
            }
        }

        private void PasswordBox_GotFocus(object sender, RoutedEventArgs e)
        {
            PasswordBorder.Background  = _focusBg;
            PasswordBorder.BorderBrush = _focusBorder;
            PasswordBox.Foreground     = new SolidColorBrush(ColorHelper.FromArgb(255, 26, 26, 26));
        }

        private void PasswordBox_LostFocus(object sender, RoutedEventArgs e)
        {
            PasswordBorder.Background  = _normalBg;
            PasswordBorder.BorderBrush = _normalBorder;
            PasswordBox.Foreground     = new SolidColorBrush(Colors.White);
        }

        private void ConfirmBox_GotFocus(object sender, RoutedEventArgs e)
        {
            ConfirmBorder.Background      = _focusBg;
            ConfirmBorder.BorderBrush     = _focusBorder;
            ConfirmPasswordBox.Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 26, 26, 26));
        }

        private void ConfirmBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ConfirmBorder.Background      = _normalBg;
            ConfirmBorder.BorderBrush     = _normalBorder;
            ConfirmPasswordBox.Foreground = new SolidColorBrush(Colors.White);
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
            => ViewModel.Password = PasswordBox.Password;

        private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
            => ViewModel.ConfirmPassword = ConfirmPasswordBox.Password;

        private void ViewModel_PropertyChanged(object? sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ViewModel.IsLoading):
                    NormalState.Visibility  = ViewModel.IsLoading ? Visibility.Collapsed : Visibility.Visible;
                    LoadingState.Visibility = ViewModel.IsLoading ? Visibility.Visible   : Visibility.Collapsed;
                    RegisterBtn.IsEnabled   = !ViewModel.IsLoading;
                    break;
                case nameof(ViewModel.HasError):
                case nameof(ViewModel.ErrorMessage):
                    ErrorBanner.Visibility = ViewModel.HasError ? Visibility.Visible : Visibility.Collapsed;
                    ErrorText.Text         = ViewModel.ErrorMessage;
                    break;
            }
        }

        private async void OnRegisterSucceeded()
        {
            await FireTrackDialog.ShowInfoAsync(
                this,
                "ACCOUNT CREATED",
                "Your Citizen account has been created.\nYou may now log in.",
                "PROCEED TO LOGIN");
            Frame.Navigate(typeof(LoginPage));
        }

        private void BackToLoginBtn_Click(object sender, RoutedEventArgs e)
            => Frame.Navigate(typeof(LoginPage));
    }
}
