using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CPE262_FINAL_PROJECT.Models;
using CPE262_FINAL_PROJECT.Repositories;
using CPE262_FINAL_PROJECT.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace CPE262_FINAL_PROJECT.ViewModels
{
    public partial class AdminDashboardViewModel : ObservableObject
    {
        private readonly UserRepository     _userRepo     = new();
        private readonly AuditLogRepository _auditRepo    = new();
        private readonly BarangayRepository _barangayRepo = new();

        private int _totalUsers;
        public int TotalUsers
        {
            get => _totalUsers;
            set => SetProperty(ref _totalUsers, value);
        }

        private int _activeUsers;
        public int ActiveUsers
        {
            get => _activeUsers;
            set => SetProperty(ref _activeUsers, value);
        }

        private int _disabledUsers;
        public int DisabledUsers
        {
            get => _disabledUsers;
            set => SetProperty(ref _disabledUsers, value);
        }

        private int _todayAuditCount;
        public int TodayAuditCount
        {
            get => _todayAuditCount;
            set => SetProperty(ref _todayAuditCount, value);
        }

        private List<User> _rawUsers = new();
        public ObservableCollection<User> FilteredUsers { get; } = new();

        private string _userSearchQuery = string.Empty;
        public string UserSearchQuery
        {
            get => _userSearchQuery;
            set
            {
                if (SetProperty(ref _userSearchQuery, value))
                {
                    ApplyUserFilter();
                }
            }
        }

        private string _selectedRoleFilter = "All";
        public string SelectedRoleFilter
        {
            get => _selectedRoleFilter;
            set
            {
                if (SetProperty(ref _selectedRoleFilter, value))
                {
                    ApplyUserFilter();
                }
            }
        }

        public string[] RoleFilters     { get; } = { "All", "BFP Officer", "DSWD Coordinator", "Barangay Official", "Citizen" };
        public string[] AssignableRoles { get; } = { "BFP Officer", "DSWD Coordinator", "Barangay Official", "Citizen" };

        private bool _isPanelOpen = false;
        public bool IsPanelOpen
        {
            get => _isPanelOpen;
            set => SetProperty(ref _isPanelOpen, value);
        }

        private bool _isEditMode = false;
        public bool IsEditMode
        {
            get => _isEditMode;
            set => SetProperty(ref _isEditMode, value);
        }

        private string _panelTitle = "CREATE NEW OPERATOR";
        public string PanelTitle
        {
            get => _panelTitle;
            set => SetProperty(ref _panelTitle, value);
        }

        private string _formFullName = string.Empty;
        public string FormFullName
        {
            get => _formFullName;
            set => SetProperty(ref _formFullName, value);
        }

        private string _formEmail = string.Empty;
        public string FormEmail
        {
            get => _formEmail;
            set => SetProperty(ref _formEmail, value);
        }

        private string _formRole = "BFP Officer";
        public string FormRole
        {
            get => _formRole;
            set
            {
                if (SetProperty(ref _formRole, value))
                {
                    ShowBarangayField = value == "Barangay Official";
                }
            }
        }

        private string _formPassword = string.Empty;
        public string FormPassword
        {
            get => _formPassword;
            set => SetProperty(ref _formPassword, value);
        }

        private string _formBarangay = string.Empty;
        public string FormBarangay
        {
            get => _formBarangay;
            set => SetProperty(ref _formBarangay, value);
        }

        private string _formError = string.Empty;
        public string FormError
        {
            get => _formError;
            set => SetProperty(ref _formError, value);
        }

        private bool _formHasError = false;
        public bool FormHasError
        {
            get => _formHasError;
            set => SetProperty(ref _formHasError, value);
        }

        private bool _formIsLoading = false;
        public bool FormIsLoading
        {
            get => _formIsLoading;
            set => SetProperty(ref _formIsLoading, value);
        }

        private bool _showBarangayField = false;
        public bool ShowBarangayField
        {
            get => _showBarangayField;
            set => SetProperty(ref _showBarangayField, value);
        }

        private int _editingUserId = 0;

        public ObservableCollection<string> BarangayList { get; } = new();

        private List<AuditLog> _rawLogs = new();
        public ObservableCollection<AuditLog> FilteredLogs { get; } = new();

        private string _logSearchQuery = string.Empty;
        public string LogSearchQuery
        {
            get => _logSearchQuery;
            set
            {
                if (SetProperty(ref _logSearchQuery, value))
                {
                    ApplyLogFilter();
                }
            }
        }

        private string _logActionFilter = "All";
        public string LogActionFilter
        {
            get => _logActionFilter;
            set
            {
                if (SetProperty(ref _logActionFilter, value))
                {
                    ApplyLogFilter();
                }
            }
        }

        public string[] ActionFilters { get; } =
            { "All","LOGIN","CREATE","UPDATE","DEACTIVATE","REACTIVATE","RESET_PASSWORD","DELETE","BACKUP" };

        private string _lastBackupTime = "Never";
        public string LastBackupTime
        {
            get => _lastBackupTime;
            set => SetProperty(ref _lastBackupTime, value);
        }

        private string _backupStatus = string.Empty;
        public string BackupStatus
        {
            get => _backupStatus;
            set => SetProperty(ref _backupStatus, value);
        }

        private bool _isBackingUp = false;
        public bool IsBackingUp
        {
            get => _isBackingUp;
            set => SetProperty(ref _isBackingUp, value);
        }

        private string _operatorName = SessionManager.FullName;
        public string OperatorName
        {
            get => _operatorName;
            set => SetProperty(ref _operatorName, value);
        }

        private string _currentTime = DateTime.Now.ToString("HH:mm:ss");
        public string CurrentTime
        {
            get => _currentTime;
            set => SetProperty(ref _currentTime, value);
        }

        public event Action<User>? DeleteRequested;
        public event Action<User>? ResetPasswordRequested;

        public AdminDashboardViewModel()
        {
            LoadBarangays();
            LoadAll();
        }

        public void LoadAll()
        {
            LoadUsers();
            LoadAuditLogs();
            RefreshOverview();
        }

        private void LoadBarangays()
        {
            BarangayList.Clear();
            try
            {
                foreach (var b in _barangayRepo.GetAllNames())
                    BarangayList.Add(b);
                if (BarangayList.Count > 0) FormBarangay = BarangayList[0];
            }
            catch (Exception ex)
            {
                FormError = $"Could not load barangays: {ex.Message}";
                FormHasError = true;
            }
        }

        private void RefreshOverview()
        {
            TotalUsers      = _rawUsers.Count;
            ActiveUsers     = _rawUsers.Count(u => u.IsActive);
            DisabledUsers   = _rawUsers.Count(u => !u.IsActive);
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            TodayAuditCount = _rawLogs.Count(l => l.Timestamp.StartsWith(today));
        }

        private void LoadUsers()
        {
            try
            {
                _rawUsers = _userRepo.GetAll(excludeAdmins: true);
                ApplyUserFilter();
            }
            catch (Exception ex)
            {
                _rawUsers = new();
                FilteredUsers.Clear();
                FormError = $"Could not load users: {ex.Message}";
                FormHasError = true;
            }
        }

        private void ApplyUserFilter()
        {
            FilteredUsers.Clear();
            var q = UserSearchQuery.Trim().ToLower();
            foreach (var u in _rawUsers)
            {
                bool matchSearch = string.IsNullOrEmpty(q)
                    || u.FullName.ToLower().Contains(q)
                    || u.Email.ToLower().Contains(q)
                    || u.AssignedBarangay.ToLower().Contains(q);
                bool matchRole = SelectedRoleFilter == "All" || u.Role == SelectedRoleFilter;
                if (matchSearch && matchRole) FilteredUsers.Add(u);
            }
            RefreshOverview();
        }

        [RelayCommand]
        private void OpenCreatePanel()
        {
            IsEditMode = false; PanelTitle = "CREATE NEW OPERATOR";
            FormFullName = string.Empty; FormEmail = string.Empty;
            FormRole = "BFP Officer"; FormPassword = string.Empty;
            FormBarangay = BarangayList.Count > 0 ? BarangayList[0] : string.Empty;
            FormError = string.Empty; FormHasError = false;
            ShowBarangayField = false; _editingUserId = 0;
            IsPanelOpen = true;
        }

        [RelayCommand]
        private void OpenEditPanel(User user)
        {
            IsEditMode = true; PanelTitle = "EDIT OPERATOR";
            FormFullName = user.FullName; FormEmail = user.Email;
            FormRole = user.Role; FormPassword = string.Empty;
            FormBarangay = string.IsNullOrEmpty(user.AssignedBarangay)
                ? (BarangayList.Count > 0 ? BarangayList[0] : "")
                : user.AssignedBarangay;
            FormError = string.Empty; FormHasError = false;
            ShowBarangayField = user.Role == "Barangay Official";
            _editingUserId = user.UserID;
            IsPanelOpen = true;
        }

        [RelayCommand] private void ClosePanel() => IsPanelOpen = false;

        [RelayCommand]
        private async Task SaveUserAsync()
        {
            FormError = string.Empty; FormHasError = false;

            if (string.IsNullOrWhiteSpace(FormFullName))
            { FormError = "Full name is required."; FormHasError = true; return; }
            if (string.IsNullOrWhiteSpace(FormEmail) || !FormEmail.Contains('@'))
            { FormError = "Valid email required."; FormHasError = true; return; }
            if (!IsEditMode && FormPassword.Length < 6)
            { FormError = "Password must be at least 6 characters."; FormHasError = true; return; }
            if (IsEditMode && !string.IsNullOrEmpty(FormPassword) && FormPassword.Length < 6)
            { FormError = "New password must be at least 6 characters (leave blank to keep current)."; FormHasError = true; return; }
            if (FormRole == "Barangay Official" && string.IsNullOrWhiteSpace(FormBarangay))
            { FormError = "Assigned barangay is required for Barangay Official."; FormHasError = true; return; }

            FormIsLoading = true;
            await Task.Delay(500);

            try
            {
                string? barangay = FormRole == "Barangay Official" ? FormBarangay : null;

                if (IsEditMode)
                {
                    var (ok, err) = _userRepo.UpdateCredentials(
                        _editingUserId,
                        FormFullName.Trim(),
                        FormEmail.Trim().ToLower(),
                        FormRole,
                        barangay);

                    if (!ok)
                    { FormError = err ?? "Failed to update user."; FormHasError = true; return; }

                    if (!string.IsNullOrWhiteSpace(FormPassword))
                    {
                        _userRepo.ResetPassword(_editingUserId, FormPassword);
                        TryLog(SessionManager.UserID, "RESET_PASSWORD", "Users", _editingUserId,
                            $"Password changed by admin for {FormEmail}");
                    }

                    TryLog(SessionManager.UserID, "UPDATE", "Users", _editingUserId,
                        $"Updated: Name={FormFullName.Trim()}, Email={FormEmail.Trim()}, Role={FormRole}" +
                        (barangay != null ? $", Barangay={barangay}" : ""));
                }
                else
                {
                    var newUser = new User
                    {
                        FullName = FormFullName.Trim(), Email = FormEmail.Trim().ToLower(),
                        Role = FormRole, AssignedBarangay = barangay ?? string.Empty
                    };
                    var (ok, err) = _userRepo.Create(newUser, FormPassword);
                    if (!ok)
                    { FormError = err ?? "Failed to create user."; FormHasError = true; return; }
                    TryLog(SessionManager.UserID, "CREATE", "Users", null,
                        $"Created {FormRole}: {FormEmail}" + (barangay != null ? $" ({barangay})" : ""));
                }

                IsPanelOpen = false;
                LoadAll();
            }
            catch (Exception ex)
            {
                FormError = $"Save failed: {ex.Message}";
                FormHasError = true;
            }
            finally
            {
                FormIsLoading = false;
            }
        }

        [RelayCommand]
        private void ToggleActive(User user)
        {
            try
            {
            bool next = !user.IsActive;
            _userRepo.SetActive(user.UserID, next);
            var auditLog = TryLog(SessionManager.UserID, next ? "REACTIVATE" : "DEACTIVATE",
                "Users", user.UserID, $"{user.Email} → {(next ? "active" : "disabled")}");
            UpdateUserStatusInMemory(user, next);
            if (auditLog != null) AddAuditLogInMemory(auditLog);
            }
            catch (Exception ex)
            {
                FormError = $"Could not update account status: {ex.Message}";
                FormHasError = true;
            }
        }

        private void UpdateUserStatusInMemory(User user, bool isActive)
        {
            var rawIndex = _rawUsers.FindIndex(u => u.UserID == user.UserID);
            var source = rawIndex >= 0 ? _rawUsers[rawIndex] : user;
            var updated = CopyWithStatus(source, isActive);

            if (rawIndex >= 0)
                _rawUsers[rawIndex] = updated;

            for (int i = 0; i < FilteredUsers.Count; i++)
            {
                if (FilteredUsers[i].UserID == user.UserID)
                {
                    FilteredUsers[i] = updated;
                    break;
                }
            }

            RefreshOverview();
        }

        private static User CopyWithStatus(User source, bool isActive) => new()
        {
            UserID           = source.UserID,
            FullName         = source.FullName,
            Role             = source.Role,
            Email            = source.Email,
            PasswordHash     = source.PasswordHash,
            IsActive         = isActive,
            AssignedBarangay = source.AssignedBarangay,
            PhoneNumber      = source.PhoneNumber,
            CreatedAt        = source.CreatedAt
        };

        private void AddAuditLogInMemory(AuditLog auditLog)
        {
            _rawLogs.Insert(0, auditLog);
            if (MatchesCurrentLogFilter(auditLog))
                FilteredLogs.Insert(0, auditLog);
            RefreshOverview();
        }

        private bool MatchesCurrentLogFilter(AuditLog log)
        {
            var q = LogSearchQuery.Trim().ToLower();
            bool ms = string.IsNullOrEmpty(q)
                || log.Action.ToLower().Contains(q)
                || log.TargetTable.ToLower().Contains(q)
                || log.Reason.ToLower().Contains(q)
                || log.UserID.ToString().Contains(q);
            bool ma = LogActionFilter == "All" || log.Action == LogActionFilter;
            return ms && ma;
        }

        private AuditLog? TryLog(int userId, string action, string targetTable = "",
            int? targetId = null, string reason = "")
        {
            try
            {
                return _auditRepo.Log(userId, action, targetTable, targetId, reason);
            }
            catch
            {
                return null;
            }
        }

        [RelayCommand]
        private void RequestDelete(User user) => DeleteRequested?.Invoke(user);

        public void ConfirmDelete(User user)
        {
            try
            {
                var (ok, err) = _userRepo.Delete(user.UserID);
                if (!ok)
                {
                    FormError = err ?? "Delete failed.";
                    FormHasError = true;
                    return;
                }
                TryLog(SessionManager.UserID, "DELETE", "Users", user.UserID,
                    $"Permanently deleted: {user.Email} ({user.Role})");
                LoadAll();
            }
            catch (Exception ex)
            {
                FormError = $"Delete failed: {ex.Message}";
                FormHasError = true;
            }
        }

        [RelayCommand]
        private void RequestResetPassword(User user) => ResetPasswordRequested?.Invoke(user);

        public void ConfirmResetPassword(User user, string newPassword)
        {
            try
            {
                _userRepo.ResetPassword(user.UserID, newPassword);
                TryLog(SessionManager.UserID, "RESET_PASSWORD", "Users", user.UserID,
                    $"Password reset for {user.Email}");
                LoadAll();
            }
            catch (Exception ex)
            {
                FormError = $"Password reset failed: {ex.Message}";
                FormHasError = true;
            }
        }

        private void LoadAuditLogs()
        {
            try
            {
                _rawLogs = _auditRepo.GetAll().ToList();
                ApplyLogFilter();
            }
            catch (Exception ex)
            {
                _rawLogs = new();
                FilteredLogs.Clear();
                FormError = $"Could not load audit logs: {ex.Message}";
                FormHasError = true;
            }
        }

        private void ApplyLogFilter()
        {
            FilteredLogs.Clear();
            var q = LogSearchQuery.Trim().ToLower();
            foreach (var l in _rawLogs)
            {
                bool ms = string.IsNullOrEmpty(q)
                    || l.Action.ToLower().Contains(q)
                    || l.TargetTable.ToLower().Contains(q)
                    || l.Reason.ToLower().Contains(q)
                    || l.UserID.ToString().Contains(q);
                bool ma = LogActionFilter == "All" || l.Action == LogActionFilter;
                if (ms && ma) FilteredLogs.Add(l);
            }
        }

        [RelayCommand]
        private async Task BackupDatabaseAsync()
        {
            IsBackingUp = true; BackupStatus = string.Empty;
            await Task.Delay(1200);
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                _auditRepo.Log(SessionManager.UserID, "BACKUP", "Database", null,
                    $"Backup requested at {timestamp}");
                LastBackupTime = timestamp;
                BackupStatus   = "✓ Backup event logged. Use mysqldump for full export.";
            }
            catch (Exception ex) { BackupStatus = $"✗ Failed: {ex.Message}"; }
            finally { IsBackingUp = false; }
        }

        public void Tick() => CurrentTime = DateTime.Now.ToString("HH:mm:ss");
    }
}
