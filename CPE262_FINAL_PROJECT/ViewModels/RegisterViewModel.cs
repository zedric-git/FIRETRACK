using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CPE262_FINAL_PROJECT.Models;
using CPE262_FINAL_PROJECT.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CPE262_FINAL_PROJECT.ViewModels
{
    public partial class RegisterViewModel : ObservableObject
    {
        private readonly UserRepository _userRepo = new();

        private string _fullName = string.Empty;
        public string FullName
        {
            get => _fullName;
            set => SetProperty(ref _fullName, value);
        }

        private string _phoneNumber = string.Empty;
        public string PhoneNumber
        {
            get => _phoneNumber;
            set => SetProperty(ref _phoneNumber, value);
        }

        private string _assignedBarangay = string.Empty;
        public string AssignedBarangay
        {
            get => _assignedBarangay;
            set => SetProperty(ref _assignedBarangay, value);
        }

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

        private string _confirmPassword = string.Empty;
        public string ConfirmPassword
        {
            get => _confirmPassword;
            set => SetProperty(ref _confirmPassword, value);
        }

        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        private bool _hasError = false;
        public bool HasError
        {
            get => _hasError;
            set => SetProperty(ref _hasError, value);
        }

        private bool _isLoading = false;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string SelectedRole => "Citizen";

        public event Action? RegisterSucceeded;

        [RelayCommand]
        private async Task RegisterAsync()
        {
            ErrorMessage = string.Empty;
            HasError = false;

            if (string.IsNullOrWhiteSpace(FullName))
            { ErrorMessage = "Full name is required."; HasError = true; return; }

            if (string.IsNullOrWhiteSpace(AssignedBarangay))
            { ErrorMessage = "Please select your home barangay. This is required to receive alerts and relief updates for your area."; HasError = true; return; }

            var rawDigits = new string((PhoneNumber ?? "").Where(char.IsDigit).ToArray());
            string normalizedPhone;
            if (rawDigits.Length == 12 && rawDigits.StartsWith("63"))
                normalizedPhone = "0" + rawDigits.Substring(2);
            else if (rawDigits.Length == 10 && rawDigits.StartsWith("9"))
                normalizedPhone = "0" + rawDigits;
            else
                normalizedPhone = rawDigits;

            if (normalizedPhone.Length != 11 || !normalizedPhone.StartsWith("09"))
            {
                ErrorMessage = "Enter a valid PH mobile (e.g. 09171234567 or +639171234567).";
                HasError = true;
                return;
            }
            PhoneNumber = normalizedPhone;

            if (string.IsNullOrWhiteSpace(Email) || !Email.Contains('@'))
            { ErrorMessage = "Enter a valid email address."; HasError = true; return; }

            if (Password.Length < 6)
            { ErrorMessage = "Access token must be at least 6 characters."; HasError = true; return; }

            if (Password != ConfirmPassword)
            { ErrorMessage = "Access tokens do not match."; HasError = true; return; }

            IsLoading = true;
            await Task.Delay(1000);

            try
            {
                var newUser = new User
                {
                    FullName         = FullName.Trim(),
                    Email            = Email.Trim().ToLower(),
                    Role             = "Citizen",
                    PhoneNumber      = PhoneNumber,
                    AssignedBarangay = AssignedBarangay.Trim()
                };

                var (success, error) = _userRepo.Create(newUser, Password);

                if (!success)
                {
                    ErrorMessage = error ?? "Registration failed.";
                    HasError = true;
                    return;
                }

                RegisterSucceeded?.Invoke();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Registration failed: {ex.Message}";
                HasError = true;
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
