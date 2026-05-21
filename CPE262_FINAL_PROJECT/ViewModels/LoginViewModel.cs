using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CPE262_FINAL_PROJECT.Repositories;
using CPE262_FINAL_PROJECT.Services;
using System;
using System.Threading.Tasks;

namespace CPE262_FINAL_PROJECT.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly UserRepository _userRepo = new();

        private string _email = string.Empty;
        public string Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
        }

        private string _password = string.Empty;
        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        private bool _isLoading = false;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private bool _hasError = false;
        public bool HasError
        {
            get => _hasError;
            set => SetProperty(ref _hasError, value);
        }

        public event Action<string>? LoginSucceeded;

        [RelayCommand]
        private async Task LoginAsync()
        {
            ErrorMessage = string.Empty;
            HasError = false;

            if (string.IsNullOrWhiteSpace(Email))
            {
                ErrorMessage = "Please enter your email address.";
                HasError = true;
                return;
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Please enter your password.";
                HasError = true;
                return;
            }

            IsLoading = true;

            await Task.Delay(1200);

            try
            {
                var (user, error) = _userRepo.Authenticate(Email.Trim(), Password);

                if (user == null)
                {
                    ErrorMessage = error ?? "Authentication failed.";
                    HasError = true;
                    Password = string.Empty;
                    return;
                }

                SessionManager.Login(user.UserID, user.FullName, user.Role,
                    user.AssignedBarangay, user.PhoneNumber);

                try
                {
                    var auditRepo = new AuditLogRepository();
                    auditRepo.Log(user.UserID, "LOGIN", "Users", user.UserID);
                }
                catch { }

                LoginSucceeded?.Invoke(user.Role);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Login failed: {ex.Message}";
                HasError = true;
                Password = string.Empty;
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
