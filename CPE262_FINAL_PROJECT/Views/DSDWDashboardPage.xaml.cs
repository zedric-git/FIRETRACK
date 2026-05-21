using CPE262_FINAL_PROJECT.Models;
using CPE262_FINAL_PROJECT.Repositories;
using CPE262_FINAL_PROJECT.Services;
using System.Text;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
using System.Text.Json;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.UI;
using Microsoft.UI.Xaml.Navigation;

namespace CPE262_FINAL_PROJECT.Views
{
    public sealed partial class DSDWDashboardPage : Page
    {
        private readonly ReliefRecordRepository   _reliefRepo    = new();
        private readonly IncidentRepository       _incidentRepo  = new();
        private readonly FamilyRepository         _familyRepo    = new();
        private readonly AuditLogRepository       _auditRepo     = new();
        private readonly BarangayRepository       _barangayRepo  = new();
        private readonly EvacuationCenterRepository _evacRepo      = new();
        private readonly DSWDMessageRepository    _dswdMsgRepo   = new();

        private List<Incident> _incidents = new();
        private List<Incident> _allIncidents = new();
        private List<Family>   _familiesForIncident = new();
        private List<(int RecordID, string HeadName, string Agency, string ItemType, int Qty, string Date)> _fullLedger = new();
        private int _distSelectedIncidentId = 0;
        private int _distSelectedFamilyId   = 0;

        private sealed class IncidentItem
        {
            public int    Id      { get; init; }
            public string Display { get; init; } = "";
            public override string ToString() => Display;
        }
        private sealed class FamilyItem
        {
            public int    Id      { get; init; }
            public string Display { get; init; } = "";
            public override string ToString() => Display;
        }

        private int    _selectedIncidentId     = 0;
        private string _selectedIncidentLabel  = "";

        private DispatcherQueueTimer? _toastTimer;

        private static readonly Color White  = Color.FromArgb(255, 200, 216, 232);

        private bool   _dsdwMapReady     = false;
        private bool   _analysisWebViewReady = false;
        private bool   _dsdwDropActive  = false;
        private double _dsdwPendingLat   = 0;
        private double _dsdwPendingLng   = 0;
        private string _dsdwPendingBarangay = "";
        private int    _dsdwOccCenterId  = 0;
        private string _currentSection   = "Overview";
        private Window? _parentWindow     = null;
        private int    _dsdwOccCapacity  = 0;
        private static readonly Color Green  = Color.FromArgb(255, 76, 175, 144);
        private static readonly Color Yellow = Color.FromArgb(255, 192, 154, 48);
        private static readonly Color Red    = Color.FromArgb(255, 192, 80, 80);

        public DSDWDashboardPage()
        {
            InitializeComponent();

            OperatorName.Text = SessionManager.FullName;

            var clock = DispatcherQueue.GetForCurrentThread().CreateTimer();
            clock.Interval = TimeSpan.FromSeconds(1);
            clock.Tick += (_, _) => ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
            clock.Start();

            _toastTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
            _toastTimer.Interval    = TimeSpan.FromSeconds(3);
            _toastTimer.IsRepeating = false;
            _toastTimer.Tick += (_, _) => ToastBanner.Visibility = Visibility.Collapsed;

            RefreshAllData();
            ShowSection("Overview");
            InitAnalysisWebViewAsync();
        }


        private void RefreshAllData()
        {
            _incidents = _incidentRepo.GetAll();
        }


        private void ShowSection(string section)
        {
            _currentSection = section;
            SectionOverview.Visibility     = Visibility.Collapsed;
            SectionDistribution.Visibility = Visibility.Collapsed;
            SectionGaps.Visibility         = Visibility.Collapsed;
            SectionIncidents.Visibility    = Visibility.Collapsed;
            SectionCityCenters.Visibility  = Visibility.Collapsed;
            SectionAnalysis.Visibility     = Visibility.Collapsed;
TopBarTitle.Text = section switch
            {
                "Overview"     => "OVERVIEW",
                "Distribution" => "RELIEF DISTRIBUTION",
                "Gaps"         => "GAPS & DUPLICATES",
                "Incidents"    => "ACTIVE INCIDENTS",
                "CityCenters"  => "CITY-LEVEL CENTERS",
                "Analysis"    => "ANALYSIS DASHBOARD",
_              => section.ToUpper()
            };
            TopBarSubtitle.Text = section switch
            {
                "Overview"     => "/ DSWD Relief Operations",
                "Distribution" => "/ Distribution Ledger",
                "Gaps"         => "/ Coverage Monitoring",
                "Incidents"    => "/ DSWD Response Status",
                "CityCenters"  => "/ Map & Manage City Centers",
                "Analysis"    => "/ City-wide relief & displacement insights",
_              => ""
            };

            SetNavActive(NavOverviewBtn,      false);
            SetNavActive(NavDistributionBtn,  false);
            SetNavActive(NavGapsBtn,          false);
            SetNavActive(NavIncidentsBtn,     false);
            SetNavActive(NavCityCentersBtn,  false);
            SetNavActive(NavAnalysisBtn,      false);
switch (section)
            {
                case "Overview":
                    SectionOverview.Visibility = Visibility.Visible;
                    SetNavActive(NavOverviewBtn, true);
                    BuildOverview();
                    break;
                case "Distribution":
                    SectionDistribution.Visibility = Visibility.Visible;
                    SetNavActive(NavDistributionBtn, true);
                    BuildDistributionSection();
                    break;
                case "Gaps":
                    SectionGaps.Visibility = Visibility.Visible;
                    SetNavActive(NavGapsBtn, true);
                    BuildGapsSection();
                    break;
                case "Incidents":
                    SectionIncidents.Visibility = Visibility.Visible;
                    SetNavActive(NavIncidentsBtn, true);
                    IncidentSearchBox.Text = "";
                    BuildIncidentsSection();
                    break;
                case "CityCenters":
                    SectionCityCenters.Visibility = Visibility.Visible;
                    SetNavActive(NavCityCentersBtn, true);
                    BuildCityCentersSection();
                    break;
                case "Analysis":
                    SectionAnalysis.Visibility = Visibility.Visible;
                    SetNavActive(NavAnalysisBtn, true);
                    BuildAnalysisSection();
                    break;

            }
        }

        private static void SetNavActive(Button btn, bool active)
        {
            if (btn.Content is not StackPanel sp) return;
            btn.Background = active
                ? new SolidColorBrush(Color.FromArgb(255, 15, 32, 53))
                : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            if (sp.Children.Count > 0 && sp.Children[0] is TextBlock title)
                title.Foreground = active
                    ? new SolidColorBrush(Color.FromArgb(255, 76, 175, 144))
                    : new SolidColorBrush(Color.FromArgb(255, 122, 155, 184));
        }

        private void NavOverview_Click(object sender, RoutedEventArgs e)
        { RefreshAllData(); ShowSection("Overview"); }
        private void NavDistribution_Click(object sender, RoutedEventArgs e)
        { RefreshAllData(); ShowSection("Distribution"); }
        private void NavGaps_Click(object sender, RoutedEventArgs e)
        { RefreshAllData(); ShowSection("Gaps"); }
        private void NavIncidents_Click(object sender, RoutedEventArgs e)
        { RefreshAllData(); ShowSection("Incidents"); }
        private void NavCityCenters_Click(object sender, RoutedEventArgs e)
        { ShowSection("CityCenters"); }
        private void NavAnalysis_Click(object sender, RoutedEventArgs e)
        { RefreshAllData(); ShowSection("Analysis"); }


        private void UpdateMessagesButton()
        {
            int pending = _dswdMsgRepo.CountPending();
            CitizenMsgBadge.Visibility = pending > 0 ? Visibility.Visible : Visibility.Collapsed;
            CitizenMsgBadgeCount.Text  = pending.ToString();
        }

        private void OpenCitizenMessagesOverlay_Click(object sender, RoutedEventArgs e)
        {
            var msgs = _dswdMsgRepo.GetAllForInbox();
            int pending = msgs.Count(m => m.Status == "Pending");
            CitizenMsgOverlaySubtitle.Text = pending > 0
                ? $"{msgs.Count} MESSAGE{(msgs.Count != 1 ? "S" : "")} · {pending} UNREAD"
                : $"{msgs.Count} MESSAGE{(msgs.Count != 1 ? "S" : "")} RECEIVED";

            CitizenMsgOverlayPanel.Children.Clear();

            if (msgs.Count == 0)
            {
                CitizenMsgOverlayPanel.Children.Add(MakeEmptyState(
                    "No citizen messages yet",
                    "Messages sent from the Citizen Portal will appear here.",
                    new Thickness(18, 32, 18, 32)));
            }
            else
            {
                foreach (var m in msgs)
                    CitizenMsgOverlayPanel.Children.Add(BuildMessageCard(m));
            }

            CitizenMessagesOverlay.Visibility = Visibility.Visible;
        }

        private void CloseCitizenMessagesOverlay_Click(object sender, RoutedEventArgs e)
        {
            CitizenMessagesOverlay.Visibility = Visibility.Collapsed;
            UpdateMessagesButton();
        }

        private Border BuildMessageCard(DSWDMessage m)
        {
            Color accentColor = m.Status == "Approved" ? Green
                              : m.Status == "Rejected"  ? Red
                              :                           Color.FromArgb(255, 192, 154, 48);

            var card = new Border
            {
                Background      = new SolidColorBrush(Color.FromArgb(255, 9, 19, 28)),
                BorderThickness = new Thickness(3, 0, 0, 0),
                BorderBrush     = new SolidColorBrush(accentColor),
                Padding         = new Thickness(16, 14, 16, 14),
                Margin          = new Thickness(0, 0, 0, 1)
            };
            var stack = new StackPanel { Spacing = 10 };

            var headerGrid = new Grid();
            var senderStack = new StackPanel { Spacing = 3 };
            senderStack.Children.Add(new TextBlock
            {
                Text       = m.SenderName,
                FontFamily = new FontFamily("Consolas"), FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(White)
            });
            string phone = string.IsNullOrEmpty(m.SenderPhone) ? "no phone on file" : m.SenderPhone;
            senderStack.Children.Add(new TextBlock
            {
                Text       = $"TEL: {phone}  |  Incident #{m.IncidentID}  |  Brgy. {m.IncidentBrgy}  |  {m.IncidentStatus.ToUpper()}",
                FontFamily = new FontFamily("Consolas"), FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 76, 175, 144))
            });

            string householdLine = m.FamilyID > 0
                ? $"HOUSEHOLD: {m.FamilyHeadName}  (Family #{m.FamilyID})"
                : "HOUSEHOLD: not yet registered under this incident";
            senderStack.Children.Add(new TextBlock
            {
                Text       = householdLine,
                FontFamily = new FontFamily("Consolas"), FontSize = 9,
                Foreground = new SolidColorBrush(m.FamilyID > 0
                    ? Color.FromArgb(255, 122, 155, 184)
                    : Color.FromArgb(255, 100, 80, 50))
            });
            headerGrid.Children.Add(senderStack);

            var statusBadge = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment   = VerticalAlignment.Top,
                Padding             = new Thickness(8, 3, 8, 3),
                Background          = new SolidColorBrush(Color.FromArgb(40, accentColor.R, accentColor.G, accentColor.B)),
                Child = new TextBlock
                {
                    Text       = m.Status.ToUpper(),
                    FontFamily = new FontFamily("Consolas"), FontSize = 8,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(accentColor)
                }
            };
            headerGrid.Children.Add(statusBadge);
            stack.Children.Add(headerGrid);

            stack.Children.Add(new TextBlock
            {
                Text       = m.SentAt,
                FontFamily = new FontFamily("Consolas"), FontSize = 8,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 58, 85, 112))
            });

            stack.Children.Add(new Border
            {
                Background      = new SolidColorBrush(Color.FromArgb(255, 12, 26, 38)),
                BorderThickness = new Thickness(2, 0, 0, 0),
                BorderBrush     = new SolidColorBrush(accentColor),
                Padding         = new Thickness(12, 10, 12, 10),
                Child = new TextBlock
                {
                    Text         = m.Message,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily   = new FontFamily("Consolas"), FontSize = 11,
                    Foreground   = new SolidColorBrush(Color.FromArgb(255, 200, 216, 232)),
                    LineHeight   = 18
                }
            });

            if (m.Status == "Rejected" && !string.IsNullOrWhiteSpace(m.RejectionReason))
            {
                stack.Children.Add(new Border
                {
                    Background  = new SolidColorBrush(Color.FromArgb(255, 28, 12, 12)),
                    Padding     = new Thickness(12, 8, 12, 8),
                    Child = new StackPanel { Spacing = 2, Children =
                    {
                        new TextBlock { Text = "REJECTION REASON", FontFamily = new FontFamily("Consolas"),
                            FontSize = 8, Foreground = new SolidColorBrush(Red),
                            FontWeight = Microsoft.UI.Text.FontWeights.Bold, CharacterSpacing = 150 },
                        new TextBlock { Text = m.RejectionReason, FontFamily = new FontFamily("Consolas"),
                            FontSize = 10, Foreground = new SolidColorBrush(Color.FromArgb(255, 200, 140, 140)),
                            TextWrapping = TextWrapping.Wrap }
                    }}
                });
            }

            if (m.Status == "Pending")
            {
                var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

                var approveBtn = new Button { Style = (Style)Resources["PrimaryBtnStyle"] };
                approveBtn.Content = new TextBlock { Text = "APPROVE",
                    FontFamily = new FontFamily("Consolas"), FontSize = 9,
                    Foreground = new SolidColorBrush(White),
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold, CharacterSpacing = 60 };
                approveBtn.Click += (s, _) => ApproveMessage(m);
                btnRow.Children.Add(approveBtn);

                var rejectBtn = new Button { Style = (Style)Resources["GhostBtnStyle"] };
                rejectBtn.Content = new TextBlock { Text = "REJECT",
                    FontFamily = new FontFamily("Consolas"), FontSize = 9,
                    Foreground = new SolidColorBrush(Red),
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold, CharacterSpacing = 60 };
                rejectBtn.Click += (s, _) => OpenRejectReasonDialog(m);
                btnRow.Children.Add(rejectBtn);

                var deleteBtn = new Button { Style = (Style)Resources["GhostBtnStyle"] };
                deleteBtn.Content = new TextBlock { Text = "DELETE",
                    FontFamily = new FontFamily("Consolas"), FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 122, 155, 184)),
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold, CharacterSpacing = 60 };
                deleteBtn.Click += (s, _) => DeleteMessage(m, card);
                btnRow.Children.Add(deleteBtn);

                stack.Children.Add(btnRow);
            }
            else
            {
                var deleteBtn = new Button { Style = (Style)Resources["GhostBtnStyle"] };
                deleteBtn.Content = new TextBlock { Text = "DELETE",
                    FontFamily = new FontFamily("Consolas"), FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 122, 155, 184)),
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold, CharacterSpacing = 60 };
                deleteBtn.Click += (s, _) => DeleteMessage(m, card);
                stack.Children.Add(deleteBtn);
            }

            card.Child = stack;
            return card;
        }

        private void RefreshOverlayCard(int messageId)
        {
            var msgs = _dswdMsgRepo.GetAllForInbox();
            int pending = msgs.Count(m => m.Status == "Pending");
            CitizenMsgOverlaySubtitle.Text = pending > 0
                ? $"{msgs.Count} MESSAGE{(msgs.Count != 1 ? "S" : "")} · {pending} UNREAD"
                : $"{msgs.Count} MESSAGE{(msgs.Count != 1 ? "S" : "")} RECEIVED";

            CitizenMsgOverlayPanel.Children.Clear();
            foreach (var m in msgs)
                CitizenMsgOverlayPanel.Children.Add(BuildMessageCard(m));
        }

        private void ApproveMessage(DSWDMessage m)
        {
            try
            {
                _dswdMsgRepo.Approve(m.MessageID);
                _auditRepo.Log(SessionManager.UserID, "UPDATE", "DSWD_Messages", m.MessageID,
                    $"Approved message from {m.SenderName}");
                RefreshOverlayCard(m.MessageID);
                UpdateMessagesButton();
            }
            catch (Exception ex)
            {
                ShowToast($"Could not approve message: {ex.Message}");
            }
        }

        private void RejectMessage(DSWDMessage m, string reason)
        {
            try
            {
                _dswdMsgRepo.Reject(m.MessageID, reason);
                _auditRepo.Log(SessionManager.UserID, "UPDATE", "DSWD_Messages", m.MessageID,
                    $"Rejected message from {m.SenderName}: {reason}");
                RefreshOverlayCard(m.MessageID);
                UpdateMessagesButton();
            }
            catch (Exception ex)
            {
                ShowToast($"Could not reject message: {ex.Message}");
            }
        }

        private void DeleteMessage(DSWDMessage m, Border card)
        {
            try
            {
                _dswdMsgRepo.HideFromDswdInbox(m.MessageID);
                _auditRepo.Log(SessionManager.UserID, "DELETE", "DSWD_Messages", m.MessageID,
                    $"Removed message from DSWD inbox: {m.SenderName}");
                if (card.Parent is StackPanel parent)
                    parent.Children.Remove(card);
                UpdateMessagesButton();
            }
            catch (Exception ex)
            {
                ShowToast($"Could not delete message: {ex.Message}");
            }
        }

        private void OpenRejectReasonDialog(DSWDMessage m)
        {
            CitizenMsgOverlayPanel.Children.Clear();
            var msgs = _dswdMsgRepo.GetAllForInbox();

            foreach (var msg in msgs)
            {
                if (msg.MessageID == m.MessageID)
                {
                    CitizenMsgOverlayPanel.Children.Add(BuildMessageCardWithRejectForm(msg));
                }
                else
                {
                    CitizenMsgOverlayPanel.Children.Add(BuildMessageCard(msg));
                }
            }
        }

        private Border BuildMessageCardWithRejectForm(DSWDMessage m)
        {
            var card = new Border
            {
                Background      = new SolidColorBrush(Color.FromArgb(255, 9, 19, 28)),
                BorderThickness = new Thickness(3, 0, 0, 0),
                BorderBrush     = new SolidColorBrush(Red),
                Padding         = new Thickness(16, 14, 16, 14),
                Margin          = new Thickness(0, 0, 0, 1)
            };
            var stack = new StackPanel { Spacing = 10 };

            stack.Children.Add(new TextBlock
            {
                Text = m.SenderName, FontFamily = new FontFamily("Consolas"), FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(White)
            });
            stack.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 12, 26, 38)),
                BorderThickness = new Thickness(2, 0, 0, 0), BorderBrush = new SolidColorBrush(Red),
                Padding = new Thickness(12, 10, 12, 10),
                Child = new TextBlock { Text = m.Message, TextWrapping = TextWrapping.Wrap,
                    FontFamily = new FontFamily("Consolas"), FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 200, 216, 232)) }
            });

            var reasonBox = new TextBox
            {
                PlaceholderText = "Enter rejection reason...",
                FontFamily = new FontFamily("Consolas"), FontSize = 10,
                Background = new SolidColorBrush(Color.FromArgb(255, 14, 8, 8)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 200, 160, 160)),
                BorderBrush = new SolidColorBrush(Red), CornerRadius = new CornerRadius(0),
                Padding = new Thickness(10, 8, 10, 8)
            };
            stack.Children.Add(reasonBox);

            var actionRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

            var confirmBtn = new Button { Style = (Style)Resources["PrimaryBtnStyle"] };
            confirmBtn.Background = new SolidColorBrush(Color.FromArgb(255, 50, 14, 14));
            confirmBtn.Content = new TextBlock { Text = "CONFIRM REJECT",
                FontFamily = new FontFamily("Consolas"), FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 230, 120, 120)),
                FontWeight = Microsoft.UI.Text.FontWeights.Bold, CharacterSpacing = 60 };
            confirmBtn.Click += (s, _) =>
            {
                string reason = reasonBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(reason)) reason = "Rejected by DSWD coordinator";
                RejectMessage(m, reason);
            };
            actionRow.Children.Add(confirmBtn);

            var cancelBtn = new Button { Style = (Style)Resources["GhostBtnStyle"] };
            cancelBtn.Content = new TextBlock { Text = "CANCEL",
                FontFamily = new FontFamily("Consolas"), FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 122, 155, 184)), CharacterSpacing = 60 };
            cancelBtn.Click += (s, _) => RefreshOverlayCard(m.MessageID);
            actionRow.Children.Add(cancelBtn);

            stack.Children.Add(actionRow);
            card.Child = stack;
            return card;
        }


        private void BuildOverview()
        {
            StatFamiliesServed.Text     = _reliefRepo.CountFamiliesServed().ToString();
            StatTotalDistributions.Text = _reliefRepo.CountTotalDistributions().ToString();
            StatUnserved.Text           = _reliefRepo.CountUnservedFamilies().ToString();
            StatDuplicates.Text         = _reliefRepo.CountPossibleDuplicates().ToString();

            BuildLedgerRows(OverviewLedgerPanel, _reliefRepo.GetLedger(10), compact: true);
            BuildPendingPanel(OverviewPendingPanel);
            UpdateMessagesButton();
        }

        private void BuildPendingPanel(StackPanel panel)
        {
            panel.Children.Clear();
            var pending = _incidents.Where(i => i.DSDWStatus == "Pending").ToList();
            var all     = _incidents.OrderByDescending(i => i.IncidentID).ToList();

            if (all.Count == 0)
            {
                panel.Children.Add(MakeEmptyState("No incidents recorded", "",
                    new Thickness(16, 12, 16, 12)));
                return;
            }

            const int CollapseAt = 4;
            bool collapsed = all.Count > CollapseAt;
            var visible    = collapsed ? all.Take(CollapseAt).ToList() : all;

            foreach (var inc in visible)
                panel.Children.Add(MakeIncidentPill(inc));

            if (collapsed)
            {
                var toggleBtn = new Button
                {
                    Background      = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                    BorderThickness = new Thickness(0),
                    Padding         = new Thickness(12, 6, 12, 6),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                var toggleText = new TextBlock
                {
                    Text             = $"v  SHOW ALL {all.Count} INCIDENTS",
                    FontFamily       = new FontFamily("Consolas"),
                    FontSize         = 8,
                    Foreground       = new SolidColorBrush(Color.FromArgb(255, 58, 85, 112)),
                    CharacterSpacing = 100,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                toggleBtn.Content = toggleText;

                bool isExpanded = false;
                toggleBtn.Click += (_, _) =>
                {
                    isExpanded = !isExpanded;
                    while (panel.Children.Count > 1)
                        panel.Children.RemoveAt(0);

                    var toShow = isExpanded ? all : all.Take(CollapseAt).ToList();
                    int insertIdx = 0;
                    foreach (var inc in toShow)
                    {
                        panel.Children.Insert(insertIdx, MakeIncidentPill(inc));
                        insertIdx++;
                    }
                    toggleText.Text = isExpanded
                        ? "^  SHOW FEWER"
                        : $"v  SHOW ALL {all.Count} INCIDENTS";
                };

                panel.Children.Add(toggleBtn);
            }
        }

        private Border MakeIncidentPill(Incident inc)
        {
            bool isPending = inc.DSDWStatus == "Pending";
            var pill = new Border
            {
                Background      = new SolidColorBrush(Color.FromArgb(255, 9, 19, 28)),
                BorderThickness = new Thickness(1, 0, 0, 0),
                BorderBrush     = new SolidColorBrush(isPending
                    ? Color.FromArgb(255, 192, 80, 80)
                    : Color.FromArgb(255, 30, 50, 70)),
                Padding = new Thickness(12, 9, 12, 9),
                Margin  = new Thickness(12, 0, 12, 4)
            };
            var sp = new StackPanel { Spacing = 3 };
            sp.Children.Add(new TextBlock
            {
                Text = $"Incident #{inc.IncidentID} -- Brgy. {inc.Barangay}",
                FontFamily = new FontFamily("Consolas"), FontSize = 10,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(White)
            });
            sp.Children.Add(new TextBlock
            {
                Text = $"Level {inc.AlarmLevel}  |  {(inc.DateTime.Length >= 10 ? inc.DateTime[..10] : inc.DateTime)}  |  DSWD: {inc.DSDWStatus}",
                FontFamily = new FontFamily("Consolas"), FontSize = 9,
                Foreground = new SolidColorBrush(isPending
                    ? Color.FromArgb(255, 192, 80, 80)
                    : Color.FromArgb(255, 58, 85, 112))
            });
            pill.Child = sp;
            return pill;
        }


        private void BuildDistributionSection()
        {
            _fullLedger = _reliefRepo.GetLedger(200);
            LedgerSearchBox.Text = "";
            FilterAndRenderLedger("");
        }

        private void FilterAndRenderLedger(string query)
        {
            var filtered = string.IsNullOrWhiteSpace(query)
                ? _fullLedger
                : _fullLedger.Where(r =>
                    r.HeadName.ToLower().Contains(query.ToLower()) ||
                    r.ItemType.ToLower().Contains(query.ToLower()) ||
                    r.Agency.ToLower().Contains(query.ToLower())).ToList();

            DistributionSubtitle.Text = string.IsNullOrWhiteSpace(query)
                ? $"{_fullLedger.Count} records total"
                : $"{filtered.Count} of {_fullLedger.Count} records";

            BuildLedgerRows(FullLedgerPanel, filtered, compact: false);
        }

        private void LedgerSearchBox_TextChanged(object sender, TextChangedEventArgs e)
            => FilterAndRenderLedger(LedgerSearchBox.Text.Trim());

        private void BuildLedgerRows(
            StackPanel panel,
            List<(int RecordID, string HeadName, string Agency, string ItemType, int Qty, string Date)> rows,
            bool compact)
        {
            panel.Children.Clear();
            if (rows.Count == 0)
            {
                panel.Children.Add(MakeEmptyState("No distributions recorded yet",
                    "Record a distribution to begin tracking relief",
                    new Thickness(18, 28, 18, 28)));
                return;
            }

            foreach (var (_, head, agency, item, qty, date) in rows)
            {
                var row = new Border
                {
                    Padding = new Thickness(18, 10, 18, 10),
                    BorderThickness = new Thickness(0, 1, 0, 0),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 17, 29, 42))
                };

                var grid = new Grid { ColumnSpacing = 8 };

                if (compact)
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
                }
                else
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
                }

                var nameBlock = new TextBlock { Text = head, FontFamily = new FontFamily("Consolas"),
                    FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(White), VerticalAlignment = VerticalAlignment.Center };
                var itemBlock = new TextBlock { Text = item, FontFamily = new FontFamily("Consolas"),
                    FontSize = 10, Foreground = new SolidColorBrush(Green),
                    VerticalAlignment = VerticalAlignment.Center };
                var qtyBlock = new TextBlock { Text = qty.ToString(), FontFamily = new FontFamily("Consolas"),
                    FontSize = 11, Foreground = new SolidColorBrush(Color.FromArgb(255, 122, 155, 184)),
                    VerticalAlignment = VerticalAlignment.Center };
                var dateBlock = new TextBlock
                {
                    Text = date.Length >= 10 ? date[..10] : date,
                    FontFamily = new FontFamily("Consolas"), FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 58, 85, 112)),
                    VerticalAlignment = VerticalAlignment.Center
                };

                Grid.SetColumn(nameBlock, 0); grid.Children.Add(nameBlock);
                Grid.SetColumn(itemBlock, 1); grid.Children.Add(itemBlock);
                Grid.SetColumn(qtyBlock,  2); grid.Children.Add(qtyBlock);

                if (!compact)
                {
                    var agencyBlock = new TextBlock { Text = agency, FontFamily = new FontFamily("Consolas"),
                        FontSize = 10, Foreground = new SolidColorBrush(Color.FromArgb(255, 74, 107, 138)),
                        VerticalAlignment = VerticalAlignment.Center };
                    Grid.SetColumn(agencyBlock, 3); grid.Children.Add(agencyBlock);
                    Grid.SetColumn(dateBlock,   4); grid.Children.Add(dateBlock);
                }
                else
                {
                    Grid.SetColumn(dateBlock, 3); grid.Children.Add(dateBlock);
                }

                row.Child = grid;
                panel.Children.Add(row);
            }
        }


        private void BuildGapsSection()
        {
            var unserved = _reliefRepo.GetUnservedFamilies();
            UnservedSubtitle.Text = $"{unserved.Count} families with no relief record";
            UnservedPanel.Children.Clear();

            if (unserved.Count == 0)
                UnservedPanel.Children.Add(MakeEmptyState("All families have received relief", "",
                    new Thickness(18, 20, 18, 20)));
            else
                foreach (var (_, head, members, barangay, _) in unserved)
                {
                    var row = new Border
                    {
                        Padding = new Thickness(18, 10, 18, 10),
                        BorderThickness = new Thickness(0, 1, 0, 0),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(255, 17, 29, 42))
                    };
                    var grid = new Grid { ColumnSpacing = 8 };
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });

                    var nameBlock = new TextBlock { Text = head, FontFamily = new FontFamily("Consolas"),
                        FontSize = 10, FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        Foreground = new SolidColorBrush(White), VerticalAlignment = VerticalAlignment.Center };
                    var membersBlock = new TextBlock { Text = members.ToString(), FontFamily = new FontFamily("Consolas"),
                        FontSize = 10, Foreground = new SolidColorBrush(Color.FromArgb(255, 122, 155, 184)),
                        VerticalAlignment = VerticalAlignment.Center };
                    var brgyBlock = new TextBlock { Text = barangay, FontFamily = new FontFamily("Consolas"),
                        FontSize = 9, Foreground = new SolidColorBrush(Yellow),
                        VerticalAlignment = VerticalAlignment.Center };

                    Grid.SetColumn(nameBlock, 0);    grid.Children.Add(nameBlock);
                    Grid.SetColumn(membersBlock, 1); grid.Children.Add(membersBlock);
                    Grid.SetColumn(brgyBlock, 2);    grid.Children.Add(brgyBlock);

                    row.Child = grid;
                    UnservedPanel.Children.Add(row);
                }

            var dups = _reliefRepo.GetDuplicateFamilies();
            DuplicatesSubtitle.Text = $"{dups.Count} possible duplicate registrations";
            DuplicatesPanel.Children.Clear();

            if (dups.Count == 0)
                DuplicatesPanel.Children.Add(MakeEmptyState("No duplicates detected", "",
                    new Thickness(18, 20, 18, 20)));
            else
                foreach (var (headName, incCount, totalMembers) in dups)
                {
                    var row = new Border
                    {
                        Padding = new Thickness(18, 10, 18, 10),
                        BorderThickness = new Thickness(0, 1, 0, 0),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(255, 17, 29, 42))
                    };
                    var grid = new Grid { ColumnSpacing = 8 };
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });

                    var nameBlock = new TextBlock { Text = headName, FontFamily = new FontFamily("Consolas"),
                        FontSize = 10, FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        Foreground = new SolidColorBrush(Red), VerticalAlignment = VerticalAlignment.Center };
                    var incBlock = new TextBlock { Text = $"{incCount}x", FontFamily = new FontFamily("Consolas"),
                        FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        Foreground = new SolidColorBrush(Red), VerticalAlignment = VerticalAlignment.Center };
                    var membersBlock = new TextBlock { Text = totalMembers.ToString(), FontFamily = new FontFamily("Consolas"),
                        FontSize = 10, Foreground = new SolidColorBrush(Color.FromArgb(255, 122, 155, 184)),
                        VerticalAlignment = VerticalAlignment.Center };

                    Grid.SetColumn(nameBlock, 0);    grid.Children.Add(nameBlock);
                    Grid.SetColumn(incBlock, 1);     grid.Children.Add(incBlock);
                    Grid.SetColumn(membersBlock, 2); grid.Children.Add(membersBlock);

                    row.Child = grid;
                    DuplicatesPanel.Children.Add(row);
                }
        }


        private void BuildIncidentsSection(string query = "")
        {
            var source = string.IsNullOrWhiteSpace(query)
                ? _incidents
                : _incidents.Where(i =>
                    $"#{i.IncidentID}".Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    i.Barangay.Contains(query, StringComparison.OrdinalIgnoreCase)         ||
                    i.Sitio.Contains(query, StringComparison.OrdinalIgnoreCase)             ||
                    i.DSDWStatus.Contains(query, StringComparison.OrdinalIgnoreCase)        ||
                    i.Status.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

            IncidentsSubtitle.Text = string.IsNullOrWhiteSpace(query)
                ? $"{_incidents.Count} total incidents"
                : $"{source.Count} of {_incidents.Count} incidents";

            IncidentsPanel.Children.Clear();

            if (source.Count == 0)
            {
                IncidentsPanel.Children.Add(MakeEmptyState(
                    string.IsNullOrWhiteSpace(query) ? "No incidents recorded" : $"No results for \"{query}\"",
                    "", new Thickness(20, 32, 20, 32)));
                return;
            }

            foreach (var inc in source)
            {
                var dswd      = inc.DSDWStatus;
                var dswdColor = dswd == "Completed"  ? Green
                              : dswd == "Responding" ? Color.FromArgb(255, 74, 142, 194)
                              :                        Color.FromArgb(255, 192, 154, 48);
                var dswdBg    = dswd == "Completed"  ? Color.FromArgb(255, 10, 30, 15)
                              : dswd == "Responding" ? Color.FromArgb(255, 10, 20, 42)
                              :                        Color.FromArgb(255, 30, 25, 10);

                var card = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(255, 12, 26, 38)),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 23, 36, 53)),
                    Padding = new Thickness(20, 14, 20, 14)
                };
                var sp = new StackPanel { Spacing = 10 };

                var hdr = new Grid();
                var left = new StackPanel { Spacing = 4 };
                left.Children.Add(new TextBlock
                {
                    Text = $"INCIDENT #{inc.IncidentID}  ·  Brgy. {inc.Barangay}",
                    FontFamily = new FontFamily("Consolas"), FontSize = 12,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(White)
                });
                left.Children.Add(new TextBlock
                {
                    Text = $"Alarm Level {inc.AlarmLevel}  ·  {inc.Sitio}  ·  {(inc.DateTime.Length >= 10 ? inc.DateTime[..10] : inc.DateTime)}",
                    FontFamily = new FontFamily("Consolas"), FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 58, 85, 112))
                });
                hdr.Children.Add(left);

                var right = new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };
                right.Children.Add(new Border
                {
                    Background = new SolidColorBrush(dswdBg),
                    Padding = new Thickness(10, 4, 10, 4),
                    Child = new TextBlock
                    {
                        Text = dswd.ToUpper(), FontFamily = new FontFamily("Consolas"),
                        FontSize = 8, FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        Foreground = new SolidColorBrush(dswdColor)
                    }
                });

                var updateBtn = new Button { Style = (Style)Resources["PrimaryBtnStyle"], Tag = inc };
                updateBtn.Content = new TextBlock
                {
                    Text = "UPDATE STATUS", FontFamily = new FontFamily("Consolas"),
                    FontSize = 8, Foreground = new SolidColorBrush(White),
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold, CharacterSpacing = 60
                };
                updateBtn.Click += (s, _) => OpenDSDWStatusDialog((Incident)((Button)s).Tag!);
                right.Children.Add(updateBtn);
                hdr.Children.Add(right);
                sp.Children.Add(hdr);

                var families = _familyRepo.GetByIncident(inc.IncidentID);
                if (families.Count > 0)
                {
                    var pills = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
                    pills.Children.Add(MakePill($"{families.Count} families",
                        Color.FromArgb(255, 74, 142, 194), Color.FromArgb(255, 12, 20, 36)));
                    pills.Children.Add(MakePill($"{families.Sum(f => f.MemberCount)} persons",
                        Color.FromArgb(255, 74, 142, 194), Color.FromArgb(255, 12, 20, 36)));
                    int served = families.Count(f => f.ReliefStatus == "Received");
                    if (served > 0)
                        pills.Children.Add(MakePill($"{served} received relief",
                            Color.FromArgb(255, 76, 175, 144), Color.FromArgb(255, 10, 30, 18)));
                    sp.Children.Add(pills);
                }

                card.Child = sp;
                IncidentsPanel.Children.Add(card);
            }
        }

        private void IncidentSearchBox_TextChanged(object sender, TextChangedEventArgs e)
            => BuildIncidentsSection(IncidentSearchBox.Text.Trim());


        private void OpenRecordDistributionDialog_Click(object sender, RoutedEventArgs e)
        {
            _allIncidents            = new List<Incident>(_incidents);
            _familiesForIncident     = new();
            _distSelectedIncidentId  = 0;
            _distSelectedFamilyId    = 0;

            DistIncidentBox.Text       = "";
            DistIncidentBox.ItemsSource = null;

            DistFamilyBox.Text       = "";
            DistFamilyBox.ItemsSource = null;
            DistFamilyBox.IsEnabled  = false;

            DistItemType.Text       = "";
            DistQuantity.Text       = "";
            DistAgency.Text         = "DSWD";
            DistErrorMsg.Visibility = Visibility.Collapsed;

            RecordDistributionOverlay.Visibility = Visibility.Visible;
        }

        private void CloseRecordDistributionDialog_Click(object sender, RoutedEventArgs e)
            => RecordDistributionOverlay.Visibility = Visibility.Collapsed;

        private void DistIncidentBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;

            string q = sender.Text.Trim().ToLower();
            _distSelectedIncidentId = 0;

            DistFamilyBox.Text       = "";
            DistFamilyBox.ItemsSource = null;
            DistFamilyBox.IsEnabled  = false;
            _familiesForIncident     = new();
            _distSelectedFamilyId    = 0;

            var filtered = string.IsNullOrEmpty(q)
                ? _allIncidents
                : _allIncidents.Where(i =>
                    $"#{i.IncidentID}".Contains(q) ||
                    i.Barangay.ToLower().Contains(q) ||
                    i.Status.ToLower().Contains(q)).ToList();

            sender.ItemsSource = filtered.Select(i => new IncidentItem
            {
                Id      = i.IncidentID,
                Display = $"#{i.IncidentID} · Brgy. {i.Barangay} ({i.Status})"
            }).ToList();
        }

        private void DistIncidentBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is not IncidentItem item) return;

            _distSelectedIncidentId = item.Id;
            sender.Text = item.Display;

            _familiesForIncident  = _familyRepo.GetByIncident(item.Id);
            _distSelectedFamilyId = 0;
            DistFamilyBox.Text       = "";
            DistFamilyBox.ItemsSource = null;
            DistFamilyBox.IsEnabled  = _familiesForIncident.Count > 0;
            DistFamilyBox.PlaceholderText = _familiesForIncident.Count > 0
                ? "Search family by name..."
                : "No families registered for this incident";
        }

        private void DistFamilyBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            if (_familiesForIncident.Count == 0) return;

            string q = sender.Text.Trim().ToLower();
            _distSelectedFamilyId = 0;

            var filtered = string.IsNullOrEmpty(q)
                ? _familiesForIncident
                : _familiesForIncident.Where(f =>
                    f.HeadName.ToLower().Contains(q)).ToList();

            sender.ItemsSource = filtered.Select(f => new FamilyItem
            {
                Id      = f.FamilyID,
                Display = $"{f.HeadName} ({f.MemberCount} members)"
            }).ToList();
        }

        private void DistFamilyBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is not FamilyItem item) return;
            _distSelectedFamilyId = item.Id;
            sender.Text = item.Display;
        }

        private void SubmitDistribution_Click(object sender, RoutedEventArgs e)
        {
            int familyId = _distSelectedFamilyId;
            string item  = DistItemType.Text.Trim();
            string qtyStr= DistQuantity.Text.Trim();
            string agency= DistAgency.Text.Trim();

            if (familyId == 0)              { ShowError(DistErrorMsg, "Please select a family."); return; }
            if (string.IsNullOrWhiteSpace(item))  { ShowError(DistErrorMsg, "Item type is required."); return; }
            if (!int.TryParse(qtyStr, out int qty) || qty < 1)
                { ShowError(DistErrorMsg, "Quantity must be a positive number."); return; }
            if (string.IsNullOrWhiteSpace(agency)) { ShowError(DistErrorMsg, "Agency name is required."); return; }

            DistErrorMsg.Visibility = Visibility.Collapsed;

            try
            {
            var rec = new ReliefRecord
            {
                FamilyID        = familyId,
                AgencyName      = agency,
                ItemType        = item,
                Quantity        = qty,
                DateDistributed = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                DistributedBy   = SessionManager.UserID
            };
            int newId = _reliefRepo.Create(rec);

            _auditRepo.Log(SessionManager.UserID, "CREATE", "Relief_Records", newId,
                $"Distributed {qty}x {item} to FamilyID {familyId} via {agency}");

            RecordDistributionOverlay.Visibility = Visibility.Collapsed;
            RefreshAllData();
            ShowSection("Distribution");
            ShowToast($"Distribution recorded — {qty}x {item}.");
            }
            catch (Exception ex)
            {
                ShowError(DistErrorMsg, $"Could not record distribution: {ex.Message}");
            }
        }

        private void OpenDSDWStatusDialog(Incident inc)
        {
            _selectedIncidentId    = inc.IncidentID;
            _selectedIncidentLabel = $"Incident #{inc.IncidentID} — Brgy. {inc.Barangay}";
            DSDWStatusIncidentLabel.Text = _selectedIncidentLabel;

            for (int i = 0; i < DSDWStatusPicker.Items.Count; i++)
                if (DSDWStatusPicker.Items[i] is ComboBoxItem ci && ci.Content?.ToString() == inc.DSDWStatus)
                    DSDWStatusPicker.SelectedIndex = i;

            DSDWStatusError.Visibility = Visibility.Collapsed;
            UpdateDSDWStatusOverlay.Visibility = Visibility.Visible;
        }

        private void CloseDSDWStatusDialog_Click(object sender, RoutedEventArgs e)
            => UpdateDSDWStatusOverlay.Visibility = Visibility.Collapsed;

        private void SubmitDSDWStatus_Click(object sender, RoutedEventArgs e)
        {
            if (DSDWStatusPicker.SelectedItem is not ComboBoxItem ci || ci.Content is null)
            { DSDWStatusError.Text = "Please select a status."; DSDWStatusError.Visibility = Visibility.Visible; return; }

            string newStatus = ci.Content.ToString()!;
            DSDWStatusError.Visibility = Visibility.Collapsed;

            try
            {
            _incidentRepo.UpdateDSDWStatus(_selectedIncidentId, newStatus);
            _auditRepo.Log(SessionManager.UserID, "UPDATE", "Incidents", _selectedIncidentId,
                $"DSWD status → {newStatus}");

            UpdateDSDWStatusOverlay.Visibility = Visibility.Collapsed;
            RefreshAllData();
            BuildIncidentsSection();
            BuildPendingPanel(OverviewPendingPanel);
            ShowToast($"DSWD status updated to \"{newStatus}\".");
            }
            catch (Exception ex)
            {
                DSDWStatusError.Text = $"Could not update DSWD status: {ex.Message}";
                DSDWStatusError.Visibility = Visibility.Visible;
            }
        }

        private void OpenUsageReportOverlay_Click(object sender, RoutedEventArgs e)
            => OpenUsageReportOverlay();

        private void OpenUsageReportOverlay()
        {
            BuildUsageReportSection();
            UsageReportOverlay.Visibility = Visibility.Visible;
        }

        private void CloseUsageReportOverlay_Click(object sender, RoutedEventArgs e)
            => UsageReportOverlay.Visibility = Visibility.Collapsed;
        private void BuildCityCentersSection()
        {
            DSDWAddCenterForm.Visibility = Visibility.Collapsed;
            DSDWOccForm.Visibility       = Visibility.Collapsed;

            if (!_dsdwMapReady)
                InitDSDWMapAsync();
            else
                DSDWMap_SendCenters();
        }

        private async void InitDSDWMapAsync()
        {
            DSDWMapLoading.Visibility = Visibility.Visible;
            DSDWMapStatus.Text = "INITIALIZING MAP...";
            await DSDWMapWebView.EnsureCoreWebView2Async();
            DSDWMapWebView.CoreWebView2.WebMessageReceived -= DSDWMap_MessageReceived;
            DSDWMapWebView.CoreWebView2.WebMessageReceived += DSDWMap_MessageReceived;
            var mapPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Map", "dswd_map.html");
            DSDWMapWebView.Source = new Uri(mapPath);
        }

        private void DSDWMap_OnReady()
        {
            _dsdwMapReady             = true;
            DSDWMapLoading.Visibility = Visibility.Collapsed;
            DSDWMapStatus.Text        = "MAP READY";
            DSDWMapWebView.CoreWebView2.PostWebMessageAsString("{\"type\":\"init_map\"}");
            DSDWMap_SendCenters();
        }

        private void DSDWMap_SendCenters()
        {
            var city = _evacRepo.GetCityLevel();

            static object ToObj(EvacuationCenter c) => new {
                id=c.CenterID, name=c.Name, barangay=c.Barangay,
                lat=c.GPSLat, lng=c.GPSLong,
                capacity=c.Capacity, occupancy=c.CurrentOccupancy, isFull=c.IsFull
            };

            DSDWMapWebView.CoreWebView2.PostWebMessageAsString(
                JsonSerializer.Serialize(new { type="load_centers", centers=city.Select(ToObj) }));
            DSDWMapStatus.Text = $"{city.Count} city-level centers";
        }

        private void DSDWMap_MessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var raw = e.TryGetWebMessageAsString();
            try
            {
                string msgType; double lat=0, lng=0; int centerId=0, capacity=0, currentOcc=0;
                string barangay = "";
                using (var doc = System.Text.Json.JsonDocument.Parse(raw))
                {
                    msgType = doc.RootElement.GetProperty("type").GetString() ?? "";
                    if (msgType == "center_pin_dropped")
                    {
                        lat = doc.RootElement.GetProperty("lat").GetDouble();
                        lng = doc.RootElement.GetProperty("lng").GetDouble();
                        if (doc.RootElement.TryGetProperty("barangay", out var brgyEl))
                            barangay = brgyEl.GetString() ?? "";
                    }
                    else if (msgType == "open_update_occupancy")
                    { centerId=doc.RootElement.GetProperty("centerId").GetInt32(); capacity=doc.RootElement.GetProperty("capacity").GetInt32(); currentOcc=doc.RootElement.GetProperty("currentOcc").GetInt32(); }
                    else if (msgType == "remove_center" || msgType == "view_usage")
                    { centerId=doc.RootElement.GetProperty("centerId").GetInt32(); }
                }
                DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                    switch (msgType)
                    {
                        case "dswd_map_ready":
                            DSDWMap_OnReady();
                            break;
                        case "center_pin_dropped":
                            _dsdwPendingLat = lat; _dsdwPendingLng = lng;
                            _dsdwPendingBarangay = barangay;
                            DSDWNewName.Text = ""; DSDWNewCapacity.Text = "";
                            DSDWDetectedBarangay.Text = string.IsNullOrWhiteSpace(barangay)
                                ? "(not detected — will save as blank)"
                                : barangay;
                            DSDWNewError.Visibility = Visibility.Collapsed;
                            DSDWAddCenterForm.Visibility = Visibility.Visible;
                            DSDWOccForm.Visibility = Visibility.Collapsed;
                            DSDWMapStatus.Text = string.IsNullOrWhiteSpace(barangay)
                                ? "Pin dropped — geocoding returned no barangay"
                                : $"Pin dropped — Brgy. {barangay}";
                            break;
                        case "open_update_occupancy":
                            _dsdwOccCenterId = centerId; _dsdwOccCapacity = capacity;
                            DSDWOccTitle.Text = "UPDATE OCCUPANCY";
                            DSDWOccHint.Text = $"Capacity: {capacity}  ·  Current: {currentOcc}";
                            DSDWOccInput.Text = currentOcc.ToString();
                            DSDWOccError.Visibility = Visibility.Collapsed;
                            DSDWOccForm.Visibility = Visibility.Visible;
                            DSDWAddCenterForm.Visibility = Visibility.Collapsed;
                            break;
                        case "remove_center":
                            var (delOk, delErr) = _evacRepo.DeleteCityCenter(centerId);
                            if (delOk)
                            {
                                _auditRepo.Log(SessionManager.UserID, "DELETE", "Evacuation_Centers", centerId, "Removed city-level center from map");
                                DSDWMap_SendCenters();
                                ShowToast("Center removed.");
                            }
                            else
                            {
                                ShowToast("Cannot remove — families still assigned.");
                                DSDWMapStatus.Text = delErr;
                            }
                            break;
                        case "view_usage":
                            OpenUsageReportOverlay();
                            break;
                    }
                    }
                    catch (Exception ex)
                    {
                        DSDWMapStatus.Text = $"Map action failed: {ex.Message}";
                        ShowToast("Map action failed.");
                    }
                });
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"DSDWMap bridge: {ex.Message}"); }
        }

        private void DSDWMap_PlaceCenter_Click(object sender, RoutedEventArgs e)
        {
            _dsdwDropActive = !_dsdwDropActive;
            DSDWAddCenterForm.Visibility = Visibility.Collapsed;
            DSDWOccForm.Visibility = Visibility.Collapsed;
            if (_dsdwMapReady)
            {
                if (_dsdwDropActive)
                {
                    DSDWMapWebView.CoreWebView2.PostWebMessageAsString("{\"type\":\"enable_drop_mode\"}");
                    DSDWMapStatus.Text = "CLICK WITHIN CEBU CITY TO PLACE CENTER  (click again to cancel)";
                }
                else
                {
                    DSDWMapWebView.CoreWebView2.PostWebMessageAsString("{\"type\":\"cancel_drop_mode\"}");
                    DSDWMapStatus.Text = "Drop mode cancelled.";
                }
            }
        }

        private void DSDWMap_ConfirmAdd_Click(object sender, RoutedEventArgs e)
        {
            string name  = DSDWNewName.Text.Trim();
            string brgy  = _dsdwPendingBarangay;
            string capStr= DSDWNewCapacity.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            { DSDWNewError.Text="Name is required."; DSDWNewError.Visibility=Visibility.Visible; return; }
            if (!int.TryParse(capStr, out int cap) || cap < 1)
            { DSDWNewError.Text="Capacity must be a positive number."; DSDWNewError.Visibility=Visibility.Visible; return; }

            DSDWNewError.Visibility = Visibility.Collapsed;

            try
            {
            var center = new EvacuationCenter {
                Name=name, Barangay=brgy,
                GPSLat=_dsdwPendingLat, GPSLong=_dsdwPendingLng,
                Capacity=cap, CurrentOccupancy=0, CenterType="City"
            };
            int newId = _evacRepo.Create(center);
            _auditRepo.Log(SessionManager.UserID, "CREATE", "Evacuation_Centers", newId,
                $"DSWD city-level center: {name}, cap {cap}, Brgy. {brgy}, ({_dsdwPendingLat:F5},{_dsdwPendingLng:F5})");

            DSDWAddCenterForm.Visibility = Visibility.Collapsed;
            DSDWMap_SendCenters();
            ShowToast($"City center \"{name}\" added.");
            DSDWMapStatus.Text = $"\"{name}\" saved — capacity {cap}";
            }
            catch (Exception ex)
            {
                DSDWNewError.Text = $"Could not add center: {ex.Message}";
                DSDWNewError.Visibility = Visibility.Visible;
            }
        }

        private void DSDWMap_CancelAdd_Click(object sender, RoutedEventArgs e)
        {
            DSDWAddCenterForm.Visibility = Visibility.Collapsed;
            if (_dsdwMapReady)
                DSDWMapWebView.CoreWebView2.PostWebMessageAsString("{\"type\":\"cancel_drop_mode\"}");
        }

        private void DSDWMap_ConfirmOcc_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(DSDWOccInput.Text.Trim(), out int occ) || occ < 0)
            { DSDWOccError.Text="Enter a valid number."; DSDWOccError.Visibility=Visibility.Visible; return; }
            if (occ > _dsdwOccCapacity)
            { DSDWOccError.Text=$"Cannot exceed capacity ({_dsdwOccCapacity})."; DSDWOccError.Visibility=Visibility.Visible; return; }

            DSDWOccError.Visibility = Visibility.Collapsed;
            try
            {
            _evacRepo.UpdateOccupancy(_dsdwOccCenterId, occ, _dsdwOccCapacity);
            _auditRepo.Log(SessionManager.UserID, "UPDATE", "Evacuation_Centers", _dsdwOccCenterId, $"Occupancy → {occ}");
            DSDWOccForm.Visibility = Visibility.Collapsed;
            DSDWMap_SendCenters();
            ShowToast("Occupancy updated.");
            DSDWMapStatus.Text = $"Occupancy updated to {occ}";
            }
            catch (Exception ex)
            {
                DSDWOccError.Text = $"Could not update occupancy: {ex.Message}";
                DSDWOccError.Visibility = Visibility.Visible;
            }
        }

        private void DSDWMap_CancelOcc_Click(object sender, RoutedEventArgs e)
            => DSDWOccForm.Visibility = Visibility.Collapsed;


        private void BuildUsageReportSection()
        {
            var centers = _evacRepo.GetCityLevel();
            UsageReportSubtitle.Text = $"{centers.Count} city-level centers — barangay usage breakdown";
            UsageReportPanel.Children.Clear();

            if (centers.Count == 0)
            {
                UsageReportPanel.Children.Add(MakeEmptyState(
                    "No city-level centers registered yet",
                    "Add centers via the City-Level Centers map",
                    new Thickness(20, 32, 20, 32)));
                return;
            }

            foreach (var c in centers)
            {
                double pct      = c.Capacity > 0 ? (double)c.CurrentOccupancy / c.Capacity : 0;
                var    fillColor= pct >= 1.0 ? Red : pct >= 0.8 ? Yellow : Green;

                var card = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(255, 12, 26, 38)),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 23, 36, 53)),
                    Padding = new Thickness(20, 16, 20, 16)
                };
                var outer = new StackPanel { Spacing = 10 };

                var hdr = new Grid();
                var left = new StackPanel { Spacing = 3 };
                left.Children.Add(new TextBlock {
                    Text=c.Name, FontFamily=new FontFamily("Consolas"), FontSize=12,
                    FontWeight=Microsoft.UI.Text.FontWeights.Bold,
                    Foreground=new SolidColorBrush(White) });
                left.Children.Add(new TextBlock {
                    Text=$"Brgy. {c.Barangay}  ·  Capacity: {c.Capacity}  ·  Occupied: {c.CurrentOccupancy}",
                    FontFamily=new FontFamily("Consolas"), FontSize=9,
                    Foreground=new SolidColorBrush(Color.FromArgb(255, 58, 85, 112)) });
                hdr.Children.Add(left);

                hdr.Children.Add(new Border {
                    HorizontalAlignment=HorizontalAlignment.Right, VerticalAlignment=VerticalAlignment.Center,
                    Padding=new Thickness(10,4,10,4),
                    Background=new SolidColorBrush(Color.FromArgb(255, 12, 26, 38)),
                    BorderThickness=new Thickness(1), BorderBrush=new SolidColorBrush(fillColor),
                    Child=new TextBlock {
                        Text=$"{(int)(pct*100)}% UTILIZED",
                        FontFamily=new FontFamily("Consolas"), FontSize=9,
                        FontWeight=Microsoft.UI.Text.FontWeights.Bold,
                        Foreground=new SolidColorBrush(fillColor) }
                });
                outer.Children.Add(hdr);

                var barGrid = new Grid { Height=5 };
                barGrid.Children.Add(new Border { Background=new SolidColorBrush(Color.FromArgb(255, 23, 36, 53)) });
                var fill = new Border { Background=new SolidColorBrush(fillColor), HorizontalAlignment=HorizontalAlignment.Left, Tag=pct };
                barGrid.Children.Add(fill);
                barGrid.SizeChanged += (s, _) => {
                    if (((Grid)s).Children[1] is Border f)
                        f.Width = Math.Max(0, Math.Min(1, f.Tag is double d ? d : 0)) * ((Grid)s).ActualWidth;
                };
                outer.Children.Add(barGrid);

                var usage = _evacRepo.GetCenterUsageReport(c.CenterID);
                if (usage.Count == 0)
                {
                    outer.Children.Add(new TextBlock {
                        Text="No barangays currently using this center",
                        FontFamily=new FontFamily("Consolas"), FontSize=9,
                        Foreground=new SolidColorBrush(Color.FromArgb(255, 42, 63, 82)) });
                }
                else
                {
                    var tbl = new StackPanel { Spacing = 0 };

                    var thead = new Grid { Padding=new Thickness(0,4,0,4) };
                    thead.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(1, GridUnitType.Star) });
                    thead.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(80) });
                    thead.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(80) });
                    var h0=new TextBlock{Text="BARANGAY",FontFamily=new FontFamily("Consolas"),FontSize=8,Foreground=new SolidColorBrush(Color.FromArgb(255,42,63,82)),CharacterSpacing=150};
                    var h1=new TextBlock{Text="FAMILIES",FontFamily=new FontFamily("Consolas"),FontSize=8,Foreground=new SolidColorBrush(Color.FromArgb(255,42,63,82)),CharacterSpacing=150};
                    var h2=new TextBlock{Text="PERSONS",FontFamily=new FontFamily("Consolas"),FontSize=8,Foreground=new SolidColorBrush(Color.FromArgb(255,42,63,82)),CharacterSpacing=150};
                    Grid.SetColumn(h0,0); Grid.SetColumn(h1,1); Grid.SetColumn(h2,2);
                    thead.Children.Add(h0); thead.Children.Add(h1); thead.Children.Add(h2);
                    tbl.Children.Add(thead);

                    foreach (var (brgy, families, persons) in usage)
                    {
                        var row = new Grid { Padding=new Thickness(0,5,0,5) };
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(1, GridUnitType.Star) });
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(80) });
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(80) });

                        var c0=new TextBlock{Text=brgy,FontFamily=new FontFamily("Consolas"),FontSize=10,FontWeight=Microsoft.UI.Text.FontWeights.Bold,Foreground=new SolidColorBrush(White),VerticalAlignment=VerticalAlignment.Center};
                        var c1=new TextBlock{Text=families.ToString(),FontFamily=new FontFamily("Consolas"),FontSize=11,FontWeight=Microsoft.UI.Text.FontWeights.Bold,Foreground=new SolidColorBrush(Green),VerticalAlignment=VerticalAlignment.Center};
                        var c2=new TextBlock{Text=persons.ToString(),FontFamily=new FontFamily("Consolas"),FontSize=11,Foreground=new SolidColorBrush(Color.FromArgb(255,122,155,184)),VerticalAlignment=VerticalAlignment.Center};
                        Grid.SetColumn(c0,0); Grid.SetColumn(c1,1); Grid.SetColumn(c2,2);
                        row.Children.Add(c0); row.Children.Add(c1); row.Children.Add(c2);
                        tbl.Children.Add(new Border {
                            BorderThickness=new Thickness(0,1,0,0),
                            BorderBrush=new SolidColorBrush(Color.FromArgb(255,17,29,42)),
                            Child=row });
                    }
                    outer.Children.Add(tbl);
                }

                card.Child = outer;
                UsageReportPanel.Children.Add(card);
            }
        }

        private void ShowToast(string message)
        {
            ToastMessage.Text      = message;
            ToastBanner.Visibility = Visibility.Visible;
            _toastTimer?.Stop();
            _toastTimer?.Start();
        }

        private static void ShowError(TextBlock block, string msg)
        {
            block.Text       = msg;
            block.Visibility = Visibility.Visible;
        }

        private static UIElement MakeEmptyState(string main, string sub, Thickness padding)
        {
            var border = new Border { Padding = padding };
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Spacing = 6 };
            sp.Children.Add(new TextBlock
            {
                Text = main, FontFamily = new FontFamily("Consolas"), FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 58, 85, 112)),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            if (!string.IsNullOrWhiteSpace(sub))
                sp.Children.Add(new TextBlock
                {
                    Text = sub, FontFamily = new FontFamily("Consolas"), FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 30, 48, 66)),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
            border.Child = sp;
            return border;
        }

        private static Border MakePill(string text, Color fg, Color bg) => new()
        {
            Background = new SolidColorBrush(bg), CornerRadius = new CornerRadius(0),
            Padding = new Thickness(10, 4, 10, 4),
            Child = new TextBlock
            {
                Text = text, FontFamily = new FontFamily("Consolas"),
                FontSize = 9, Foreground = new SolidColorBrush(fg),
                FontWeight = Microsoft.UI.Text.FontWeights.Bold
            }
        };


        private async void InitAnalysisWebViewAsync()
        {
            await AnalysisWebView.EnsureCoreWebView2Async();
            AnalysisWebView.CoreWebView2.Settings.IsWebMessageEnabled = false;
            _analysisWebViewReady = true;
        }

        private void BuildAnalysisSection()
        {
            if (!_analysisWebViewReady) return;

            AnalysisTimestamp.Text = $"UPDATED {DateTime.Now:HH:mm:ss}";

            int familiesServed     = _reliefRepo.CountFamiliesServed();
            int totalDistributions = _reliefRepo.CountTotalDistributions();
            int unservedFamilies   = _reliefRepo.CountUnservedFamilies();
            var centers            = _evacRepo.GetCityLevel();
            var brgyFamilies       = _familyRepo.GetFamilyCountByBarangay();
            var allRelief          = _reliefRepo.GetAll();

            int totalFamilies = familiesServed + unservedFamilies;
            double coveragePct = totalFamilies > 0 ? (double)familiesServed / totalFamilies * 100 : 0;

            int covServed   = familiesServed;
            int covUnserved = unservedFamilies;

            int dPending    = _incidents.Count(i => i.DSDWStatus == "Pending");
            int dResponding = _incidents.Count(i => i.DSDWStatus == "Responding");
            int dCompleted  = _incidents.Count(i => i.DSDWStatus == "Completed");

            int[] alarmCounts = new[]
            {
                _incidents.Count(i => i.AlarmLevel == 1),
                _incidents.Count(i => i.AlarmLevel == 2),
                _incidents.Count(i => i.AlarmLevel == 3),
                _incidents.Count(i => i.AlarmLevel == 4)
            };

            var now = DateTime.Now;
            var monthLabels = new List<string>();
            var monthData   = new List<int>();
            for (int m = 11; m >= 0; m--)
            {
                var d = now.AddMonths(-m);
                monthLabels.Add(d.ToString("MMM yy"));
                monthData.Add(allRelief.Count(r =>
                    DateTime.TryParse(r.DateDistributed, out var dt) &&
                    dt.Year == d.Year && dt.Month == d.Month));
            }

            var top10 = brgyFamilies.Take(10).ToList();

            var centerNames = centers.Select(c => c.Name).ToList();
            var centerPcts  = centers.Select(c =>
                c.Capacity > 0 ? Math.Round((double)c.CurrentOccupancy / c.Capacity * 100, 1) : 0.0).ToList();

            string mlJs = JsonSerializer.Serialize(monthLabels);
            string mdJs = JsonSerializer.Serialize(monthData);
            string blJs = JsonSerializer.Serialize(top10.Select(x => x.Barangay).ToArray());
            string bdJs = JsonSerializer.Serialize(top10.Select(x => x.Families).ToArray());
            string cnJs = JsonSerializer.Serialize(centerNames);
            string cpJs = JsonSerializer.Serialize(centerPcts);

            AnalysisWebView.NavigateToString(BuildAnalysisHtml(
                familiesServed, totalDistributions, coveragePct, centers.Count,
                covServed, covUnserved,
                dPending, dResponding, dCompleted,
                alarmCounts,
                mlJs, mdJs, blJs, bdJs, cnJs, cpJs));
        }

        private static string BuildAnalysisHtml(
            int familiesServed, int totalDistributions, double coveragePct, int centerCount,
            int covServed, int covUnserved,
            int dPending, int dResponding, int dCompleted,
            int[] alarmCounts,
            string mlJs, string mdJs,
            string blJs, string bdJs,
            string cnJs, string cpJs)
        {
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html lang='en'><head><meta charset='UTF-8'/><style>");
            sb.Append("*{margin:0;padding:0;box-sizing:border-box;}");
            sb.Append("body{background:#09131C;color:#C8D8E8;font-family:Consolas,monospace;padding:16px;overflow-y:auto;}");
            sb.Append(".sr{display:flex;gap:10px;margin-bottom:12px;}");
            sb.Append(".st{flex:1;background:#0C1A26;border:1px solid #172435;padding:12px;}");
            sb.Append(".sl{font-size:8px;letter-spacing:1.5px;color:#3A5570;}");
            sb.Append(".sv{font-size:22px;font-weight:bold;margin-top:4px;}");
            sb.Append(".row2{display:grid;grid-template-columns:1fr 1fr;gap:12px;margin-bottom:12px;}");
            sb.Append(".bl{background:#0C1A26;border:1px solid #172435;padding:14px;}");
            sb.Append(".bl-full{background:#0C1A26;border:1px solid #172435;padding:14px;margin-bottom:12px;}");
            sb.Append(".bt{font-size:8px;letter-spacing:1.5px;color:#4CAF90;margin-bottom:10px;font-weight:bold;}");
            sb.Append("canvas{max-height:180px;}");
            sb.Append("</style></head><body>");

            sb.Append("<div class='sr'>");
            sb.AppendFormat("<div class='st'><div class='sl'>FAMILIES SERVED</div><div class='sv' style='color:#4CAF90'>{0}</div></div>", familiesServed);
            sb.AppendFormat("<div class='st'><div class='sl'>TOTAL DISTRIBUTIONS</div><div class='sv' style='color:#4CAF90'>{0}</div></div>", totalDistributions);
            sb.AppendFormat("<div class='st'><div class='sl'>RELIEF COVERAGE</div><div class='sv' style='color:{1}'>{0:F1}%</div></div>",
                coveragePct, coveragePct >= 75 ? "#4CAF90" : coveragePct >= 40 ? "#C09A30" : "#C05050");
            sb.AppendFormat("<div class='st'><div class='sl'>CITY CENTERS</div><div class='sv' style='color:#7A9BB8'>{0}</div></div>", centerCount);
            sb.Append("</div>");

            sb.Append("<div class='row2'>");
            sb.Append("<div class='bl'><div class='bt'>RELIEF COVERAGE</div><canvas id='cv'></canvas></div>");
            sb.Append("<div class='bl'><div class='bt'>DSWD RESPONSE STATUS</div><canvas id='ds'></canvas></div>");
            sb.Append("</div>");

            sb.Append("<div class='row2'>");
            sb.Append("<div class='bl'><div class='bt'>INCIDENTS BY ALARM LEVEL</div><canvas id='al'></canvas></div>");
            sb.Append("<div class='bl'><div class='bt'>CENTER UTILIZATION (%)</div><canvas id='cu'></canvas></div>");
            sb.Append("</div>");

            sb.Append("<div class='bl-full'><div class='bt'>TOP BARANGAYS BY FAMILIES REGISTERED</div><canvas id='bb' style='max-height:220px'></canvas></div>");

            sb.Append("<div class='bl-full'><div class='bt'>RELIEF DISTRIBUTIONS PER MONTH (LAST 12)</div><canvas id='ml' style='max-height:180px'></canvas></div>");

            sb.Append("<script src='https://cdnjs.cloudflare.com/ajax/libs/Chart.js/4.4.0/chart.umd.min.js'></script>");
            sb.Append("<script>");
            sb.Append("Chart.defaults.color='#7A9BB8';Chart.defaults.borderColor='#172435';");
            sb.Append("var G={color:'#172435'};");

            sb.AppendFormat("var ML={0},MD={1},BL={2},BD={3},CN={4},CP={5};", mlJs, mdJs, blJs, bdJs, cnJs, cpJs);
            sb.AppendFormat("var COV_SERVED={0},COV_UNSERVED={1};", covServed, covUnserved);
            sb.AppendFormat("var DS_P={0},DS_R={1},DS_C={2};", dPending, dResponding, dCompleted);
            sb.AppendFormat("var AL1={0},AL2={1},AL3={2},AL4={3};", alarmCounts[0], alarmCounts[1], alarmCounts[2], alarmCounts[3]);

            sb.Append("new Chart(document.getElementById('cv'),{type:'doughnut',");
            sb.Append("data:{labels:['Served','Unserved'],datasets:[{data:[COV_SERVED,COV_UNSERVED],");
            sb.Append("backgroundColor:['#4CAF90','#C09A30'],borderColor:'#09131C',borderWidth:2}]},");
            sb.Append("options:{responsive:true,plugins:{legend:{position:'bottom',labels:{font:{family:'Consolas'},padding:10}}}}});");

            sb.Append("new Chart(document.getElementById('ds'),{type:'doughnut',");
            sb.Append("data:{labels:['Pending','Responding','Completed'],datasets:[{data:[DS_P,DS_R,DS_C],");
            sb.Append("backgroundColor:['#C09A30','#4A8EC2','#4CAF90'],borderColor:'#09131C',borderWidth:2}]},");
            sb.Append("options:{responsive:true,plugins:{legend:{position:'bottom',labels:{font:{family:'Consolas'},padding:10}}}}});");

            sb.Append("new Chart(document.getElementById('al'),{type:'doughnut',");
            sb.Append("data:{labels:['Level 1','Level 2','Level 3','Level 4'],datasets:[{data:[AL1,AL2,AL3,AL4],");
            sb.Append("backgroundColor:['#4A8EC2','#C09A30','#C07030','#C05050'],borderColor:'#09131C',borderWidth:2}]},");
            sb.Append("options:{responsive:true,plugins:{legend:{position:'bottom',labels:{font:{family:'Consolas'},padding:8}}}}});");

            sb.Append("new Chart(document.getElementById('cu'),{type:'bar',");
            sb.Append("data:{labels:CN,datasets:[{label:'% Utilized',data:CP,");
            sb.Append("backgroundColor:CP.map(v=>v>=100?'rgba(192,80,80,0.7)':v>=80?'rgba(192,154,48,0.7)':'rgba(76,175,144,0.7)'),");
            sb.Append("borderWidth:0}]},");
            sb.Append("options:{indexAxis:'y',responsive:true,plugins:{legend:{display:false}},");
            sb.Append("scales:{x:{grid:G,max:100,ticks:{callback:function(v){return v+'%'}}},y:{grid:G}}}});");

            sb.Append("new Chart(document.getElementById('bb'),{type:'bar',");
            sb.Append("data:{labels:BL,datasets:[{label:'Families',data:BD,");
            sb.Append("backgroundColor:'rgba(74,142,194,0.6)',borderColor:'#4A8EC2',borderWidth:1}]},");
            sb.Append("options:{indexAxis:'y',responsive:true,plugins:{legend:{display:false}},");
            sb.Append("scales:{x:{grid:G,ticks:{stepSize:1}},y:{grid:G}}}});");

            sb.Append("new Chart(document.getElementById('ml'),{type:'line',");
            sb.Append("data:{labels:ML,datasets:[{label:'Distributions',data:MD,");
            sb.Append("borderColor:'#4CAF90',backgroundColor:'rgba(76,175,144,0.08)',");
            sb.Append("tension:0.3,fill:true,pointRadius:4,pointBackgroundColor:'#4CAF90'}]},");
            sb.Append("options:{responsive:true,plugins:{legend:{display:false}},");
            sb.Append("scales:{x:{grid:G},y:{grid:G,beginAtZero:true,ticks:{stepSize:1}}}}});");

            sb.Append("</script></body></html>");
            return sb.ToString();
        }

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
            {
                RefreshAllData();
                ShowSection(_currentSection);
            }
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            SessionManager.Logout();
            Frame.Navigate(typeof(LoginPage));
        }
    }
}
