using CPE262_FINAL_PROJECT.Services;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Threading.Tasks;

namespace CPE262_FINAL_PROJECT.Views
{
    public sealed partial class LoadingPage : Page
    {
        public LoadingPage() { InitializeComponent(); }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var role = e.Parameter as string ?? SessionManager.Role;
            RoleBadge.Text = $"ROLE: {role.ToUpper()} // CEBU CITY OPS";

            await Task.Delay(600);
            StatusText.Text = "LOADING OPERATOR PROFILE...";
            await Task.Delay(600);
            StatusText.Text = "SYNCING INCIDENT DATABASE...";
            await Task.Delay(600);
            StatusText.Text = "ACCESS GRANTED";
            await Task.Delay(500);

            Type destination = role switch
            {
                "Admin"             => typeof(AdminDashboardPage),
                "BFP Officer"       => typeof(BFPDashboardPage),
                "DSWD Coordinator"  => typeof(DSDWDashboardPage),
                "Barangay Official" => typeof(BarangayDashboardPage),
                "Citizen"           => typeof(CitizenDashboardPage),
                _                   => typeof(LoginPage)
            };

            Frame.Navigate(destination);
        }
    }
}
