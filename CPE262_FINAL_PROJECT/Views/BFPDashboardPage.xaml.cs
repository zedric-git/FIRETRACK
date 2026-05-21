using CPE262_FINAL_PROJECT.Models;
using CPE262_FINAL_PROJECT.Repositories;
using CPE262_FINAL_PROJECT.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Windows.UI;
using Microsoft.UI.Xaml.Navigation;

namespace CPE262_FINAL_PROJECT.Views
{
    public sealed partial class BFPDashboardPage : Page
    {
        private readonly IncidentRepository      _incidentRepo      = new();
        private readonly AuditLogRepository      _auditRepo         = new();
        private readonly FamilyRepository        _familyRepo        = new();
        private readonly CitizenReportRepository _citizenReportRepo = new();

        private bool   _mapReady           = false;
        private bool   _dropModeOn         = false;
        private double _pinnedLat          = 0;
        private double _pinnedLng          = 0;
        private bool   _pinDropped         = false;
        private bool   _analysisMode       = false;
        private bool   _citizenReportsMode = false;
        private bool   _chartWebViewReady  = false;
        private Window?  _parentWindow      = null;

        public BFPDashboardPage()
        {
            InitializeComponent();
            OperatorText.Text = SessionManager.FullName;

            var timer = Microsoft.UI.Dispatching.DispatcherQueue
                            .GetForCurrentThread().CreateTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += (_, _) => ClockText.Text = $"TIME: {DateTime.Now:HH:mm:ss}";
            timer.Start();

            InitMapAsync();
            InitChartWebViewAsync();
            UpdateCitizenReportsToggleButton();
        }

        private async void InitMapAsync()
        {
            try
            {
                await MapWebView.EnsureCoreWebView2Async();
                MapWebView.CoreWebView2.Settings.IsWebMessageEnabled = true;
                MapWebView.CoreWebView2.WebMessageReceived += OnMapMessage;
                var mapPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Map", "map.html");
                MapWebView.Source = new Uri(mapPath);
            }
            catch (Exception ex)
            {
                MapLoadingOverlay.Visibility = Visibility.Collapsed;
                ShowFormError($"Could not initialize map: {ex.Message}");
            }
        }

        private async void InitChartWebViewAsync()
        {
            try
            {
                await ChartWebView.EnsureCoreWebView2Async();
                ChartWebView.CoreWebView2.Settings.IsWebMessageEnabled = false;
                _chartWebViewReady = true;
                ShowChartPlaceholder();
            }
            catch (Exception ex)
            {
                ShowFormError($"Could not initialize chart view: {ex.Message}");
            }
        }

        private void MapWebView_NavigationCompleted(object sender,
            CoreWebView2NavigationCompletedEventArgs e) { }

        private void OnMapMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    var json = e.TryGetWebMessageAsString();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    var type = root.GetProperty("type").GetString();

                    switch (type)
                    {
                        case "map_ready":
                            _mapReady = true;
                            MapLoadingOverlay.Visibility = Visibility.Collapsed;
                            LoadIncidentMarkers();
                            break;

                        case "pin_dropped":
                            OnPinDropped(root);
                            break;

                        case "pin_rejected":
                            ShowFormError("Cannot drop pin outside the Cebu City boundary.");
                            _dropModeOn = false;
                            UpdateDropPinButton();
                            break;

                        case "request_status_update":
                            if (root.TryGetProperty("incidentId", out var idEl))
                            {
                                var idStr = idEl.GetInt32().ToString();
                                StatusIncidentIdBox.Text       = idStr;
                                StatusOverlaySubtitle.Text     = $"Incident #{idStr}";
                                StatusErrorBanner.Visibility   = Visibility.Collapsed;
                                NewStatusBox.SelectedIndex     = 0;
                                StatusUpdateOverlay.Visibility = Visibility.Visible;
                            }
                            break;

                        case "request_delete_incident":
                            if (root.TryGetProperty("incidentId", out var deleteIdEl))
                                _ = DeleteIncidentAsync(deleteIdEl.GetInt32());
                            break;

                        case "brgy_selected":
                            if (_analysisMode)
                            {
                                var brgyName = TryGet(root, "barangay");
                                AnalysisBarangayName.Text = brgyName;
                                LoadBrgyCharts(brgyName);
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Map message error: {ex.Message}");
                }
            });
        }

        private void OnPinDropped(JsonElement root)
        {
            _pinnedLat  = root.GetProperty("lat").GetDouble();
            _pinnedLng  = root.GetProperty("lng").GetDouble();
            _pinDropped = true;
            _dropModeOn = false;

            LatBox.Text = _pinnedLat.ToString("F6");
            LngBox.Text = _pinnedLng.ToString("F6");

            var barangay = TryGet(root, "barangay");
            var sitio    = TryGet(root, "sitio");
            var green    = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                               Microsoft.UI.ColorHelper.FromArgb(255, 76, 175, 80));

            if (!string.IsNullOrEmpty(barangay))
            { BarangayBox.Text = barangay; BarangayFieldBorder.BorderBrush = green; }
            if (!string.IsNullOrEmpty(sitio))
            { SitioBox.Text = sitio; SitioFieldBorder.BorderBrush = green; }

            HideFormError();
            UpdateDropPinButton();
        }

        private static string TryGet(JsonElement el, string key)
        {
            try { return el.GetProperty(key).GetString() ?? ""; }
            catch { return ""; }
        }

        private void SendToMap(object payload)
        {
            if (!_mapReady) return;
            MapWebView.CoreWebView2.PostWebMessageAsString(JsonSerializer.Serialize(payload));
        }

        private void LoadIncidentMarkers()
        {
            try
            {
                var incidents = _incidentRepo.GetAll();
                var markers   = new System.Collections.Generic.List<object>();
                foreach (var inc in incidents)
                    markers.Add(new {
                        id = inc.IncidentID, lat = inc.GPSLat, lng = inc.GPSLong,
                        barangay = inc.Barangay, sitio = inc.Sitio,
                        alarmLevel = inc.AlarmLevel, cause = inc.CauseOfFire,
                        status = inc.Status, dateTime = inc.DateTime
                    });
                SendToMap(new { type = "load_incidents", incidents = markers });
            }
            catch (Exception ex)
            {
                ShowFormError($"Could not load incident markers: {ex.Message}");
            }
        }

        private void DropPin_Click(object sender, RoutedEventArgs e)
        {
            if (!_mapReady) return;
            _dropModeOn = !_dropModeOn;
            if (_dropModeOn)
            {
                SendToMap(new { type = "enable_drop_mode" });
                LatBox.Text = LngBox.Text = string.Empty;
                _pinDropped = false;
                ResetFieldBorders();
            }
            else SendToMap(new { type = "cancel_drop_mode" });
            UpdateDropPinButton();
        }

        private void UpdateDropPinButton()
        {
            var orange = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(255, 255, 152, 0));
            var green  = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(255, 76, 175, 80));
            if (_dropModeOn)
            { DropPinText.Text = "CLICK INSIDE CEBU CITY TO DROP PIN  (Cancel)"; DropPinBtn.BorderBrush = orange; }
            else if (_pinDropped)
            { DropPinText.Text = "PIN DROPPED - Click to reset"; DropPinBtn.BorderBrush = green; }
            else
            { DropPinText.Text = "DROP PIN ON MAP"; DropPinBtn.BorderBrush = green; }
        }

        private void RefreshMarkers_Click(object sender, RoutedEventArgs e) => LoadIncidentMarkers();

        private void AnalysisToggle_Click(object sender, RoutedEventArgs e)
        {
            if (!_mapReady) return;
            _analysisMode = !_analysisMode;

            if (_analysisMode)
            {
                try
                {
                    var rows    = _incidentRepo.GetSeverityByBarangay();
                    var payload = new System.Collections.Generic.List<object>();
                    foreach (var (brgy, cnt, avg) in rows)
                        payload.Add(new { name = brgy, count = cnt, avgAlarm = avg });
                    SendToMap(new { type = "analysis_mode", barangays = payload });
                }
                catch (Exception ex)
                {
                    _analysisMode = false;
                    ShowFormError($"Could not load analysis data: {ex.Message}");
                    return;
                }

                RegisterFormPanel.Visibility   = Visibility.Collapsed;
                AnalysisPanelBorder.Visibility = Visibility.Visible;
                AnalysisBarangayName.Text      = "Click a barangay polygon on the map";
                ShowChartPlaceholder();

                AnalysisToggleBtn.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.ColorHelper.FromArgb(255, 58, 46, 16));
            }
            else
            {
                SendToMap(new { type = "exit_analysis" });

                RegisterFormPanel.Visibility   = Visibility.Visible;
                AnalysisPanelBorder.Visibility = Visibility.Collapsed;

                AnalysisToggleBtn.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.ColorHelper.FromArgb(0, 0, 0, 0));
            }
        }

        private void ShowChartPlaceholder()
        {
            if (!_chartWebViewReady) return;
            ChartWebView.NavigateToString(
                "<html><head><meta charset='UTF-8'/>" +
                "<style>body{background:#0D1B2A;display:flex;align-items:center;" +
                "justify-content:center;height:100vh;margin:0;" +
                "font-family:Consolas,monospace;color:#4A6B8A;" +
                "text-align:center;flex-direction:column;gap:14px;}" +
                ".icon{font-size:48px}.msg{font-size:11px;letter-spacing:1px;line-height:1.8}" +
                "</style></head><body>" +
                "<div class='icon'>[CHART]</div>" +
                "<div class='msg'>CLICK A BARANGAY POLYGON<br/>ON THE MAP TO LOAD CHARTS</div>" +
                "</body></html>");
        }

        private void LoadBrgyCharts(string barangay)
        {
            if (!_chartWebViewReady || string.IsNullOrEmpty(barangay)) return;

            try
            {
            var incidents = _incidentRepo.GetByBarangay(barangay);
            var (totalFamilies, totalMembers) = _familyRepo.GetTotalDisplacedByBarangay(barangay);

            double avgAlarm = incidents.Count > 0
                ? (double)incidents.Sum(i => i.AlarmLevel) / incidents.Count
                : 0;

            var now = DateTime.Now;
            var monthLabels = new System.Collections.Generic.List<string>();
            var monthData   = new System.Collections.Generic.List<int>();
            for (int m = 11; m >= 0; m--)
            {
                var d = now.AddMonths(-m);
                monthLabels.Add(d.ToString("MMM yyyy"));
                monthData.Add(incidents.Count(i =>
                    DateTime.TryParse(i.DateTime, out var dt) &&
                    dt.Year == d.Year && dt.Month == d.Month));
            }

            var alarmData = new[]
            {
                incidents.Count(i => i.AlarmLevel == 1),
                incidents.Count(i => i.AlarmLevel == 2),
                incidents.Count(i => i.AlarmLevel == 3),
                incidents.Count(i => i.AlarmLevel == 4)
            };

            var causeCounts = incidents
                .GroupBy(i => string.IsNullOrEmpty(i.CauseOfFire) ? "Unknown" : i.CauseOfFire)
                .OrderByDescending(g => g.Count())
                .ToList();

            string mlJs = JsonSerializer.Serialize(monthLabels);
            string mdJs = JsonSerializer.Serialize(monthData);
            string alJs = JsonSerializer.Serialize(new[] { "Level 1","Level 2","Level 3","Level 4" });
            string adJs = JsonSerializer.Serialize(alarmData);
            string clJs = JsonSerializer.Serialize(causeCounts.Select(g => g.Key).ToArray());
            string cdJs = JsonSerializer.Serialize(causeCounts.Select(g => g.Count()).ToArray());

            double avgResolutionHours = _incidentRepo.GetAvgResolutionHoursByBarangay(barangay);

            ChartWebView.NavigateToString(
                BuildChartsHtml(incidents.Count, avgAlarm, totalFamilies, totalMembers,
                                avgResolutionHours,
                                mlJs, mdJs, alJs, adJs, clJs, cdJs));
            }
            catch (Exception ex)
            {
                ChartWebView.NavigateToString(
                    "<html><body style='background:#0D1B2A;color:#ff7878;font-family:Consolas,monospace;padding:24px'>" +
                    $"Could not load charts: {System.Net.WebUtility.HtmlEncode(ex.Message)}</body></html>");
            }
        }

        private static string BuildChartsHtml(
            int totalIncidents, double avgAlarm,
            int totalFamilies, int totalMembers,
            double avgResolutionHours,
            string mlJs, string mdJs,
            string alJs, string adJs,
            string clJs, string cdJs)
        {
            string resStr = avgResolutionHours < 0 ? "N/A"
                : avgResolutionHours >= 24 ? $"{avgResolutionHours / 24:F1}d"
                : avgResolutionHours >= 1  ? $"{avgResolutionHours:F1}h"
                : $"{avgResolutionHours * 60:F0}m";

            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html lang='en'><head><meta charset='UTF-8'/><style>");
            sb.Append("*{margin:0;padding:0;box-sizing:border-box;}");
            sb.Append("body{background:#0D1B2A;color:#fff;font-family:Consolas,monospace;padding:14px;overflow-y:auto;}");
            sb.Append(".sr{display:flex;gap:10px;margin-bottom:12px;}");
            sb.Append(".st{flex:1;background:#1E2D3D;border:1px solid #2A3F52;border-radius:4px;padding:10px;}");
            sb.Append(".sl{font-size:8px;letter-spacing:1px;color:#4A6B8A;}");
            sb.Append(".sv{font-size:18px;font-weight:bold;margin-top:4px;}");
            sb.Append(".bl{background:#1E2D3D;border:1px solid #2A3F52;border-radius:4px;padding:12px;margin-bottom:10px;}");
            sb.Append(".bt{font-size:9px;letter-spacing:1px;color:#FFC107;margin-bottom:10px;}");
            sb.Append("canvas{max-height:160px;}");
            sb.Append("</style></head><body>");

            sb.Append("<div class='sr'>");
            sb.AppendFormat("<div class='st'><div class='sl'>INCIDENTS</div><div class='sv' style='color:#FF5252'>{0}</div></div>", totalIncidents);
            sb.AppendFormat("<div class='st'><div class='sl'>AVG ALARM</div><div class='sv' style='color:#FF9800'>{0:F1}</div></div>", avgAlarm);
            sb.AppendFormat("<div class='st'><div class='sl'>FAMILIES</div><div class='sv' style='color:#4D96FF'>{0}</div></div>", totalFamilies);
            sb.AppendFormat("<div class='st'><div class='sl'>MEMBERS</div><div class='sv' style='color:#A0B4C8'>{0}</div></div>", totalMembers);
            sb.Append("</div>");
            sb.Append("<div class='sr' style='margin-bottom:12px;'>");
            sb.AppendFormat("<div class='st' style='flex:1'><div class='sl'>AVG RESOLUTION TIME</div><div class='sv' style='color:#FFC107'>{0}</div></div>", resStr);
            sb.Append("</div>");

            sb.Append("<div class='bl'><div class='bt'>INCIDENTS BY MONTH (LAST 12)</div><canvas id='mc'></canvas></div>");
            sb.Append("<div class='bl'><div class='bt'>ALARM LEVEL DISTRIBUTION</div><canvas id='ac'></canvas></div>");
            sb.Append("<div class='bl'><div class='bt'>CAUSE OF FIRE BREAKDOWN</div><canvas id='cc'></canvas></div>");

            sb.Append("<script src='https://cdnjs.cloudflare.com/ajax/libs/Chart.js/4.4.0/chart.umd.min.js'></script>");
            sb.Append("<script>");
            sb.Append("Chart.defaults.color='#A0B4C8';Chart.defaults.borderColor='#2A3F52';");
            sb.Append("var G={color:'#2A3F52'};");
            sb.AppendFormat("var ML={0},MD={1},AL={2},AD={3},CL={4},CD={5};", mlJs, mdJs, alJs, adJs, clJs, cdJs);

            sb.Append("new Chart(document.getElementById('mc'),{type:'bar',");
            sb.Append("data:{labels:ML,datasets:[{label:'Incidents',data:MD,");
            sb.Append("backgroundColor:'rgba(255,82,82,0.5)',borderColor:'#FF5252',borderWidth:1}]},");
            sb.Append("options:{responsive:true,plugins:{legend:{display:false}},");
            sb.Append("scales:{x:{grid:G},y:{grid:G,ticks:{stepSize:1}}}}});");

            sb.Append("new Chart(document.getElementById('ac'),{type:'doughnut',");
            sb.Append("data:{labels:AL,datasets:[{data:AD,");
            sb.Append("backgroundColor:['#4CAF50','#FF9800','#FF5252','#9C27B0'],borderWidth:1}]},");
            sb.Append("options:{responsive:true,plugins:{legend:{position:'right',");
            sb.Append("labels:{font:{family:'Consolas',size:10}}}}}});");

            sb.Append("new Chart(document.getElementById('cc'),{type:'bar',");
            sb.Append("data:{labels:CL,datasets:[{label:'Incidents',data:CD,");
            sb.Append("backgroundColor:'rgba(255,193,7,0.5)',borderColor:'#FFC107',borderWidth:1}]},");
            sb.Append("options:{indexAxis:'y',responsive:true,plugins:{legend:{display:false}},");
            sb.Append("scales:{x:{grid:G,ticks:{stepSize:1}},y:{grid:G}}}});");

            sb.Append("</script></body></html>");
            return sb.ToString();
        }

        private async void BrowsePhoto_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.FileTypeFilter.Add(".jpg");
                picker.FileTypeFilter.Add(".jpeg");
                picker.FileTypeFilter.Add(".png");
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(
                    (Application.Current as App)!.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                var file = await picker.PickSingleFileAsync();
                if (file != null) PhotoPathBox.Text = file.Path;
            }
            catch (Exception ex)
            {
                ShowFormError($"Could not select photo: {ex.Message}");
            }
        }

        private async void Submit_Click(object sender, RoutedEventArgs e)
        {
            HideFormError(); HideFormSuccess();

            if (!_pinDropped || _pinnedLat == 0)
            { ShowFormError("Please drop a pin inside Cebu City to set the incident location."); return; }
            if (string.IsNullOrWhiteSpace(BarangayBox.Text))
            { ShowFormError("Barangay is required. Drop a pin to auto-fill."); return; }
            if (string.IsNullOrWhiteSpace(PhotoPathBox.Text))
            { ShowFormError("A site photo is mandatory before registering the incident."); return; }

            int alarmLevel = AlarmLevelBox.SelectedIndex + 1;
            var cause      = (CauseBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Unknown";

            SubmitNormal.Visibility  = Visibility.Collapsed;
            SubmitLoading.Visibility = Visibility.Visible;
            SubmitBtn.IsEnabled      = false;
            await Task.Delay(600);

            try
            {
                var incident = new CPE262_FINAL_PROJECT.Models.Incident
                {
                    Barangay     = BarangayBox.Text.Trim(), Sitio = SitioBox.Text.Trim(),
                    GPSLat       = _pinnedLat, GPSLong = _pinnedLng, AlarmLevel = alarmLevel,
                    DateTime     = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    CauseOfFire  = cause, PhotoPath = PhotoPathBox.Text.Trim(),
                    Status       = "Active", DSDWStatus = "Pending",
                    RegisteredBy = SessionManager.UserID
                };

                int newId = _incidentRepo.Create(incident);
                _auditRepo.Log(SessionManager.UserID, "CREATE", "Incidents", newId,
                    $"Incident registered at {incident.Barangay}, Level {alarmLevel}");

                ShowFormSuccess($"Incident #{newId} registered at {incident.Barangay}.");
                LoadIncidentMarkers();
                SendToMap(new { type = "fly_to", lat = _pinnedLat, lng = _pinnedLng, zoom = 16 });
                ClearFormFields();
            }
            catch (Exception ex)
            {
                ShowFormError($"Could not register incident: {ex.Message}");
            }
            finally
            {
                SubmitNormal.Visibility  = Visibility.Visible;
                SubmitLoading.Visibility = Visibility.Collapsed;
                SubmitBtn.IsEnabled      = true;
            }
        }

        private void CloseStatusOverlay_Click(object sender, RoutedEventArgs e)
        {
            StatusUpdateOverlay.Visibility = Visibility.Collapsed;
            StatusErrorBanner.Visibility   = Visibility.Collapsed;
        }

        private void UpdateStatus_Click(object sender, RoutedEventArgs e)
        {
            StatusErrorBanner.Visibility = Visibility.Collapsed;

            if (!int.TryParse(StatusIncidentIdBox.Text.Trim(), out int incidentId) || incidentId <= 0)
            {
                StatusErrorText.Text         = "Invalid incident ID.";
                StatusErrorBanner.Visibility = Visibility.Visible;
                return;
            }

            CPE262_FINAL_PROJECT.Models.Incident incident;
            var newStatus = (NewStatusBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Active";

            try
            {
                var foundIncident = _incidentRepo.GetById(incidentId);
                if (foundIncident == null)
                {
                    StatusErrorText.Text         = $"Incident #{incidentId} not found.";
                    StatusErrorBanner.Visibility = Visibility.Visible;
                    return;
                }

                incident = foundIncident;
                _incidentRepo.UpdateStatus(incidentId, newStatus);
                _auditRepo.Log(SessionManager.UserID, "UPDATE", "Incidents", incidentId,
                    $"Status changed: {incident.Status} to {newStatus}");
            }
            catch (Exception ex)
            {
                StatusErrorText.Text         = $"Could not update incident status: {ex.Message}";
                StatusErrorBanner.Visibility = Visibility.Visible;
                return;
            }

            StatusUpdateOverlay.Visibility = Visibility.Collapsed;
            LoadIncidentMarkers();

            if (newStatus == "Fire Out")
            {
                try
                {
                    ShowFireOutReport(incident, newStatus);
                }
                catch (Exception ex)
                {
                    ShowFormError($"Status updated, but the fire-out report could not be generated: {ex.Message}");
                }
            }
            else
                ShowFormSuccess($"Incident #{incidentId} status updated to {newStatus}.");
        }

        private async Task DeleteIncidentAsync(int incidentId)
        {
            HideFormError();
            HideFormSuccess();

            CPE262_FINAL_PROJECT.Models.Incident? incident;
            try
            {
                incident = _incidentRepo.GetById(incidentId);
            }
            catch (Exception ex)
            {
                ShowFormError($"Could not load incident #{incidentId}: {ex.Message}");
                return;
            }
            if (incident == null)
            {
                ShowFormError($"Incident #{incidentId} was already deleted or cannot be found.");
                LoadIncidentMarkers();
                return;
            }

            bool confirmed = await FireTrackDialog.ShowConfirmAsync(
                this,
                "DELETE INCIDENT",
                $"This will permanently delete Incident #{incidentId} in {incident.Barangay}, including its DSWD messages, registered families, and relief records. Other dashboards will no longer see it.",
                "DELETE INCIDENT",
                "CANCEL",
                danger: true);

            if (!confirmed) return;

            bool deleted;
            try
            {
                deleted = _incidentRepo.DeletePermanent(incidentId);
            }
            catch (Exception ex)
            {
                ShowFormError($"Could not delete incident #{incidentId}: {ex.Message}");
                return;
            }

            if (!deleted)
            {
                ShowFormError($"Incident #{incidentId} was not found.");
                LoadIncidentMarkers();
                return;
            }

            try
            {
                _auditRepo.Log(SessionManager.UserID, "DELETE", "Incidents", incidentId,
                    $"Permanently deleted incident at {incident.Barangay}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Delete audit log failed: {ex.Message}");
            }

            StatusUpdateOverlay.Visibility = Visibility.Collapsed;
            StatusErrorBanner.Visibility   = Visibility.Collapsed;
            LoadIncidentMarkers();
            ShowFormSuccess($"Incident #{incidentId} permanently deleted.");
        }

        private void ShowFireOutReport(CPE262_FINAL_PROJECT.Models.Incident incident, string newStatus)
        {
            var now = DateTime.Now;
            string duration = "Unknown";
            if (DateTime.TryParse(incident.DateTime, out var startDt))
            {
                var span = now - startDt;
                if (span.TotalHours >= 1)        duration = $"{(int)span.TotalHours}h {span.Minutes}m {span.Seconds}s";
                else if (span.TotalMinutes >= 1) duration = $"{(int)span.TotalMinutes}m {span.Seconds}s";
                else                             duration = $"{(int)span.TotalSeconds}s";
            }

            var families    = _familyRepo.GetByIncident(incident.IncidentID);
            int memberCount = 0;
            foreach (var f in families) memberCount += f.MemberCount;

            RptIncidentId.Text  = $"#{incident.IncidentID}";
            RptBarangay.Text    = incident.Barangay;
            RptSitio.Text       = string.IsNullOrEmpty(incident.Sitio)       ? "N/A" : incident.Sitio;
            RptCause.Text       = string.IsNullOrEmpty(incident.CauseOfFire) ? "N/A" : incident.CauseOfFire;
            RptAlarmLevel.Text  = $"Level {incident.AlarmLevel}";
            RptOfficer.Text     = SessionManager.FullName;
            RptTimeStarted.Text = incident.DateTime;
            RptTimeEnded.Text   = now.ToString("yyyy-MM-dd HH:mm:ss");
            RptDuration.Text    = duration;
            RptDsdwStatus.Text  = incident.DSDWStatus;
            RptFamilies.Text    = $"{families.Count} families ({memberCount} persons)";
            RptGps.Text         = $"{incident.GPSLat:F5}, {incident.GPSLong:F5}";
            RptGeneratedAt.Text = $"Generated by {SessionManager.FullName} on {now:yyyy-MM-dd HH:mm:ss}";
            ReportSubtitle.Text = $"Incident #{incident.IncidentID} - {incident.Barangay}";

            ReportOverlay.Visibility = Visibility.Visible;
        }

        private void CloseReport_Click(object sender, RoutedEventArgs e)
            => ReportOverlay.Visibility = Visibility.Collapsed;

        private void ClearForm_Click(object sender, RoutedEventArgs e)
        { ClearFormFields(); HideFormError(); HideFormSuccess(); }

        private void ClearFormFields()
        {
            LatBox.Text = LngBox.Text = BarangayBox.Text = SitioBox.Text = PhotoPathBox.Text = string.Empty;
            AlarmLevelBox.SelectedIndex = CauseBox.SelectedIndex = 0;
            _pinDropped = false; _pinnedLat = 0; _pinnedLng = 0; _dropModeOn = false;
            ResetFieldBorders(); UpdateDropPinButton();
            SendToMap(new { type = "cancel_drop_mode" });
        }

        private void ResetFieldBorders()
        {
            var normal = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(255, 42, 63, 82));
            BarangayFieldBorder.BorderBrush = normal;
            SitioFieldBorder.BorderBrush    = normal;
        }

        private void ShowFormError(string msg)
        { FormErrorText.Text = msg; FormErrorBanner.Visibility = Visibility.Visible; }
        private void HideFormError()   => FormErrorBanner.Visibility  = Visibility.Collapsed;
        private void ShowFormSuccess(string msg)
        { FormSuccessText.Text = msg; FormSuccessBanner.Visibility = Visibility.Visible; }
        private void HideFormSuccess() => FormSuccessBanner.Visibility = Visibility.Collapsed;

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
            if (e.WindowActivationState != WindowActivationState.Deactivated && _mapReady)
            {
                LoadIncidentMarkers();
                if (_citizenReportsMode) LoadCitizenReports();
            }
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        { SessionManager.Logout(); Frame.Navigate(typeof(LoginPage)); }

        private void CitizenReportsToggle_Click(object sender, RoutedEventArgs e)
        {
            _citizenReportsMode = !_citizenReportsMode;

            var transparent = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            var highlight   = new SolidColorBrush(Color.FromArgb(255, 26, 58, 32));

            if (_citizenReportsMode)
            {
                if (_analysisMode)
                {
                    _analysisMode = false;
                    SendToMap(new { type = "exit_analysis" });
                    AnalysisPanelBorder.Visibility = Visibility.Collapsed;
                    AnalysisToggleBtn.Background  = transparent;
                }

                RegisterFormPanel.Visibility         = Visibility.Collapsed;
                CitizenReportsPanelBorder.Visibility = Visibility.Visible;
                CitizenReportsToggleBtn.Background   = highlight;

                LoadCitizenReports();
            }
            else
            {
                RegisterFormPanel.Visibility         = Visibility.Visible;
                CitizenReportsPanelBorder.Visibility = Visibility.Collapsed;
                CitizenReportsToggleBtn.Background   = transparent;
            }
            UpdateCitizenReportsToggleButton();
        }

        private void LoadCitizenReports()
        {
            System.Collections.Generic.List<CitizenReport> reports;
            try
            {
                reports = _citizenReportRepo.GetAll();
            }
            catch (Exception ex)
            {
                CitizenReportsPanel.Children.Clear();
                CitizenReportsCount.Text = "Could not load reports";
                CitizenReportsPanel.Children.Add(new TextBlock
                {
                    Text = $"Could not load citizen reports: {ex.Message}",
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 230, 120, 120)),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 24, 0, 0)
                });
                return;
            }

            CitizenReportsPanel.Children.Clear();

            int verifiedCount = reports.Count(r => r.IsVerified);
            int pendingCount  = reports.Count - verifiedCount;
            CitizenReportsCount.Text = reports.Count == 0
                ? "No reports yet"
                : $"{reports.Count} total · {pendingCount} pending · {verifiedCount} verified";

            if (reports.Count == 0)
            {
                CitizenReportsPanel.Children.Add(new TextBlock
                {
                    Text                = "No citizen reports submitted yet.",
                    FontFamily          = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                    FontSize             = 11,
                    Foreground          = new SolidColorBrush(Color.FromArgb(255, 74, 107, 138)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin              = new Thickness(0, 24, 0, 0)
                });
                return;
            }

            foreach (var report in reports)
                CitizenReportsPanel.Children.Add(BuildCitizenReportCard(report));

            UpdateCitizenReportsToggleButton();
        }

        private void UpdateCitizenReportsToggleButton()
        {
            try
            {
                var reports = _citizenReportRepo.GetAll();
                int pending = reports.Count(r => !r.IsVerified);
                BfpCitizenReportsBadge.Visibility = pending > 0 ? Visibility.Visible : Visibility.Collapsed;
                BfpCitizenReportsBadgeCount.Text = pending.ToString();
            }
            catch
            {
                BfpCitizenReportsBadge.Visibility = Visibility.Collapsed;
                BfpCitizenReportsBadgeCount.Text = "0";
            }
        }

        private Border BuildCitizenReportCard(CitizenReport report)
        {
            bool verified = report.IsVerified;
            Color borderColor = verified
                ? Color.FromArgb(255, 76, 175, 80)
                : Color.FromArgb(255, 255, 152, 0);
            Color statusBg = verified
                ? Color.FromArgb(255, 26, 43, 26)
                : Color.FromArgb(255, 45, 35, 16);
            var consolas = new Microsoft.UI.Xaml.Media.FontFamily("Consolas");

            var card = new Border
            {
                Background      = new SolidColorBrush(Color.FromArgb(255, 14, 24, 36)),
                CornerRadius    = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                BorderBrush     = new SolidColorBrush(borderColor)
            };

            var stack = new StackPanel();

            var header = new Grid
            {
                Padding = new Thickness(14, 10, 14, 10),
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto }
                }
            };
            var headerLeft = new StackPanel { Spacing = 2 };
            headerLeft.Children.Add(new TextBlock
            {
                Text       = report.FullName,
                FontFamily = consolas, FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255))
            });
            headerLeft.Children.Add(new TextBlock
            {
                Text       = $"📞 {report.Phone}",
                FontFamily = consolas, FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 160, 180, 200))
            });
            Grid.SetColumn(headerLeft, 0);
            header.Children.Add(headerLeft);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top
            };
            actions.Children.Add(new Border
            {
                Background          = new SolidColorBrush(statusBg),
                CornerRadius        = new CornerRadius(3),
                Padding             = new Thickness(8, 3, 8, 3),
                Child = new TextBlock
                {
                    Text             = verified ? "✓ VERIFIED" : "⏳ PENDING",
                    FontFamily       = consolas, FontSize = 9,
                    FontWeight       = Microsoft.UI.Text.FontWeights.Bold,
                    Foreground       = new SolidColorBrush(borderColor),
                    CharacterSpacing = 100
                }
            });
            if (verified)
            {
                var removeBtn = new Button
                {
                    Content = "X",
                    Width = 24,
                    Height = 24,
                    Padding = new Thickness(0),
                    CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(Color.FromArgb(255, 45, 20, 20)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 255, 107, 107)),
                    BorderThickness = new Thickness(1),
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 128, 112)),
                    FontFamily = consolas,
                    FontSize = 11,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold
                };
                removeBtn.Click += (s, e) =>
                {
                    try { _citizenReportRepo.DismissFromBfpInbox(report.ReportID); } catch {  }
                    LoadCitizenReports();
                    UpdateCitizenReportsToggleButton();
                };
                actions.Children.Add(removeBtn);
            }
            Grid.SetColumn(actions, 1);
            header.Children.Add(actions);
            stack.Children.Add(header);

            var details = new StackPanel { Padding = new Thickness(14, 0, 14, 10), Spacing = 4 };
            details.Children.Add(new TextBlock
            {
                Text         = $"📍 {report.Address}",
                FontFamily   = consolas, FontSize = 11,
                Foreground   = new SolidColorBrush(Color.FromArgb(255, 200, 216, 232)),
                TextWrapping = TextWrapping.Wrap
            });
            details.Children.Add(new TextBlock
            {
                Text       = $"Brgy. {report.Barangay} · Submitted {report.SubmittedAt}",
                FontFamily = consolas, FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 74, 107, 138))
            });
            stack.Children.Add(details);

            if (!verified)
            {
                var callBtn = new Button
                {
                    Margin              = new Thickness(14, 0, 14, 14),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Background          = new SolidColorBrush(Color.FromArgb(255, 76, 175, 80)),
                    BorderThickness     = new Thickness(0),
                    Padding             = new Thickness(10, 8, 10, 8),
                    CornerRadius        = new CornerRadius(3),
                    Content = new StackPanel
                    {
                        Orientation         = Orientation.Horizontal,
                        Spacing             = 6,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Children =
                        {
                            new TextBlock { Text = "📞", FontSize = 14, VerticalAlignment = VerticalAlignment.Center },
                            new TextBlock
                            {
                                Text             = "CALL TO VERIFY",
                                FontFamily       = consolas, FontSize = 11,
                                FontWeight       = Microsoft.UI.Text.FontWeights.Bold,
                                Foreground       = new SolidColorBrush(Color.FromArgb(255, 26, 26, 26)),
                                CharacterSpacing = 80,
                                VerticalAlignment = VerticalAlignment.Center
                            }
                        }
                    }
                };
                callBtn.Click += async (s, e) => await ShowCallDialog(report);
                stack.Children.Add(callBtn);
            }

            card.Child = stack;
            return card;
        }

        private async Task ShowCallDialog(CitizenReport report)
        {
            var consolas = new Microsoft.UI.Xaml.Media.FontFamily("Consolas");
            var panelBg  = new SolidColorBrush(Color.FromArgb(255, 14, 24, 36));
            var headerBg = new SolidColorBrush(Color.FromArgb(255, 30, 45, 61));
            var fieldBg  = new SolidColorBrush(Color.FromArgb(255, 18, 31, 46));
            var border   = new SolidColorBrush(Color.FromArgb(255, 42, 63, 82));
            var muted    = new SolidColorBrush(Color.FromArgb(255, 74, 107, 138));
            var text     = new SolidColorBrush(Color.FromArgb(255, 200, 216, 232));
            var accent   = new SolidColorBrush(Color.FromArgb(255, 255, 128, 112));
            var amber    = new SolidColorBrush(Color.FromArgb(255, 255, 193, 7));
            var green    = new SolidColorBrush(Color.FromArgb(255, 76, 175, 80));

            var ring = new ProgressRing
            {
                IsActive = true,
                Width = 56,
                Height = 56,
                Foreground = amber,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var checkText = new TextBlock
            {
                Text                = "OK",
                FontFamily          = consolas,
                FontSize             = 32,
                FontWeight          = Microsoft.UI.Text.FontWeights.Bold,
                Foreground          = green,
                CharacterSpacing    = 160,
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility          = Visibility.Collapsed,
                Margin              = new Thickness(0, 8, 0, 8)
            };
            var signalText = new TextBlock
            {
                Text             = "OUTBOUND VERIFICATION CALL",
                FontFamily       = consolas,
                FontSize         = 10,
                FontWeight       = Microsoft.UI.Text.FontWeights.Bold,
                Foreground       = amber,
                CharacterSpacing = 120,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var statusText = new TextBlock
            {
                Text                = $"CALLING {report.Phone}",
                FontFamily          = consolas, FontSize = 13,
                FontWeight          = Microsoft.UI.Text.FontWeights.Bold,
                Foreground          = text,
                CharacterSpacing    = 80,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var subText = new TextBlock
            {
                Text                = $"Reaching {report.FullName}...",
                FontFamily          = consolas, FontSize = 10,
                Foreground          = muted,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping        = TextWrapping.Wrap,
                TextAlignment       = TextAlignment.Center
            };

            var closeBtn = new Button
            {
                Content = "CLOSE REPORT",
                IsEnabled = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = fieldBg,
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                Foreground = muted,
                FontFamily = consolas,
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                CharacterSpacing = 80,
                Padding = new Thickness(14, 10, 14, 10),
                CornerRadius = new CornerRadius(3)
            };
            var callBadgeText = new TextBlock
            {
                Text = "CALLING",
                FontFamily = consolas,
                FontSize = 9,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = amber,
                CharacterSpacing = 80
            };
            var callBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 45, 35, 16)),
                BorderBrush = amber,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Right,
                Child = callBadgeText
            };

            var header = new Border
            {
                Background = headerBg,
                BorderBrush = border,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(18, 14, 18, 14),
                Child = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                        new ColumnDefinition { Width = GridLength.Auto }
                    },
                    Children =
                    {
                        new StackPanel
                        {
                            Spacing = 3,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = "FIRETRACK VERIFY",
                                    FontFamily = consolas,
                                    FontSize = 14,
                                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                                    Foreground = text,
                                    CharacterSpacing = 120
                                },
                                new TextBlock
                                {
                                    Text = "CITIZEN REPORT CONFIRMATION",
                                    FontFamily = consolas,
                                    FontSize = 9,
                                    Foreground = muted,
                                    CharacterSpacing = 80
                                }
                            }
                        },
                        callBadge
                    }
                }
            };
            Grid.SetColumn(callBadge, 1);

            var metaGrid = new Grid
            {
                ColumnSpacing = 8,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                },
                Children =
                {
                    new Border
                    {
                        Background = fieldBg,
                        BorderBrush = border,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(12, 9, 12, 9),
                        Child = new StackPanel
                        {
                            Spacing = 2,
                            Children =
                            {
                                new TextBlock { Text = "REPORT ID", FontFamily = consolas, FontSize = 8, Foreground = muted, CharacterSpacing = 120 },
                                new TextBlock { Text = $"#{report.ReportID}", FontFamily = consolas, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = accent }
                            }
                        }
                    },
                    new Border
                    {
                        Background = fieldBg,
                        BorderBrush = border,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(12, 9, 12, 9),
                        Child = new StackPanel
                        {
                            Spacing = 2,
                            Children =
                            {
                                new TextBlock { Text = "BARANGAY", FontFamily = consolas, FontSize = 8, Foreground = muted, CharacterSpacing = 120 },
                                new TextBlock { Text = report.Barangay, FontFamily = consolas, FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = text, TextTrimming = TextTrimming.CharacterEllipsis }
                            }
                        }
                    }
                }
            };
            Grid.SetColumn((FrameworkElement)metaGrid.Children[1], 1);

            var content = new Border
            {
                Width = 400,
                Background = panelBg,
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Child = new StackPanel
                {
                    Children =
                    {
                        header,
                        new StackPanel
                        {
                            Spacing = 14,
                            Padding = new Thickness(22, 20, 22, 18),
                            Children =
                            {
                                metaGrid,
                                new Border
                                {
                                    Background = fieldBg,
                                    BorderBrush = border,
                                    BorderThickness = new Thickness(1),
                                    CornerRadius = new CornerRadius(4),
                                    Padding = new Thickness(18, 20, 18, 18),
                                    Child = new StackPanel
                                    {
                                        Spacing = 11,
                                        HorizontalAlignment = HorizontalAlignment.Stretch,
                                        Children = { ring, checkText, signalText, statusText, subText }
                                    }
                                },
                                closeBtn
                            }
                        }
                    }
                }
            };

            var popupClosed = new TaskCompletionSource<bool>();
            var overlay = new Grid
            {
                Width = ActualWidth,
                Height = ActualHeight,
                Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0))
            };
            content.HorizontalAlignment = HorizontalAlignment.Center;
            content.VerticalAlignment = VerticalAlignment.Center;
            overlay.Children.Add(content);
            var popup = new Popup
            {
                XamlRoot = XamlRoot,
                Child = overlay,
                IsLightDismissEnabled = false
            };
            SizeChangedEventHandler? resizeHandler = null;
            resizeHandler = (s, e) =>
            {
                overlay.Width = ActualWidth;
                overlay.Height = ActualHeight;
            };
            SizeChanged += resizeHandler;
            closeBtn.Click += (s, e) =>
            {
                popup.IsOpen = false;
                if (resizeHandler is not null) SizeChanged -= resizeHandler;
                popupClosed.TrySetResult(true);
            };

            DispatcherQueue.TryEnqueue(async () =>
            {
                await Task.Delay(3000);
                ring.IsActive   = false;
                ring.Visibility = Visibility.Collapsed;
                checkText.Visibility = Visibility.Visible;
                signalText.Text = "VERIFICATION COMPLETE";
                signalText.Foreground = green;
                statusText.Text = "REPORT VERIFIED";
                statusText.Foreground = green;
                callBadge.Background = new SolidColorBrush(Color.FromArgb(255, 26, 43, 26));
                callBadge.BorderBrush = green;
                callBadgeText.Text = "VERIFIED";
                callBadgeText.Foreground = green;
                subText.Text = $"Report from {report.FullName} confirmed.\nMarked as verified in the system.";
                closeBtn.IsEnabled = true;
                closeBtn.Background = green;
                closeBtn.BorderBrush = green;
                closeBtn.Foreground = new SolidColorBrush(Color.FromArgb(255, 14, 24, 36));

                try { _citizenReportRepo.SetVerified(report.ReportID); } catch {  }
            });

            popup.IsOpen = true;
            await popupClosed.Task;
            LoadCitizenReports();
            UpdateCitizenReportsToggleButton();
        }
    }
}
