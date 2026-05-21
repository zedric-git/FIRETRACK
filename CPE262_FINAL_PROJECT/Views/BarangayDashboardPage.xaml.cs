using CPE262_FINAL_PROJECT.Models;
using CPE262_FINAL_PROJECT.Repositories;
using CPE262_FINAL_PROJECT.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.IO;
using UIRectangle = Microsoft.UI.Xaml.Shapes.Rectangle;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Windows.UI;
using Microsoft.UI.Xaml.Navigation;

namespace CPE262_FINAL_PROJECT.Views
{
    public sealed partial class BarangayDashboardPage : Page
    {
        private readonly FamilyRepository                _familyRepo        = new();
        private readonly EvacuationCenterRepository      _evacRepo          = new();
        private readonly IncidentRepository              _incidentRepo      = new();
        private readonly AuditLogRepository              _auditRepo         = new();
        private readonly CrossBarangayRequestRepository  _crossRepo         = new();
        private readonly CitizenReportRepository         _citizenReportRepo = new();

        private List<Family>           _families  = new();
        private List<EvacuationCenter> _centers   = new();
        private List<Incident>         _incidents = new();

        private int _selectedCenterIdForOccupancy = 0;
        private int _selectedCenterCapacity        = 0;

        private bool   _evacMapReady   = false;
        private bool   _mapDropActive      = false;
        private Action?        _warningConfirmAction  = null;
        private Family?        _familyBeingReassigned = null;
        private double _pendingPinLat  = 0;
        private double _pendingPinLng  = 0;
        private int    _mapOccCenterId = 0;
        private int    _mapOccCapacity = 0;

        private bool _brgyChartWebViewReady = false;

        private DispatcherQueueTimer? _toastTimer;
        private string _currentSection = "Overview";
        private Window? _parentWindow  = null;

        private static readonly Color White = Color.FromArgb(255, 200, 216, 232);


        public BarangayDashboardPage()
        {
            InitializeComponent();

            OperatorName.Text     = SessionManager.FullName;
            OperatorBarangay.Text = string.IsNullOrWhiteSpace(SessionManager.AssignedBarangay)
                ? "All Barangays"
                : $"Brgy. {SessionManager.AssignedBarangay}";

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
            InitBrgyChartWebViewAsync();
        }


        private void RefreshAllData()
        {
            try
            {
                var brgy = SessionManager.AssignedBarangay;

                _incidents = string.IsNullOrWhiteSpace(brgy)
                    ? _incidentRepo.GetAll()
                    : _incidentRepo.GetByBarangay(brgy);

                _families = new List<Family>();
                foreach (var inc in _incidents)
                    _families.AddRange(_familyRepo.GetByIncident(inc.IncidentID));

                _centers = string.IsNullOrWhiteSpace(brgy)
                    ? _evacRepo.GetAll()
                    : _evacRepo.GetByBarangayIncludingCity(brgy);
            }
            catch (Exception ex)
            {
                _incidents = new();
                _families = new();
                _centers = new();
                ShowToast($"! Could not refresh dashboard data: {ex.Message}");
            }
        }


        private void ShowSection(string section)
        {
            _currentSection = section;
            SectionOverview.Visibility  = Visibility.Collapsed;
            SectionFamilies.Visibility  = Visibility.Collapsed;
            SectionCenters.Visibility   = Visibility.Collapsed;
            SectionIncidents.Visibility = Visibility.Collapsed;
            SectionAnalysis.Visibility  = Visibility.Collapsed;
            SectionCitizenReports.Visibility = Visibility.Collapsed;
TopBarTitle.Text = section switch
            {
                "Overview"        => "OVERVIEW",
                "Families"        => "AFFECTED FAMILIES",
                "Centers"         => "EVACUATION CENTERS",
                "Incidents"       => "INCIDENT REPORTS",
                "EvacMap"         => "EVAC CENTER MAP",
                "CitizenReports"  => "CITIZEN REPORTS",
                _                 => section.ToUpper()
            };
            TopBarSubtitle.Text = section switch
            {
                "Overview"        => "/ Barangay Response Dashboard",
                "Families"        => "/ Affected Families Registry",
                "Centers"         => "/ Evacuation Center Management",
                "Incidents"       => "/ Active Fire Incidents",
                "EvacMap"         => "/ Map-Based Center Management",
                "CitizenReports"  => "/ Verify resident-submitted tips",
                _                 => ""
            };

            SetNavActive(NavOverviewBtn,       false);
            SetNavActive(NavFamiliesBtn,       false);
            SetNavActive(NavCentersBtn,        false);
            SetNavActive(NavIncidentsBtn,      false);
            SetNavActive(NavAnalysisBtn,       false);

            switch (section)
            {
                case "Overview":
                    SectionOverview.Visibility = Visibility.Visible;
                    SetNavActive(NavOverviewBtn, true);
                    BuildOverview();
                    break;
                case "Families":
                    SectionFamilies.Visibility = Visibility.Visible;
                    SetNavActive(NavFamiliesBtn, true);
                    BuildFamiliesSection();
                    break;
                case "Centers":
                    SectionCenters.Visibility = Visibility.Visible;
                    SetNavActive(NavCentersBtn, true);
                    BuildCentersSection();
                    break;
                case "Incidents":
                    SectionIncidents.Visibility = Visibility.Visible;
                    SetNavActive(NavIncidentsBtn, true);
                    BuildIncidentsSection();
                    break;
                case "Analysis":
                    SectionAnalysis.Visibility  = Visibility.Visible;
                    BrgyChartLoading.Visibility = Visibility.Visible;
                    SetNavActive(NavAnalysisBtn, true);
                    LoadBrgyAnalysisCharts();
                    break;
                case "CitizenReports":
                    SectionCitizenReports.Visibility = Visibility.Visible;
                    BuildBrgyCitizenReportsSection();
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
                    ? new SolidColorBrush(Color.FromArgb(255, 74, 142, 194))
                    : new SolidColorBrush(Color.FromArgb(255, 122, 155, 184));
        }

        private void NavOverview_Click(object sender, RoutedEventArgs e)
        { RefreshAllData(); ShowSection("Overview"); }

        private void NavFamilies_Click(object sender, RoutedEventArgs e)
        { RefreshAllData(); ShowSection("Families"); }

        private void NavCenters_Click(object sender, RoutedEventArgs e)
        { RefreshAllData(); ShowSection("Centers"); }

        private void NavIncidents_Click(object sender, RoutedEventArgs e)
        { RefreshAllData(); ShowSection("Incidents"); }

        private void NavCitizenReports_Click(object sender, RoutedEventArgs e)
        { ShowSection("CitizenReports"); }



        private void BuildOverview()
        {
            StatTotalFamilies.Text    = _families.Count.ToString();
            StatPersonsDisplaced.Text = _families.Sum(f => f.MemberCount).ToString();
            StatEvacCenters.Text      = _centers.Count.ToString();
            StatRepeatDisplaced.Text  = _families.Count(f => f.IsRepeatDisplaced).ToString();

            BuildFamilyRows(OverviewFamiliesPanel, _families.TakeLast(10).Reverse().ToList(), compact: true);
            BuildCentersMini(OverviewCentersPanel);
            UpdateBrgyCitizenReportsButton();
        }

        private void UpdateBrgyCitizenReportsButton()
        {
            try
            {
                var brgy = SessionManager.AssignedBarangay;
                var reports = string.IsNullOrWhiteSpace(brgy)
                    ? _citizenReportRepo.GetAllForBarangayInbox()
                    : _citizenReportRepo.GetByBarangay(brgy);
                int pending = reports.Count(r => !r.IsVerified);
                BrgyCitizenReportsBadge.Visibility = pending > 0 ? Visibility.Visible : Visibility.Collapsed;
                BrgyCitizenReportsBadgeCount.Text = pending.ToString();
            }
            catch
            {
                BrgyCitizenReportsBadge.Visibility = Visibility.Collapsed;
                BrgyCitizenReportsBadgeCount.Text = "0";
            }
        }

        private void BuildCentersMini(StackPanel panel)
        {
            panel.Children.Clear();

            if (_centers.Count == 0)
            {
                panel.Children.Add(MakeEmptyState("No centers registered yet", "Use Evac Center Map to add one",
                    new Thickness(16, 12, 16, 12)));
                return;
            }

            foreach (var c in _centers)
            {
                double pct       = c.Capacity > 0 ? (double)c.CurrentOccupancy / c.Capacity : 0;
                var    fillColor = pct >= 1.0 ? Color.FromArgb(255, 192, 80, 80)
                                 : pct >= 0.8 ? Color.FromArgb(255, 192, 154, 48)
                                 : Color.FromArgb(255, 74, 142, 194);

                var row = new Border
                {
                    Padding         = new Thickness(16, 12, 16, 12),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    BorderBrush     = new SolidColorBrush(Color.FromArgb(255, 23, 36, 53))
                };
                var sp = new StackPanel { Spacing = 6 };

                var nameRow = new Grid();
                nameRow.Children.Add(new TextBlock
                {
                    Text = c.Name, FontFamily = new FontFamily("Consolas"),
                    FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(White)
                });
                string statusTxt = c.IsFull ? "FULL" : pct >= 0.8 ? "NEAR FULL" : "AVAILABLE";
                var statusBg = c.IsFull   ? Color.FromArgb(255, 45, 20, 20)
                             : pct >= 0.8 ? Color.FromArgb(255, 45, 38, 10)
                             :              Color.FromArgb(255, 15, 35, 20);
                nameRow.Children.Add(new Border
                {
                    Background = new SolidColorBrush(statusBg), CornerRadius = new CornerRadius(0),
                    Padding = new Thickness(8, 3, 8, 3),
                    HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = statusTxt, FontFamily = new FontFamily("Consolas"),
                        FontSize = 8, Foreground = new SolidColorBrush(fillColor),
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold
                    }
                });
                sp.Children.Add(nameRow);

                var barGrid = new Grid { Height = 4 };
                barGrid.Children.Add(new Border { Background = new SolidColorBrush(Color.FromArgb(255, 23, 36, 53)) });
                var fill = new Border { Background = new SolidColorBrush(fillColor), HorizontalAlignment = HorizontalAlignment.Left, Tag = pct };
                barGrid.Children.Add(fill);
                barGrid.SizeChanged += (s, _) =>
                {
                    if (((Grid)s).Children[1] is Border f)
                        f.Width = Math.Max(0, Math.Min(1, f.Tag is double d ? d : 0)) * ((Grid)s).ActualWidth;
                };
                sp.Children.Add(barGrid);

                sp.Children.Add(new TextBlock
                {
                    Text = $"{c.CurrentOccupancy} / {c.Capacity} persons",
                    FontFamily = new FontFamily("Consolas"), FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 74, 107, 138))
                });

                row.Child = sp;
                panel.Children.Add(row);
            }
        }


        private void BuildFamiliesSection()
        {
            FamiliesSubtitle.Text = _families.Count == 0
                ? "No families registered yet"
                : $"{_families.Count} families · {_families.Sum(f => f.MemberCount)} persons total";
            BuildFamilyRows(AllFamiliesPanel, _families, compact: false);
        }

        private void BuildFamilyRows(StackPanel panel, List<Family> families, bool compact)
        {
            panel.Children.Clear();
            if (families.Count == 0)
            {
                panel.Children.Add(MakeEmptyState("No families registered",
                    "Register an affected family to get started", new Thickness(20, 32, 20, 32)));
                return;
            }

            var centerLookup = _centers.ToDictionary(c => c.CenterID, c => c.Name);

            foreach (var fam in families)
            {
                var reliefColor = fam.ReliefStatus == "Received"
                    ? Color.FromArgb(255, 76, 175, 80) : Color.FromArgb(255, 255, 193, 7);
                var reliefBg = fam.ReliefStatus == "Received"
                    ? Color.FromArgb(40, 76, 175, 80) : Color.FromArgb(40, 255, 193, 7);
                string centerName = fam.EvacuationCenterID.HasValue &&
                                    centerLookup.TryGetValue(fam.EvacuationCenterID.Value, out var cn) ? cn : "—";

                var row = new Border
                {
                    Padding = new Thickness(20, 12, 20, 12),
                    BorderThickness = new Thickness(0, 1, 0, 0),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 23, 36, 53))
                };
                var grid = new Grid { ColumnSpacing = 8 };
                if (compact)
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
                }
                else
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                }

                var nameStack = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
                nameStack.Children.Add(new TextBlock
                {
                    Text = fam.HeadName, FontFamily = new FontFamily("Consolas"),
                    FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(White)
                });
                if (fam.IsRepeatDisplaced)
                    nameStack.Children.Add(new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(255, 45, 20, 20)),
                        CornerRadius = new CornerRadius(0), Padding = new Thickness(6, 2, 6, 2),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Child = new TextBlock
                        {
                            Text = "! REPEAT", FontFamily = new FontFamily("Consolas"), FontSize = 8,
                            Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 155, 138)),
                            FontWeight = Microsoft.UI.Text.FontWeights.Bold
                        }
                    });
                Grid.SetColumn(nameStack, 0); grid.Children.Add(nameStack);

                var membersText = new TextBlock
                {
                    Text = fam.MemberCount.ToString(), FontFamily = new FontFamily("Consolas"), FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 122, 155, 184)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(membersText, 1); grid.Children.Add(membersText);

                var centerText = new TextBlock
                {
                    Text = centerName, FontFamily = new FontFamily("Consolas"), FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 122, 155, 184)),
                    VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap
                };
                Grid.SetColumn(centerText, 2); grid.Children.Add(centerText);

                var reliefBadge = new Border
                {
                    Background = new SolidColorBrush(reliefBg), CornerRadius = new CornerRadius(0),
                    Padding = new Thickness(8, 4, 8, 4),
                    HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = fam.ReliefStatus.ToUpper(), FontFamily = new FontFamily("Consolas"),
                        FontSize = 9, Foreground = new SolidColorBrush(reliefColor),
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold
                    }
                };
                Grid.SetColumn(reliefBadge, 3); grid.Children.Add(reliefBadge);

                if (!compact)
                {
                    var repeatCell = new TextBlock
                    {
                        Text = fam.IsRepeatDisplaced ? "YES" : "—",
                        FontFamily = new FontFamily("Consolas"), FontSize = 10,
                        Foreground = new SolidColorBrush(fam.IsRepeatDisplaced
                            ? Color.FromArgb(255, 192, 80, 80) : Color.FromArgb(255, 74, 107, 138)),
                        VerticalAlignment = VerticalAlignment.Center,
                        FontWeight = fam.IsRepeatDisplaced
                            ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal
                    };
                    Grid.SetColumn(repeatCell, 4); grid.Children.Add(repeatCell);
                }

                if (!compact)
                {
                    var actionRow = new StackPanel {
                        Orientation = Orientation.Horizontal, Spacing = 6,
                        Padding = new Thickness(20, 0, 20, 10)
                    };

                    if (fam.EvacuationCenterID.HasValue)
                    {
                        var unassignBtn = new Button { Style = (Style)Resources["GhostBtnStyle"], Tag = fam };
                        unassignBtn.Content = new TextBlock {
                            Text = "UNASSIGN CENTER", FontFamily = new FontFamily("Consolas"),
                            FontSize = 8, Foreground = new SolidColorBrush(Color.FromArgb(255, 192, 154, 48)),
                            FontWeight = Microsoft.UI.Text.FontWeights.Bold, CharacterSpacing = 60
                        };
                        unassignBtn.Click += (s, _) => UnassignFamily_Click((Family)((Button)s).Tag!);
                        actionRow.Children.Add(unassignBtn);
                    }

                    bool isAssigned = fam.EvacuationCenterID.HasValue;
                    var assignBtn = new Button { Style = (Style)Resources["GhostBtnStyle"], Tag = fam };
                    assignBtn.Content = new TextBlock {
                        Text = isAssigned ? "REASSIGN CENTER" : "ASSIGN CENTER",
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 8, Foreground = new SolidColorBrush(Color.FromArgb(255, 74, 142, 194)),
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold, CharacterSpacing = 60
                    };
                    assignBtn.Click += (s, _) => ReassignFamily_Click((Family)((Button)s).Tag!);
                    actionRow.Children.Add(assignBtn);

                    var deleteBtn = new Button { Style = (Style)Resources["GhostBtnStyle"], Tag = fam };
                    deleteBtn.Content = new TextBlock {
                        Text = "DELETE", FontFamily = new FontFamily("Consolas"),
                        FontSize = 8, Foreground = new SolidColorBrush(Color.FromArgb(255, 192, 80, 80)),
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold, CharacterSpacing = 60
                    };
                    deleteBtn.Click += (s, _) => DeleteFamily_Click((Family)((Button)s).Tag!);
                    actionRow.Children.Add(deleteBtn);

                    row.Child = grid;
                    var cardWrap = new Border {
                        BorderThickness = new Thickness(0, 1, 0, 0),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(255, 23, 36, 53))
                    };
                    var cardStack = new StackPanel();
                    cardStack.Children.Add(row);
                    cardStack.Children.Add(actionRow);
                    cardWrap.Child = cardStack;
                    panel.Children.Add(cardWrap);
                }
                else
                {
                    row.Child = grid;
                    panel.Children.Add(row);
                }
            }
        }


        private void UnassignFamily_Click(Family fam)
        {
            if (!fam.EvacuationCenterID.HasValue) return;
            try
            {
                _familyRepo.UnassignCenter(fam.FamilyID, fam.MemberCount, fam.EvacuationCenterID.Value);
                _auditRepo.Log(SessionManager.UserID, "UPDATE", "Families", fam.FamilyID,
                    $"Unassigned from center #{fam.EvacuationCenterID.Value}");
                RefreshAllData();
                BuildFamiliesSection();
                BuildCentersMini(OverviewCentersPanel);
                if (_evacMapReady) EvacMap_SendCenters();
                ShowToast($"\"{fam.HeadName}\" unassigned from center.");
            }
            catch (Exception ex)
            {
                ShowToast($"! Could not unassign family: {ex.Message}");
            }
        }


        private void ReassignFamily_Click(Family fam)
        {
            _familyBeingReassigned = fam;
            ReassignFamilyLabel.Text = $"Family: {fam.HeadName} ({fam.MemberCount} members)";
            ReassignCenterPicker.Items.Clear();
            ReassignCenterPicker.Items.Add(new ComboBoxItem { Content = "— Select new center —", Tag = 0 });

            var myBrgy  = SessionManager.AssignedBarangay;
            var seenIds = new HashSet<int>();
            foreach (var c in _centers)
            {
                if (!seenIds.Add(c.CenterID)) continue;
                string badge = c.CenterType == "City"
                    ? " [CITY-LEVEL]"
                    : (c.CenterType == "Barangay" && c.Barangay != myBrgy)
                        ? $" [CROSS-BRGY · {c.Barangay}]"
                        : "";
                string label = c.IsFull
                    ? $"{c.Name}{badge} — FULL ({c.CurrentOccupancy}/{c.Capacity})"
                    : $"{c.Name}{badge} ({c.AvailableSlots} slots available)";
                ReassignCenterPicker.Items.Add(new ComboBoxItem { Content = label, Tag = c.CenterID });
            }

            ReassignCenterPicker.SelectedIndex = 0;
            ReassignErrorMsg.Visibility = Visibility.Collapsed;
            ReassignCenterOverlay.Visibility = Visibility.Visible;
        }

        private void CloseReassignOverlay_Click(object sender, RoutedEventArgs e)
        {
            _familyBeingReassigned = null;
            ReassignCenterOverlay.Visibility = Visibility.Collapsed;
        }

        private void SubmitReassign_Click(object sender, RoutedEventArgs e)
        {
            if (_familyBeingReassigned == null) return;
            int newCenterId = ReassignCenterPicker.SelectedItem is ComboBoxItem ci ? (int)(ci.Tag ?? 0) : 0;
            if (newCenterId == 0)
            {
                ReassignErrorMsg.Text = "Please select a center.";
                ReassignErrorMsg.Visibility = Visibility.Visible;
                return;
            }

            var target = _evacRepo.GetById(newCenterId);
            if (target == null)
            {
                ReassignErrorMsg.Text = "Center not found.";
                ReassignErrorMsg.Visibility = Visibility.Visible;
                return;
            }
            if (target.AvailableSlots < _familyBeingReassigned.MemberCount)
            {
                ReassignErrorMsg.Text =
                    $"Not enough capacity — {target.Name} has {target.AvailableSlots} slot(s) available, " +
                    $"family has {_familyBeingReassigned.MemberCount} member(s).";
                ReassignErrorMsg.Visibility = Visibility.Visible;
                return;
            }

            ReassignErrorMsg.Visibility = Visibility.Collapsed;
            try
            {
                _familyRepo.AssignCenter(_familyBeingReassigned.FamilyID, newCenterId,
                    _familyBeingReassigned.MemberCount, _familyBeingReassigned.EvacuationCenterID);
                _auditRepo.Log(SessionManager.UserID, "UPDATE", "Families", _familyBeingReassigned.FamilyID,
                    $"Reassigned to center #{newCenterId}");
                ReassignCenterOverlay.Visibility = Visibility.Collapsed;
                _familyBeingReassigned = null;
                RefreshAllData();
                BuildFamiliesSection();
                BuildCentersMini(OverviewCentersPanel);
                if (_evacMapReady) EvacMap_SendCenters();
                ShowToast("Family reassigned to new center.");
            }
            catch (Exception ex)
            {
                ReassignErrorMsg.Text = $"Could not reassign family: {ex.Message}";
                ReassignErrorMsg.Visibility = Visibility.Visible;
            }
        }


        private void DeleteFamily_Click(Family fam)
        {
            ShowWarning(
                $"Delete family \"{fam.HeadName}\"?\n\nAny associated relief records will also be permanently removed.\n\nThis cannot be undone.",
                "DELETE FAMILY", () =>
            {
                var (reliefDeleted, deleted) = _familyRepo.DeleteFamily(
                    fam.FamilyID, fam.EvacuationCenterID, fam.MemberCount);
                if (!deleted) { ShowToast("! Failed to delete family."); return; }
                _auditRepo.Log(SessionManager.UserID, "DELETE", "Families", fam.FamilyID,
                    $"Deleted family: {fam.HeadName}" +
                    (reliefDeleted > 0 ? $" + {reliefDeleted} relief record(s)" : ""));
                string note = reliefDeleted > 0 ? $" and {reliefDeleted} relief record(s) removed." : ".";
                RefreshAllData();
                BuildFamiliesSection();
                BuildCentersMini(OverviewCentersPanel);
                if (_evacMapReady) EvacMap_SendCenters();
                ShowToast($"\"{fam.HeadName}\" deleted{note}");
            });
        }


        private void BuildCentersSection()
        {
            CentersDetailPanel.Children.Clear();
            CapacityOverviewPanel.Children.Clear();

            if (_centers.Count == 0)
            {
                CentersDetailPanel.Children.Add(MakeEmptyState(
                    "No evacuation centers registered",
                    "Use the Evac Center Map section to add one",
                    new Thickness(20, 32, 20, 32)));
                BuildCentersRequestPanels();
                return;
            }

            int totalCap = _centers.Sum(c => c.Capacity);
            int totalOcc = _centers.Sum(c => c.CurrentOccupancy);

            CapacityOverviewPanel.Children.Add(MakeCapacitySummaryRow("TOTAL CAPACITY",    totalCap.ToString(), White));
            CapacityOverviewPanel.Children.Add(MakeCapacitySummaryRow("CURRENT OCCUPANCY", totalOcc.ToString(), Color.FromArgb(255, 74, 142, 194)));
            CapacityOverviewPanel.Children.Add(MakeCapacitySummaryRow("AVAILABLE SLOTS",   (totalCap - totalOcc).ToString(), Color.FromArgb(255, 76, 175, 80)));
            CapacityOverviewPanel.Children.Add(MakeCapacitySummaryRow("CENTERS FULL",      _centers.Count(c => c.IsFull).ToString(),
                _centers.Any(c => c.IsFull) ? Color.FromArgb(255, 192, 80, 80) : Color.FromArgb(255, 74, 107, 138)));

            foreach (var c in _centers)
            {
                double pct       = c.Capacity > 0 ? (double)c.CurrentOccupancy / c.Capacity : 0;
                var    fillColor = pct >= 1.0 ? Color.FromArgb(255, 192, 80, 80)
                                 : pct >= 0.8 ? Color.FromArgb(255, 192, 154, 48)
                                 :              Color.FromArgb(255, 74, 142, 194);

                var card = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(255, 12, 26, 38)),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 23, 36, 53)),
                    Padding = new Thickness(20, 16, 20, 16)
                };
                var outer = new StackPanel { Spacing = 12 };

                var hdr  = new Grid();
                var left = new StackPanel { Spacing = 4 };
                left.Children.Add(new TextBlock
                {
                    Text = c.Name, FontFamily = new FontFamily("Consolas"),
                    FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(White)
                });
                left.Children.Add(new TextBlock
                {
                    Text = $"Brgy. {c.Barangay}  ·  Type: {c.CenterType}  ·  Updated: {(c.LastUpdated.Length >= 10 ? c.LastUpdated[..10] : "—")}",
                    FontFamily = new FontFamily("Consolas"), FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 58, 85, 112))
                });
                hdr.Children.Add(left);

                var btnRow = new StackPanel {
                    Orientation = Orientation.Horizontal, Spacing = 6,
                    HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center
                };
                var updBtn = new Button { Style = (Style)Resources["PrimaryBtnStyle"], Tag = c };
                updBtn.Content = new TextBlock { Text = "UPDATE OCCUPANCY",
                    FontFamily = new FontFamily("Consolas"), FontSize = 9,
                    Foreground = new SolidColorBrush(White), FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    CharacterSpacing = 60 };
                updBtn.Click += (s, _) => OpenOccupancyDialog((EvacuationCenter)((Button)s).Tag!);
                btnRow.Children.Add(updBtn);

                var detailsBtn = new Button { Style = (Style)Resources["GhostBtnStyle"], Tag = c };
                detailsBtn.Content = new TextBlock { Text = "VIEW DETAILS",
                    FontFamily = new FontFamily("Consolas"), FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 122, 155, 184)),
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold, CharacterSpacing = 60 };
                detailsBtn.Click += (s, _) => OpenCenterDetailsOverlay((EvacuationCenter)((Button)s).Tag!);
                btnRow.Children.Add(detailsBtn);

                if (c.CenterType == "City"
                    || (c.CenterType == "Barangay" && c.Barangay != SessionManager.AssignedBarangay))
                {
                    var releaseBtn = new Button { Style = (Style)Resources["GhostBtnStyle"], Tag = c };
                    releaseBtn.Content = new TextBlock { Text = "RELEASE",
                        FontFamily = new FontFamily("Consolas"), FontSize = 9,
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 192, 154, 48)),
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold, CharacterSpacing = 60 };
                    releaseBtn.Click += (s, _) => ReleaseCenter_Click((EvacuationCenter)((Button)s).Tag!);
                    btnRow.Children.Add(releaseBtn);
                }
                else
                {
                    var delBtn = new Button { Style = (Style)Resources["GhostBtnStyle"], Tag = c };
                    delBtn.Content = new TextBlock { Text = "REMOVE",
                        FontFamily = new FontFamily("Consolas"), FontSize = 9,
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 192, 80, 80)),
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold, CharacterSpacing = 60 };
                    delBtn.Click += (s, _) => RemoveCenter_Click((EvacuationCenter)((Button)s).Tag!);
                    btnRow.Children.Add(delBtn);
                }
                hdr.Children.Add(btnRow);
                outer.Children.Add(hdr);

                var barGrid = new Grid { Height = 6 };
                barGrid.Children.Add(new Border { Background = new SolidColorBrush(Color.FromArgb(255, 23, 36, 53)) });
                var fill = new Border { Background = new SolidColorBrush(fillColor), HorizontalAlignment = HorizontalAlignment.Left, Tag = pct };
                barGrid.Children.Add(fill);
                barGrid.SizeChanged += (s, _) =>
                {
                    if (((Grid)s).Children[1] is Border f)
                        f.Width = Math.Max(0, Math.Min(1, f.Tag is double d ? d : 0)) * ((Grid)s).ActualWidth;
                };
                outer.Children.Add(barGrid);

                var stats = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
                stats.Children.Add(MakeCenterStat("CAPACITY",    c.Capacity.ToString(),        White));
                stats.Children.Add(MakeCenterStat("OCCUPIED",    c.CurrentOccupancy.ToString(), Color.FromArgb(255, 74, 142, 194)));
                stats.Children.Add(MakeCenterStat("AVAILABLE",   c.AvailableSlots.ToString(),   Color.FromArgb(255, 76, 175, 80)));
                stats.Children.Add(MakeCenterStat("UTILIZATION", $"{(int)(pct * 100)}%",       fillColor));
                outer.Children.Add(stats);

                card.Child = outer;
                CentersDetailPanel.Children.Add(card);
            }

            BuildCentersRequestPanels();
        }

        private UIElement MakeCapacitySummaryRow(string label, string value, Color color)
        {
            var row = new Grid { Padding = new Thickness(0, 4, 0, 4) };
            row.Children.Add(new TextBlock { Text = label, FontFamily = new FontFamily("Consolas"),
                FontSize = 10, Foreground = new SolidColorBrush(Color.FromArgb(255, 58, 85, 112)) });
            row.Children.Add(new TextBlock { Text = value, FontFamily = new FontFamily("Consolas"),
                FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(color),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center });
            return row;
        }

        private StackPanel MakeCenterStat(string label, string value, Color color)
        {
            var sp = new StackPanel { Spacing = 2 };
            sp.Children.Add(new TextBlock { Text = label, FontFamily = new FontFamily("Consolas"),
                FontSize = 8, Foreground = new SolidColorBrush(Color.FromArgb(255, 58, 85, 112)), CharacterSpacing = 150 });
            sp.Children.Add(new TextBlock { Text = value, FontFamily = new FontFamily("Consolas"),
                FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(color) });
            return sp;
        }


        private void BuildIncidentsSection()
        {
            IncidentsPanel.Children.Clear();
            var brgy = SessionManager.AssignedBarangay;
            IncidentsSubtitle.Text = string.IsNullOrWhiteSpace(brgy)
                ? $"{_incidents.Count} incidents total"
                : $"{_incidents.Count} incidents in Brgy. {brgy}";

            if (_incidents.Count == 0)
            {
                IncidentsPanel.Children.Add(MakeEmptyState(
                    "No incidents recorded in your barangay",
                    "Incidents are registered by BFP Officers",
                    new Thickness(20, 32, 20, 32)));
                return;
            }

            foreach (var inc in _incidents)
            {
                Color statusColor; string statusLabel; Color statusBg;
                switch (inc.Status)
                {
                    case "Under Control":
                        statusColor = Color.FromArgb(255, 255, 165, 50); statusBg = Color.FromArgb(255, 35, 25, 10); statusLabel = "UNDER CONTROL"; break;
                    case "Fire Out":
                        statusColor = Color.FromArgb(255, 76, 175, 80); statusBg = Color.FromArgb(255, 10, 30, 15); statusLabel = "FIRE OUT"; break;
                    default:
                        statusColor = Color.FromArgb(255, 192, 80, 80); statusBg = Color.FromArgb(255, 45, 15, 15); statusLabel = "ACTIVE"; break;
                }

                var card = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(255, 12, 26, 38)),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 23, 36, 53)),
                    Padding = new Thickness(20, 16, 20, 16)
                };
                var sp = new StackPanel { Spacing = 10 };

                var hdrRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
                hdrRow.Children.Add(new Border
                {
                    Background = new SolidColorBrush(statusBg), CornerRadius = new CornerRadius(0),
                    Padding = new Thickness(12, 5, 12, 5),
                    Child = new TextBlock { Text = statusLabel, FontFamily = new FontFamily("Consolas"),
                        FontSize = 10, Foreground = new SolidColorBrush(statusColor),
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold }
                });
                hdrRow.Children.Add(new TextBlock
                {
                    Text = $"INCIDENT #{inc.IncidentID}",
                    FontFamily = new FontFamily("Consolas"), FontSize = 13,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(White), VerticalAlignment = VerticalAlignment.Center
                });
                hdrRow.Children.Add(new TextBlock
                {
                    Text = $"Alarm Level {inc.AlarmLevel}",
                    FontFamily = new FontFamily("Consolas"), FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 58, 85, 112)),
                    VerticalAlignment = VerticalAlignment.Center
                });
                sp.Children.Add(hdrRow);

                var detGrid = new Grid { ColumnSpacing = 32 };
                for (int k = 0; k < 4; k++) detGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var items = new[] {
                    ("SITIO",       inc.Sitio),
                    ("CAUSE",       string.IsNullOrWhiteSpace(inc.CauseOfFire) ? "—" : inc.CauseOfFire),
                    ("DATE/TIME",   inc.DateTime.Length > 16 ? inc.DateTime[..16] : inc.DateTime),
                    ("DSWD STATUS", inc.DSDWStatus)
                };
                for (int i = 0; i < items.Length; i++)
                {
                    var col = new StackPanel { Spacing = 2 };
                    Grid.SetColumn(col, i);
                    col.Children.Add(new TextBlock { Text = items[i].Item1, FontFamily = new FontFamily("Consolas"),
                        FontSize = 8, Foreground = new SolidColorBrush(Color.FromArgb(255, 58, 85, 112)), CharacterSpacing = 150 });
                    col.Children.Add(new TextBlock { Text = items[i].Item2, FontFamily = new FontFamily("Consolas"),
                        FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(White) });
                    detGrid.Children.Add(col);
                }
                sp.Children.Add(detGrid);

                var incFamilies = _families.Where(f => f.IncidentID == inc.IncidentID).ToList();
                if (incFamilies.Count > 0)
                {
                    var pills = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
                    pills.Children.Add(MakePill($"{incFamilies.Count} families",
                        Color.FromArgb(255, 74, 142, 194), Color.FromArgb(255, 15, 28, 42)));
                    pills.Children.Add(MakePill($"{incFamilies.Sum(f => f.MemberCount)} persons",
                        Color.FromArgb(255, 74, 142, 194), Color.FromArgb(255, 15, 28, 42)));
                    int received = incFamilies.Count(f => f.ReliefStatus == "Received");
                    if (received > 0)
                        pills.Children.Add(MakePill($"{received} received relief",
                            Color.FromArgb(255, 76, 175, 80), Color.FromArgb(255, 15, 40, 20)));
                    sp.Children.Add(pills);
                }

                card.Child = sp;
                IncidentsPanel.Children.Add(card);
            }
        }

        private static Border MakePill(string text, Color fg, Color bg) => new()
        {
            Background = new SolidColorBrush(bg), CornerRadius = new CornerRadius(0),
            Padding = new Thickness(10, 4, 10, 4),
            Child = new TextBlock { Text = text, FontFamily = new FontFamily("Consolas"),
                FontSize = 9, Foreground = new SolidColorBrush(fg), FontWeight = Microsoft.UI.Text.FontWeights.Bold }
        };


        private void OpenEvacMapOverlay_Click(object sender, RoutedEventArgs e)
        {
            RefreshAllData();
            EvacMapOverlay.Visibility       = Visibility.Visible;
            EvacMapBrgyLabel.Text           = $"Brgy. {SessionManager.AssignedBarangay}";
            OverlayAddCenterForm.Visibility = Visibility.Collapsed;
            OverlayOccForm.Visibility       = Visibility.Collapsed;
            BtnExitRequestMode.Visibility   = Visibility.Collapsed;

            if (!_evacMapReady)
                InitEvacMapAsync();
            else
            {
                EvacMap_SendCenters();
                BuildCentersSection();
            }
        }

        private void CloseEvacMapOverlay_Click(object sender, RoutedEventArgs e)
        {
            _mapDropActive = false;
            EvacMapOverlay.Visibility = Visibility.Collapsed;
            if (_evacMapReady)
            {
                EvacMapWebView.CoreWebView2.PostWebMessageAsString("{\"type\":\"exit_request_mode\"}");
                EvacMapWebView.CoreWebView2.PostWebMessageAsString("{\"type\":\"cancel_drop_mode\"}");
            }
            RefreshAllData();
            BuildCentersSection();
        }

        private async void InitEvacMapAsync()
        {
            try
            {
                EvacMapLoading.Visibility = Visibility.Visible;
                EvacMapStatusBar.Text     = "INITIALIZING MAP...";
                await EvacMapWebView.EnsureCoreWebView2Async();
                EvacMapWebView.CoreWebView2.WebMessageReceived -= EvacMap_MessageReceived;
                EvacMapWebView.CoreWebView2.WebMessageReceived += EvacMap_MessageReceived;
                var mapPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Map", "evac_map.html");
                EvacMapWebView.Source = new Uri(mapPath);
            }
            catch (Exception ex)
            {
                EvacMapLoading.Visibility = Visibility.Collapsed;
                EvacMapStatusBar.Text = $"Map failed to initialize: {ex.Message}";
                ShowToast($"! Map failed to initialize: {ex.Message}");
            }
        }

        private void EvacMap_OnReady()
        {
            _evacMapReady             = true;
            EvacMapLoading.Visibility = Visibility.Collapsed;
            EvacMapStatusBar.Text     = "MAP READY";

            string initMsg = "{\"type\":\"init_map\",\"barangay\":\"" + EscapeJson(SessionManager.AssignedBarangay) + "\"}";
            EvacMapWebView.CoreWebView2.PostWebMessageAsString(initMsg);
            EvacMap_SendCenters();
        }

        private static string EscapeJson(string s)
            => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

        private void EvacMap_SendCenters()
        {
            try
            {
            var brgy     = SessionManager.AssignedBarangay;
            var own      = _evacRepo.GetByBarangay(brgy);
            var city     = _evacRepo.GetCityLevel();
            var cross    = _evacRepo.GetApprovedCrossBarangayCenters(brgy);
            var chosenIds = _evacRepo.GetChosenCenterIds(brgy);

            object ToMapObj(EvacuationCenter c, bool forceChosen = false) => new {
                id        = c.CenterID,
                name      = c.Name,
                barangay  = c.Barangay,
                lat       = c.GPSLat,
                lng       = c.GPSLong,
                capacity  = c.Capacity,
                occupancy = c.CurrentOccupancy,
                isFull    = c.IsFull,
                inUse     = c.CurrentOccupancy > 0,
                isChosen  = forceChosen || chosenIds.Contains(c.CenterID)
            };

            EvacMapWebView.CoreWebView2.PostWebMessageAsString(
                JsonSerializer.Serialize(new {
                    type  = "load_centers",
                    own   = own.Select(c => ToMapObj(c)),
                    cross = cross.Select(c => ToMapObj(c, forceChosen: true)),
                    city  = System.Array.Empty<object>()
                })
            );
            EvacMapStatusBar.Text = $"{own.Count} own center(s) · {cross.Count} approved cross-brgy";
            }
            catch (Exception ex)
            {
                EvacMapStatusBar.Text = $"Could not load centers: {ex.Message}";
                ShowToast($"! Could not load map centers: {ex.Message}");
            }
        }

        private void EvacMap_MessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var raw = e.TryGetWebMessageAsString();
            try
            {
                string msgType;
                double lat = 0, lng = 0;
                int    centerId = 0, capacity = 0, currentOcc = 0;
                string centerName = "", barangay = "";

                using (var doc = JsonDocument.Parse(raw))
                {
                    msgType = doc.RootElement.GetProperty("type").GetString() ?? "";

                    if (msgType == "center_pin_dropped")
                    {
                        lat = doc.RootElement.GetProperty("lat").GetDouble();
                        lng = doc.RootElement.GetProperty("lng").GetDouble();
                    }
                    else if (msgType == "open_update_occupancy")
                    {
                        centerId   = doc.RootElement.GetProperty("centerId").GetInt32();
                        capacity   = doc.RootElement.GetProperty("capacity").GetInt32();
                        currentOcc = doc.RootElement.GetProperty("currentOcc").GetInt32();
                    }
                    else if (msgType == "choose_center" || msgType == "delete_center")
                    {
                        centerId = doc.RootElement.GetProperty("centerId").GetInt32();
                    }
                    else if (msgType == "request_center")
                    {
                        centerId   = doc.RootElement.GetProperty("centerId").GetInt32();
                        centerName = doc.RootElement.GetProperty("centerName").GetString() ?? "";
                        barangay   = doc.RootElement.GetProperty("barangay").GetString() ?? "";
                    }
                }

                DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                    switch (msgType)
                    {
                        case "evac_map_ready":
                            EvacMap_OnReady();
                            break;

                        case "center_pin_dropped":
                            _mapDropActive = false;
                            _pendingPinLat = lat;
                            _pendingPinLng = lng;
                            OverlayNewName.Text             = "";
                            OverlayNewCapacity.Text         = "";
                            OverlayNewError.Visibility      = Visibility.Collapsed;
                            OverlayAddCenterForm.Visibility = Visibility.Visible;
                            OverlayOccForm.Visibility       = Visibility.Collapsed;
                            EvacMapStatusBar.Text           = "Pin dropped — fill in center details";
                            break;

                        case "open_update_occupancy":
                            _mapOccCenterId = centerId;
                            _mapOccCapacity = capacity;
                            OverlayOccTitle.Text            = "UPDATE OCCUPANCY";
                            OverlayOccHint.Text             = $"Capacity: {capacity}  ·  Current: {currentOcc}";
                            OverlayOccInput.Text            = currentOcc.ToString();
                            OverlayOccError.Visibility      = Visibility.Collapsed;
                            OverlayOccForm.Visibility       = Visibility.Visible;
                            OverlayAddCenterForm.Visibility = Visibility.Collapsed;
                            break;

                        case "choose_center":
                            HandleChooseCenter(centerId);
                            break;

                        case "delete_center":
                            var center = _evacRepo.GetById(centerId);
                            if (center != null)
                                DeleteCenter_Click(center);
                            break;

                        case "request_center":
                            SubmitCrossRequest(centerId, centerName, barangay);
                            break;
                    }
                    }
                    catch (Exception ex)
                    {
                        EvacMapStatusBar.Text = $"Map action failed: {ex.Message}";
                        ShowToast($"! Map action failed: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EvacMap bridge error: {ex.Message}");
            }
        }

        private void HandleChooseCenter(int centerId)
        {
            var center = _evacRepo.GetById(centerId);
            if (center == null) return;
            var myBrgy = SessionManager.AssignedBarangay;

            if (center.CenterType == "Barangay" && center.Barangay == myBrgy)
            {
                _evacRepo.MarkAsChosen(centerId, myBrgy);
                _auditRepo.Log(SessionManager.UserID, "CREATE", "Barangay_Chosen_Centers", centerId,
                    $"Chose own center \"{center.Name}\" for active use");
                EvacMapOverlay.Visibility = Visibility.Collapsed;
                RefreshAllData();
                ShowSection("Centers");
                ShowToast($"\"{center.Name}\" added to active centers.");
                return;
            }

            if (center.CenterType == "City")
            {
                _evacRepo.MarkAsChosen(centerId, myBrgy);
                _auditRepo.Log(SessionManager.UserID, "CREATE", "Barangay_Chosen_Centers", centerId,
                    $"Opted Brgy. {myBrgy} into city center \"{center.Name}\"");
                EvacMapOverlay.Visibility = Visibility.Collapsed;
                RefreshAllData();
                ShowSection("Centers");
                ShowToast($"City-level \"{center.Name}\" activated for Brgy. {myBrgy}.");
                return;
            }

            if (_evacMapReady) EvacMap_SendCenters();
            ShowToast($"Brgy. {center.Barangay}'s \"{center.Name}\" is available. Assign families from Affected Families → Register/Reassign.");
        }

        private void EvacMap_PlaceCenter_Click(object sender, RoutedEventArgs e)
        {
            if (BtnExitRequestMode.Visibility == Visibility.Visible)
            {
                ShowToast("Exit request mode first before placing a new center.");
                return;
            }
            _mapDropActive = !_mapDropActive;
            OverlayAddCenterForm.Visibility = Visibility.Collapsed;
            OverlayOccForm.Visibility       = Visibility.Collapsed;
            if (_evacMapReady)
            {
                if (_mapDropActive)
                {
                    EvacMapWebView.CoreWebView2.PostWebMessageAsString("{\"type\":\"enable_drop_mode\"}");
                    EvacMapStatusBar.Text = "Click within your barangay boundary to place a center  (click again to cancel)";
                }
                else
                {
                    EvacMapWebView.CoreWebView2.PostWebMessageAsString("{\"type\":\"cancel_drop_mode\"}");
                    EvacMapStatusBar.Text = "Drop mode cancelled.";
                }
            }
        }

        private void EvacMap_ConfirmAdd_Click(object sender, RoutedEventArgs e)
        {
            string name   = OverlayNewName.Text.Trim();
            string capStr = OverlayNewCapacity.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            { OverlayNewError.Text = "Name is required."; OverlayNewError.Visibility = Visibility.Visible; return; }
            if (!int.TryParse(capStr, out int cap) || cap < 1)
            { OverlayNewError.Text = "Capacity must be a positive number."; OverlayNewError.Visibility = Visibility.Visible; return; }

            OverlayNewError.Visibility = Visibility.Collapsed;
            var center = new EvacuationCenter {
                Name=name, Barangay=SessionManager.AssignedBarangay,
                GPSLat=_pendingPinLat, GPSLong=_pendingPinLng,
                Capacity=cap, CurrentOccupancy=0, CenterType="Barangay"
            };
            int newId = _evacRepo.Create(center);
            _auditRepo.Log(SessionManager.UserID, "CREATE", "Evacuation_Centers", newId,
                $"Map-placed: {name}, cap {cap}, ({_pendingPinLat:F5},{_pendingPinLng:F5})");

            OverlayAddCenterForm.Visibility = Visibility.Collapsed;
            EvacMap_SendCenters();
            BuildCentersMini(OverviewCentersPanel);
            ShowToast($"\"{name}\" placed. Click the pin → CHOOSE CENTER to activate.");
            EvacMapStatusBar.Text = $"\"{name}\" saved (capacity {cap}) — click pin to activate.";
        }

        private void EvacMap_CancelAdd_Click(object sender, RoutedEventArgs e)
        {
            _mapDropActive = false;
            OverlayAddCenterForm.Visibility = Visibility.Collapsed;
            if (_evacMapReady)
                EvacMapWebView.CoreWebView2.PostWebMessageAsString("{\"type\":\"cancel_drop_mode\"}");
        }

        private void EvacMap_ConfirmOcc_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(OverlayOccInput.Text.Trim(), out int occ) || occ < 0)
            { OverlayOccError.Text = "Enter a valid number (0 or more)."; OverlayOccError.Visibility = Visibility.Visible; return; }
            if (occ > _mapOccCapacity)
            { OverlayOccError.Text = $"Cannot exceed capacity ({_mapOccCapacity})."; OverlayOccError.Visibility = Visibility.Visible; return; }

            OverlayOccError.Visibility = Visibility.Collapsed;
            _evacRepo.UpdateOccupancy(_mapOccCenterId, occ, _mapOccCapacity);
            _auditRepo.Log(SessionManager.UserID, "UPDATE", "Evacuation_Centers", _mapOccCenterId, $"Occupancy → {occ}");
            OverlayOccForm.Visibility = Visibility.Collapsed;
            RefreshAllData();
            EvacMap_SendCenters();
            BuildCentersMini(OverviewCentersPanel);
            ShowToast("Occupancy updated.");
            EvacMapStatusBar.Text = $"Occupancy updated to {occ}";
        }

        private void EvacMap_CancelOcc_Click(object sender, RoutedEventArgs e)
            => OverlayOccForm.Visibility = Visibility.Collapsed;

        private void EvacMap_RequestExternal_Click(object sender, RoutedEventArgs e)
        {
            var brgy      = SessionManager.AssignedBarangay;
            var neighbors = _evacRepo.GetNeighboringAvailable(brgy);
            static object ToMapObj(EvacuationCenter c) => new {
                id=c.CenterID, name=c.Name, barangay=c.Barangay,
                lat=c.GPSLat, lng=c.GPSLong,
                capacity=c.Capacity, occupancy=c.CurrentOccupancy, isFull=c.IsFull,
                inUse=c.CurrentOccupancy > 0, isChosen=true
            };
            if (_evacMapReady)
            {
                var cityInRequest = _evacRepo.GetCityLevel();
                var chosenIds = _evacRepo.GetChosenCenterIds(brgy);
                object ToCityObj(EvacuationCenter c) => new {
                    id=c.CenterID, name=c.Name, barangay=c.Barangay,
                    lat=c.GPSLat, lng=c.GPSLong,
                    capacity=c.Capacity, occupancy=c.CurrentOccupancy, isFull=c.IsFull,
                    inUse=c.CurrentOccupancy > 0, isChosen=chosenIds.Contains(c.CenterID)
                };
                EvacMapWebView.CoreWebView2.PostWebMessageAsString(
                    JsonSerializer.Serialize(new { type="load_neighbors", neighbors=neighbors.Select(ToMapObj) }));
                EvacMapWebView.CoreWebView2.PostWebMessageAsString(
                    JsonSerializer.Serialize(new {
                        type="load_centers",
                        own   = System.Array.Empty<object>(),
                        cross = System.Array.Empty<object>(),
                        city  = cityInRequest.Select(c => ToCityObj(c))
                    }));
                BtnExitRequestMode.Visibility = Visibility.Visible;
                EvacMapStatusBar.Text = $"REQUEST MODE — {neighbors.Count} neighbor + {cityInRequest.Count} city center(s) shown";
            }
        }

        private void EvacMap_ExitRequestMode_Click(object sender, RoutedEventArgs e)
        {
            if (_evacMapReady)
            {
                EvacMapWebView.CoreWebView2.PostWebMessageAsString("{\"type\":\"exit_request_mode\"}");
                EvacMap_SendCenters();
            }
            BtnExitRequestMode.Visibility = Visibility.Collapsed;
            EvacMapStatusBar.Text         = "MAP READY";
        }

        private void SubmitCrossRequest(int centerId, string centerName, string barangay)
        {
            var myBrgy = SessionManager.AssignedBarangay;

            if (_crossRepo.HasPendingRequest(myBrgy, centerId))
            {
                ShowToast($"A pending request for \"{centerName}\" already exists.");
                return;
            }

            int reqId = _crossRepo.SendRequest(SessionManager.UserID, myBrgy, centerId);
            _auditRepo.Log(SessionManager.UserID, "CREATE", "Cross_Barangay_Requests", reqId,
                $"Requested center #{centerId} ({centerName}) from Brgy. {barangay}");

            if (_evacMapReady)
                EvacMapWebView.CoreWebView2.PostWebMessageAsString("{\"type\":\"exit_request_mode\"}");

            BuildCentersRequestPanels();
            ShowToast($"Request sent for \"{centerName}\" (Brgy. {barangay}).");
            EvacMapStatusBar.Text = $"Request submitted — waiting for Brgy. {barangay} official to respond";
        }


        private void RemoveCenter_Click(EvacuationCenter center)
        {
            var families = _familyRepo.GetByCenter(center.CenterID);
            string msg = families.Count > 0
                ? $"Remove \"{center.Name}\" from active use?\n\n{families.Count} family(ies) will be unassigned from this center.\nThe center will remain on the map (dimmed) and can be chosen again."
                : $"Remove \"{center.Name}\" from active use?\n\nNo families are assigned. The center will remain on the map (dimmed).";

            ShowWarning(msg, "REMOVE", () =>
            {
                foreach (var fam in families)
                    if (fam.EvacuationCenterID.HasValue)
                        _familyRepo.UnassignCenter(fam.FamilyID, fam.MemberCount, fam.EvacuationCenterID.Value);
                _evacRepo.UpdateOccupancy(center.CenterID, 0, center.Capacity);
                _evacRepo.UnmarkChosen(center.CenterID, SessionManager.AssignedBarangay);
                _auditRepo.Log(SessionManager.UserID, "UPDATE", "Evacuation_Centers", center.CenterID,
                    $"Removed from active use — {families.Count} families unassigned, occupancy reset to 0, chosen record cleared");
                RefreshAllData();
                BuildCentersSection();
                BuildCentersMini(OverviewCentersPanel);
                if (_evacMapReady) EvacMap_SendCenters();
                ShowToast($"\"{center.Name}\" removed — {families.Count} families unassigned. Pin still on map (dimmed).");
            });
        }

        private void ReleaseCenter_Click(EvacuationCenter center)
        {
            var myBrgy = SessionManager.AssignedBarangay;
            var myFamilies = _familyRepo.GetByCenter(center.CenterID)
                .Where(f => _incidents.Any(i => i.IncidentID == f.IncidentID && i.Barangay == myBrgy))
                .ToList();

            if (myFamilies.Count == 0)
            {
                if (center.CenterType == "City")
                {
                    ShowWarning($"No families from Brgy. {myBrgy} are assigned to \"{center.Name}\".\n\n" +
                                $"Clear your barangay's opt-in for this city center?",
                                "CLEAR OPT-IN", () =>
                    {
                        _evacRepo.UnmarkChosen(center.CenterID, myBrgy);
                        _auditRepo.Log(SessionManager.UserID, "DELETE", "Barangay_Chosen_Centers", center.CenterID,
                            $"Cleared opt-in for city center \"{center.Name}\"");
                        RefreshAllData();
                        BuildCentersSection();
                        BuildCentersMini(OverviewCentersPanel);
                        if (_evacMapReady) EvacMap_SendCenters();
                        ShowToast($"Cleared opt-in for \"{center.Name}\".");
                    });
                }
                else
                {
                    ShowWarning($"No families from Brgy. {myBrgy} are assigned to \"{center.Name}\".",
                                "OK — CLOSE", () => { });
                }
                return;
            }

            string msg = $"Release \"{center.Name}\" from Brgy. {myBrgy}?\n\n" +
                         $"{myFamilies.Count} family(ies) from your barangay will be unassigned.\n" +
                         $"Other barangays using this center are unaffected.\n\n" +
                         $"The center will remain on the map and can be chosen again later.";

            ShowWarning(msg, "RELEASE", () =>
            {
                int membersUnassigned = _familyRepo.UnassignAllFromCenterByBarangay(center.CenterID, myBrgy);
                if (center.CenterType == "City")
                    _evacRepo.UnmarkChosen(center.CenterID, myBrgy);
                _auditRepo.Log(SessionManager.UserID, "UPDATE", "Evacuation_Centers", center.CenterID,
                    $"Released by Brgy. {myBrgy} — {myFamilies.Count} families, {membersUnassigned} members unassigned" +
                    (center.CenterType == "City" ? ", chosen record cleared" : ""));
                RefreshAllData();
                BuildCentersSection();
                BuildCentersMini(OverviewCentersPanel);
                BuildFamiliesSection();
                if (_evacMapReady) EvacMap_SendCenters();
                ShowToast($"Released \"{center.Name}\" — {myFamilies.Count} family(ies) unassigned.");
            });
        }

        private void DeleteCenter_Click(EvacuationCenter center)
        {
            var families = _familyRepo.GetByCenter(center.CenterID);
            string msg = families.Count > 0
                ? $"Permanently delete \"{center.Name}\"?\n\n! {families.Count} family(ies) are still assigned here.\nUnassign or delete all families first before deleting the center."
                : $"Permanently delete \"{center.Name}\"?\n\nThis will remove the center from the map and database. This cannot be undone.";

            if (families.Count > 0)
            {
                ShowWarning(msg, "OK — CLOSE", () => { });
                return;
            }

            ShowWarning(msg, "DELETE PERMANENTLY", () =>
            {
                var (success, warning) = _evacRepo.DeleteBarangayCenter(center.CenterID);
                if (!success)
                {
                    ShowWarning(warning, "OK — CLOSE", () => { });
                    return;
                }
                _auditRepo.Log(SessionManager.UserID, "DELETE", "Evacuation_Centers", center.CenterID,
                    $"Permanently deleted center: {center.Name}");
                RefreshAllData();
                BuildCentersSection();
                BuildCentersMini(OverviewCentersPanel);
                if (_evacMapReady) EvacMap_SendCenters();
                ShowToast($"\"{center.Name}\" permanently deleted." + warning);
            });
        }

        private void BuildCentersRequestPanels()
        {
            var brgy = SessionManager.AssignedBarangay;

            CentersIncomingPanel.Children.Clear();
            var incoming = _crossRepo.GetIncomingActive(brgy);
            var activeIncoming = incoming.Where(r => r.Status == "Pending" || r.Status == "Approved").ToList();

            if (activeIncoming.Count > 0)
            {
                var blockBtn = new Button
                {
                    Style = (Style)Resources["GhostBtnStyle"],
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(0, 0, 0, 8),
                    Tag = brgy
                };
                blockBtn.Content = new TextBlock
                {
                    Text = $"x BLOCK ALL INCOMING ({activeIncoming.Count})",
                    FontFamily = new FontFamily("Consolas"), FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 192, 80, 80)),
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold, CharacterSpacing = 80,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                blockBtn.Click += (s, _) => BlockAllIncoming_Click((string)((Button)s).Tag!);
                CentersIncomingPanel.Children.Add(blockBtn);
            }

            if (incoming.Count == 0)
                CentersIncomingPanel.Children.Add(new TextBlock {
                    Text = "No incoming requests", FontFamily = new FontFamily("Consolas"),
                    FontSize = 9, Foreground = new SolidColorBrush(Color.FromArgb(255, 42, 63, 82))
                });
            else
                foreach (var req in incoming)
                    CentersIncomingPanel.Children.Add(BuildRequestCard(req, incoming: true));

            CentersOutgoingPanel.Children.Clear();
            var outgoing = _crossRepo.GetOutgoing(brgy);
            if (outgoing.Count == 0)
                CentersOutgoingPanel.Children.Add(new TextBlock {
                    Text = "No outgoing requests", FontFamily = new FontFamily("Consolas"),
                    FontSize = 9, Foreground = new SolidColorBrush(Color.FromArgb(255, 42, 63, 82))
                });
            else
                foreach (var req in outgoing)
                    CentersOutgoingPanel.Children.Add(BuildRequestCard(req, incoming: false));
        }

        private void BlockAllIncoming_Click(string ownerBrgy)
        {
            var active   = _crossRepo.GetIncomingActive(ownerBrgy);
            int pending  = active.Count(r => r.Status == "Pending");
            int approved = active.Count(r => r.Status == "Approved");

            string msg = $"Block all incoming access to your centers in Brgy. {ownerBrgy}?\n\n" +
                         $"  Pending requests to reject: {pending}\n" +
                         $"  Approved grants to revoke:  {approved}\n\n" +
                         $"All families from other barangays currently assigned to your centers will be unassigned.\n\n" +
                         $"The other barangays can re-request afterwards.";

            ShowWarning(msg, "BLOCK ALL INCOMING", () =>
            {
                var pairs = _crossRepo.GetApprovedIncomingPairs(ownerBrgy);

                int totalUnassigned = 0;
                foreach (var (centerId, requesterBrgy) in pairs)
                    totalUnassigned += _familyRepo.UnassignAllFromCenterByBarangay(centerId, requesterBrgy);

                int revoked = _crossRepo.RevokeAllIncoming(ownerBrgy);

                _auditRepo.Log(SessionManager.UserID, "UPDATE", "Cross_Barangay_Requests", 0,
                    $"BLOCK ALL INCOMING: revoked {revoked} request(s), unassigned {totalUnassigned} member(s) across {pairs.Count} center(s) in Brgy. {ownerBrgy}");

                RefreshAllData();
                BuildCentersSection();
                BuildCentersMini(OverviewCentersPanel);
                BuildFamiliesSection();
                if (_evacMapReady) EvacMap_SendCenters();
                ShowToast($"Blocked all incoming — {revoked} request(s) revoked, {totalUnassigned} member(s) unassigned.");
            });
        }

        private UIElement BuildRequestCard(CrossBarangayRequest req, bool incoming)
        {
            var statusColor = req.Status switch
            {
                "Approved" => Color.FromArgb(255, 76, 175, 80),
                "Rejected" => Color.FromArgb(255, 192, 80, 80),
                _          => Color.FromArgb(255, 255, 193, 7)
            };

            var wrapper = new Grid { Margin = new Thickness(0, 0, 0, 4) };

            var card = new Border
            {
                Background      = new SolidColorBrush(Color.FromArgb(255, 9, 19, 28)),
                BorderThickness = new Thickness(1, 0, 0, 0),
                BorderBrush     = new SolidColorBrush(statusColor),
                Padding         = new Thickness(10, 8, 10, 8)
            };
            var sp = new StackPanel { Spacing = 5 };

            var headerRow = new Grid();
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleText = new TextBlock
            {
                Text = incoming
                    ? $"FROM: Brgy. {req.RequesterBarangay}"
                    : $"TO: {req.TargetCenterName}",
                FontFamily = new FontFamily("Consolas"), FontSize = 9,
                Foreground = new SolidColorBrush(White), FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(titleText, 0);
            headerRow.Children.Add(titleText);

            if (req.Status == "Rejected")
            {
                var xBtn = new Button
                {
                    Content = new TextBlock
                    {
                        Text = "×", FontFamily = new FontFamily("Consolas"), FontSize = 11,
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 100, 120, 140))
                    },
                    Background      = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                    BorderThickness = new Thickness(0),
                    Padding         = new Thickness(4, 0, 0, 0),
                    VerticalAlignment   = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                xBtn.Click += (_, _) =>
                {
                    _crossRepo.DeleteRequest(req.RequestID);
                    if (wrapper.Parent is Panel panel)
                        panel.Children.Remove(wrapper);
                };
                Grid.SetColumn(xBtn, 1);
                headerRow.Children.Add(xBtn);
            }

            sp.Children.Add(headerRow);

            sp.Children.Add(new TextBlock
            {
                Text = incoming
                    ? $"Wants: {req.TargetCenterName}"
                    : $"Brgy. {req.TargetBarangay}",
                FontFamily = new FontFamily("Consolas"), FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 58, 85, 112))
            });

            var statusRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            statusRow.Children.Add(new TextBlock
            {
                Text = req.Status, FontFamily = new FontFamily("Consolas"), FontSize = 8,
                Foreground = new SolidColorBrush(statusColor), FontWeight = Microsoft.UI.Text.FontWeights.Bold
            });
            if (!string.IsNullOrWhiteSpace(req.Reason))
                statusRow.Children.Add(new TextBlock
                {
                    Text = $"— {req.Reason}", FontFamily = new FontFamily("Consolas"), FontSize = 8,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 58, 85, 112))
                });
            sp.Children.Add(statusRow);

            if (incoming && req.Status == "Pending")
            {
                var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

                var approveBtn = new Button { Style = (Style)Resources["PrimaryBtnStyle"] };
                approveBtn.Content = new TextBlock { Text = "APPROVE", FontFamily = new FontFamily("Consolas"),
                    FontSize = 8, Foreground = new SolidColorBrush(White), CharacterSpacing = 60 };
                approveBtn.Click += (_, _) =>
                {
                    _crossRepo.Approve(req.RequestID);
                    _auditRepo.Log(SessionManager.UserID, "UPDATE", "Cross_Barangay_Requests", req.RequestID,
                        $"Approved request from Brgy. {req.RequesterBarangay}");
                    RefreshAllData();
                    if (_evacMapReady) EvacMap_SendCenters();
                    BuildCentersRequestPanels();
                    ShowToast($"Approved request from Brgy. {req.RequesterBarangay}.");
                };

                var rejectBtn = new Button { Style = (Style)Resources["GhostBtnStyle"] };
                rejectBtn.Content = new TextBlock { Text = "REJECT", FontFamily = new FontFamily("Consolas"),
                    FontSize = 8, Foreground = new SolidColorBrush(Color.FromArgb(255, 192, 80, 80)), CharacterSpacing = 60 };
                rejectBtn.Click += (_, _) =>
                {
                    _crossRepo.Reject(req.RequestID, "Rejected by barangay official");
                    _auditRepo.Log(SessionManager.UserID, "UPDATE", "Cross_Barangay_Requests", req.RequestID,
                        $"Rejected request from Brgy. {req.RequesterBarangay}");
                    BuildCentersRequestPanels();
                    ShowToast($"Rejected request from Brgy. {req.RequesterBarangay}.");
                };

                btnRow.Children.Add(approveBtn);
                btnRow.Children.Add(rejectBtn);
                sp.Children.Add(btnRow);
            }

            if (!incoming && req.Status == "Pending")
            {
                var cancelBtn = new Button { Style = (Style)Resources["GhostBtnStyle"],
                    HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 4, 0, 0) };
                cancelBtn.Content = new TextBlock
                {
                    Text = "CANCEL REQUEST", FontFamily = new FontFamily("Consolas"), FontSize = 8,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 192, 80, 80)),
                    CharacterSpacing = 60, HorizontalAlignment = HorizontalAlignment.Center
                };
                cancelBtn.Click += (_, _) =>
                {
                    _crossRepo.Reject(req.RequestID, "Cancelled by requester");
                    _auditRepo.Log(SessionManager.UserID, "UPDATE", "Cross_Barangay_Requests", req.RequestID,
                        $"Cancelled outgoing request to {req.TargetCenterName} (Brgy. {req.TargetBarangay})");
                    RefreshAllData();
                    if (_evacMapReady) EvacMap_SendCenters();
                    BuildCentersRequestPanels();
                    ShowToast($"Request to {req.TargetCenterName} cancelled.");
                };
                sp.Children.Add(cancelBtn);
            }

            card.Child = sp;
            wrapper.Children.Add(card);
            return wrapper;
        }


        private void OpenRegisterFamilyDialog_Click(object sender, RoutedEventArgs e)
        {
            IncidentPicker.Items.Clear();
            IncidentPicker.Items.Add(new ComboBoxItem { Content = "— Select incident —", Tag = 0 });
            foreach (var inc in _incidents)
                IncidentPicker.Items.Add(new ComboBoxItem
                {
                    Content = $"#{inc.IncidentID} · {inc.Sitio} ({inc.Status})",
                    Tag     = inc.IncidentID
                });
            IncidentPicker.SelectedIndex = 0;

            EvacCenterPicker.Items.Clear();
            EvacCenterPicker.Items.Add(new ComboBoxItem { Content = "— None / TBD —", Tag = 0 });

            var myBrgy  = SessionManager.AssignedBarangay;
            var seenIds = new HashSet<int>();
            foreach (var c in _centers.Where(c => !c.IsFull))
            {
                if (!seenIds.Add(c.CenterID)) continue;
                string label;
                if (c.CenterType == "City")
                    label = $"{c.Name} [CITY-LEVEL] ({c.AvailableSlots} slots)";
                else if (c.CenterType == "Barangay" && c.Barangay != myBrgy)
                    label = $"{c.Name} [CROSS-BRGY · {c.Barangay}] ({c.AvailableSlots} slots)";
                else
                    label = $"{c.Name} ({c.AvailableSlots} slots)";
                EvacCenterPicker.Items.Add(new ComboBoxItem { Content = label, Tag = c.CenterID });
            }
            EvacCenterPicker.SelectedIndex = 0;

            HeadNameInput.Text               = "";
            MemberCountInput.Text            = "";
            RegisterErrorMsg.Visibility      = Visibility.Collapsed;
            RepeatWarningBanner.Visibility   = Visibility.Collapsed;
            RegisterFamilyOverlay.Visibility = Visibility.Visible;
        }

        private void CloseRegisterFamilyDialog_Click(object sender, RoutedEventArgs e)
            => RegisterFamilyOverlay.Visibility = Visibility.Collapsed;

        private void SubmitRegisterFamily_Click(object sender, RoutedEventArgs e)
        {
            int    incidentId = IncidentPicker.SelectedItem is ComboBoxItem ci ? (int)(ci.Tag ?? 0) : 0;
            string headName   = HeadNameInput.Text.Trim();
            string memberStr  = MemberCountInput.Text.Trim();
            int?   centerId   = EvacCenterPicker.SelectedItem is ComboBoxItem cc ? (int)(cc.Tag ?? 0) : 0;
            if (centerId == 0) centerId = null;

            if (incidentId == 0)                                       { ShowError(RegisterErrorMsg, "Please select an incident."); return; }
            if (string.IsNullOrWhiteSpace(headName))                   { ShowError(RegisterErrorMsg, "Head of family name is required."); return; }
            if (!int.TryParse(memberStr, out int members) || members < 1) { ShowError(RegisterErrorMsg, "Member count must be a positive number."); return; }

            EvacuationCenter? target = null;
            if (centerId.HasValue)
            {
                target = _evacRepo.GetById(centerId.Value);
                if (target == null)
                { ShowError(RegisterErrorMsg, "Selected center not found."); return; }
                if (target.AvailableSlots < members)
                {
                    ShowError(RegisterErrorMsg,
                        $"Not enough capacity — {target.Name} has {target.AvailableSlots} slot(s) available, " +
                        $"family has {members} member(s).");
                    return;
                }
            }

            RegisterErrorMsg.Visibility = Visibility.Collapsed;
            try
            {
            bool isRepeat = _familyRepo.IsRepeatDisplaced(headName, incidentId);

            var family = new Family
            {
                IncidentID = incidentId, HeadName = headName, MemberCount = members,
                EvacuationCenterID = centerId, ReliefStatus = "Pending", IsRepeatDisplaced = isRepeat
            };
            int newId = _familyRepo.Create(family);

            _auditRepo.Log(SessionManager.UserID, "CREATE", "Families", newId,
                $"Registered family: {headName}, {members} members, Incident #{incidentId}");

            RegisterFamilyOverlay.Visibility = Visibility.Collapsed;
            RefreshAllData();
            ShowSection("Families");
            ShowToast($"Family \"{headName}\" registered{(isRepeat ? " — flagged as repeat" : "")}.");
            }
            catch (Exception ex)
            {
                ShowError(RegisterErrorMsg, $"Could not register family: {ex.Message}");
            }
        }


        private void OpenAddCenterDialog_Click(object sender, RoutedEventArgs e)
        {
            CenterNameInput.Text         = "";
            CenterCapacityInput.Text     = "";
            AddCenterErrorMsg.Visibility = Visibility.Collapsed;
            AddCenterOverlay.Visibility  = Visibility.Visible;
        }

        private void CloseAddCenterDialog_Click(object sender, RoutedEventArgs e)
            => AddCenterOverlay.Visibility = Visibility.Collapsed;

        private void SubmitAddCenter_Click(object sender, RoutedEventArgs e)
        {
            string name    = CenterNameInput.Text.Trim();
            string capStr  = CenterCapacityInput.Text.Trim();
            string barangay = SessionManager.AssignedBarangay;

            if (string.IsNullOrWhiteSpace(name))
                { ShowError(AddCenterErrorMsg, "Center name is required."); return; }
            if (!int.TryParse(capStr, out int cap) || cap < 1)
                { ShowError(AddCenterErrorMsg, "Capacity must be a positive number."); return; }

            AddCenterErrorMsg.Visibility = Visibility.Collapsed;

            var center = new EvacuationCenter
            {
                Name = name, Barangay = string.IsNullOrWhiteSpace(barangay) ? "Cebu City" : barangay,
                Capacity = cap, CurrentOccupancy = 0, CenterType = "Barangay"
            };
            int newId = _evacRepo.Create(center);
            _evacRepo.MarkAsChosen(newId, center.Barangay);
            _auditRepo.Log(SessionManager.UserID, "CREATE", "Evacuation_Centers", newId,
                $"Added center: {name}, capacity {cap}, marked chosen");

            AddCenterOverlay.Visibility = Visibility.Collapsed;
            RefreshAllData();
            ShowSection("Centers");
            ShowToast($"Center \"{name}\" added.");
        }


        private void OpenOccupancyDialog(EvacuationCenter center)
        {
            _selectedCenterIdForOccupancy = center.CenterID;
            _selectedCenterCapacity       = center.Capacity;
            OccupancyCenterLabel.Text     = center.Name;
            OccupancyInput.Text           = center.CurrentOccupancy.ToString();
            OccupancyCapacityHint.Text    = $"Capacity: {center.Capacity} persons";
            OccupancyErrorMsg.Visibility  = Visibility.Collapsed;
            UpdateOccupancyOverlay.Visibility = Visibility.Visible;
        }

        private void CloseOccupancyDialog_Click(object sender, RoutedEventArgs e)
            => UpdateOccupancyOverlay.Visibility = Visibility.Collapsed;

        private void SubmitOccupancy_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(OccupancyInput.Text.Trim(), out int occ) || occ < 0)
                { ShowError(OccupancyErrorMsg, "Please enter a valid number (0 or more)."); return; }
            if (occ > _selectedCenterCapacity)
                { ShowError(OccupancyErrorMsg, $"Cannot exceed capacity ({_selectedCenterCapacity})."); return; }

            OccupancyErrorMsg.Visibility = Visibility.Collapsed;
            _evacRepo.UpdateOccupancy(_selectedCenterIdForOccupancy, occ, _selectedCenterCapacity);
            _auditRepo.Log(SessionManager.UserID, "UPDATE", "Evacuation_Centers",
                _selectedCenterIdForOccupancy, $"Occupancy → {occ}");

            UpdateOccupancyOverlay.Visibility = Visibility.Collapsed;
            RefreshAllData();
            BuildCentersSection();
            BuildCentersMini(OverviewCentersPanel);
            ShowToast("Occupancy updated.");
        }


        private static double[][] GetCebuCityBoundary() => new double[][]
        {
            [10.493494,123.887977],[10.490852,123.895334],[10.487373,123.901598],
            [10.485117,123.913721],[10.479028,123.918555],[10.476279,123.919679],
            [10.474151,123.919717],[10.473296,123.919984],[10.472653,123.921488],
            [10.472695,123.922731],[10.473487,123.923588],[10.473600,123.923885],
            [10.473598,123.924361],[10.473422,123.924816],[10.472554,123.925094],
            [10.471264,123.925217],[10.453212,123.925019],[10.434836,123.923037],
            [10.428896,123.924317],[10.410396,123.923413],[10.405157,123.921716],
            [10.400814,123.924156],[10.398287,123.923867],[10.398240,123.923493],
            [10.386649,123.924130],[10.383341,123.923180],[10.383082,123.923889],
            [10.382548,123.923933],[10.380852,123.924620],[10.379678,123.924171],
            [10.378069,123.924205],[10.377358,123.925385],[10.375241,123.925540],
            [10.374636,123.926061],[10.374617,123.926204],[10.373755,123.926633],
            [10.373821,123.926830],[10.372155,123.926488],[10.371134,123.926520],
            [10.370803,123.926792],[10.367682,123.923741],[10.366404,123.924504],
            [10.366602,123.924894],[10.366354,123.925317],[10.365998,123.925032],
            [10.365443,123.925185],[10.364944,123.925007],[10.364357,123.925210],
            [10.363263,123.925041],[10.361940,123.925765],[10.360982,123.922209],
            [10.359897,123.920749],[10.358136,123.919063],[10.357694,123.919075],
            [10.350277,123.915472],[10.339723,123.912933],[10.338121,123.916869],
            [10.337091,123.917289],[10.336796,123.917227],[10.336711,123.917330],
            [10.336114,123.917080],[10.335554,123.917321],[10.335281,123.918380],
            [10.334854,123.918762],[10.334374,123.918822],[10.334200,123.918717],
            [10.333054,123.919528],[10.334064,123.920139],[10.333312,123.922126],
            [10.330902,123.921309],[10.291224,123.907059],[10.291271,123.904031],
            [10.290598,123.902918],[10.290925,123.902778],[10.290268,123.900955],
            [10.289985,123.901049],[10.289806,123.900654],[10.289642,123.900619],
            [10.289568,123.900351],[10.290062,123.899756],[10.289912,123.899359],
            [10.290223,123.899026],[10.289642,123.898610],[10.288712,123.897012],
            [10.288668,123.896449],[10.289310,123.896475],[10.289455,123.894147],
            [10.288996,123.893796],[10.288871,123.893238],[10.289326,123.892498],
            [10.289326,123.891162],[10.289877,123.890947],[10.289804,123.890588],
            [10.288202,123.890676],[10.288175,123.888236],[10.287521,123.887238],
            [10.285900,123.887549],[10.279630,123.881959],[10.273934,123.879819],
            [10.273293,123.880278],[10.273235,123.880685],[10.273288,123.880833],
            [10.272485,123.881946],[10.272522,123.882482],[10.272395,123.882590],
            [10.271506,123.882493],[10.270414,123.881455],[10.269511,123.881694],
            [10.260530,123.873964],[10.260393,123.874111],[10.259926,123.873854],
            [10.259514,123.874336],[10.259176,123.874055],[10.263518,123.869173],
            [10.263018,123.868798],[10.262670,123.868961],[10.262522,123.868267],
            [10.262258,123.868218],[10.262080,123.866871],[10.262508,123.866448],
            [10.261178,123.860014],[10.258828,123.858523],[10.259093,123.858071],
            [10.262649,123.855995],[10.263127,123.855986],[10.263061,123.854857],
            [10.262940,123.854550],[10.263160,123.853971],[10.263095,123.853808],
            [10.263103,123.852726],[10.263208,123.852368],[10.263020,123.851960],
            [10.263150,123.851574],[10.263470,123.851205],[10.263615,123.851300],
            [10.268267,123.851717],[10.269741,123.849418],[10.269909,123.848389],
            [10.270145,123.847844],[10.272411,123.847077],[10.272659,123.847676],
            [10.273796,123.847683],[10.273813,123.847600],[10.274049,123.847510],
            [10.274158,123.847347],[10.274202,123.847084],[10.274497,123.846882],
            [10.275992,123.846344],[10.276213,123.845565],[10.276741,123.845478],
            [10.278647,123.844182],[10.279080,123.843382],[10.280860,123.841296],
            [10.280429,123.840614],[10.281270,123.838105],[10.282244,123.837730],
            [10.282275,123.837481],[10.281800,123.836588],[10.284200,123.835404],
            [10.284604,123.830297],[10.292971,123.828579],[10.296567,123.826076],
            [10.313848,123.820202],[10.320490,123.821026],[10.322402,123.820283],
            [10.323290,123.820637],[10.323582,123.820213],[10.324517,123.820320],
            [10.329531,123.815459],[10.328791,123.814500],[10.327692,123.814353],
            [10.327517,123.813683],[10.329358,123.811356],[10.328320,123.809626],
            [10.327645,123.809716],[10.327291,123.809269],[10.328376,123.808252],
            [10.330563,123.809371],[10.332229,123.806500],[10.338039,123.791530],
            [10.329806,123.788934],[10.327653,123.775850],[10.348866,123.776176],
            [10.364748,123.770173],[10.386035,123.769502],[10.407871,123.785034],
            [10.410283,123.793900],[10.421038,123.801071],[10.430967,123.804544],
            [10.429887,123.805251],[10.431635,123.805404],[10.432043,123.806189],
            [10.432902,123.806219],[10.433603,123.807329],[10.435368,123.807332],
            [10.435990,123.808568],[10.436863,123.808735],[10.439531,123.814069],
            [10.439217,123.814828],[10.441269,123.817808],[10.439812,123.818915],
            [10.443009,123.822415],[10.443950,123.825618],[10.446604,123.827468],
            [10.448597,123.829388],[10.448775,123.831605],[10.451845,123.834521],
            [10.452100,123.835130],[10.455418,123.837882],[10.454828,123.838694],
            [10.488366,123.867084],[10.493494,123.887977]
        };


        private void ShowWarning(string message, string confirmLabel, Action onConfirm)
        {
            WarningMessage.Text = message;
            WarningConfirmLabel.Text = confirmLabel;
            _warningConfirmAction = onConfirm;
            WarningOverlay.Visibility = Visibility.Visible;
        }

        private void CloseWarningOverlay_Click(object sender, RoutedEventArgs e)
        {
            _warningConfirmAction = null;
            WarningOverlay.Visibility = Visibility.Collapsed;
        }

        private void ConfirmWarning_Click(object sender, RoutedEventArgs e)
        {
            WarningOverlay.Visibility = Visibility.Collapsed;
            var action = _warningConfirmAction;
            _warningConfirmAction = null;
            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                ShowToast($"! Operation failed: {ex.Message}");
                RefreshAllData();
                ShowSection(_currentSection);
            }
        }


        private void OpenCenterDetailsOverlay(EvacuationCenter center)
        {
            CenterDetailsTitle.Text    = center.Name.ToUpper();
            CenterDetailsSubtitle.Text = $"Brgy. {center.Barangay}  ·  Capacity: {center.Capacity}  ·  Occupied: {center.CurrentOccupancy}  ·  Available: {center.AvailableSlots}";
            CenterDetailsFamiliesPanel.Children.Clear();

            var families = _familyRepo.GetByCenterWithBarangay(center.CenterID);
            if (families.Count == 0)
            {
                CenterDetailsFamiliesPanel.Children.Add(MakeEmptyState(
                    "No families assigned to this center",
                    "Assign families via the Affected Families section",
                    new Thickness(18, 28, 18, 28)));
            }
            else
            {
                foreach (var fam in families)
                {
                    var reliefColor = fam.ReliefStatus == "Received"
                        ? Color.FromArgb(255, 76, 175, 80)
                        : Color.FromArgb(255, 255, 193, 7);
                    var reliefBg = fam.ReliefStatus == "Received"
                        ? Color.FromArgb(40, 76, 175, 80)
                        : Color.FromArgb(40, 255, 193, 7);

                    var row = new Border
                    {
                        Padding = new Thickness(18, 10, 18, 10),
                        BorderThickness = new Thickness(0, 1, 0, 0),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(255, 23, 36, 53))
                    };
                    var grid = new Grid { ColumnSpacing = 8 };
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });

                    var nameBlock = new TextBlock
                    {
                        Text = fam.HeadName, FontFamily = new FontFamily("Consolas"),
                        FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        Foreground = new SolidColorBrush(White), VerticalAlignment = VerticalAlignment.Center
                    };
                    var membersBlock = new TextBlock
                    {
                        Text = fam.MemberCount.ToString(), FontFamily = new FontFamily("Consolas"),
                        FontSize = 11, Foreground = new SolidColorBrush(Color.FromArgb(255, 122, 155, 184)),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    var reliefBadge = new Border
                    {
                        Background = new SolidColorBrush(reliefBg), Padding = new Thickness(6, 3, 6, 3),
                        HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center,
                        Child = new TextBlock
                        {
                            Text = fam.ReliefStatus.ToUpper(), FontFamily = new FontFamily("Consolas"),
                            FontSize = 8, Foreground = new SolidColorBrush(reliefColor),
                            FontWeight = Microsoft.UI.Text.FontWeights.Bold
                        }
                    };
                    var barangayBlock = new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(fam.Barangay) ? "—" : fam.Barangay,
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 10,
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 74, 142, 194)),
                        VerticalAlignment = VerticalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap
                    };

                    Grid.SetColumn(nameBlock,     0); grid.Children.Add(nameBlock);
                    Grid.SetColumn(membersBlock,  1); grid.Children.Add(membersBlock);
                    Grid.SetColumn(reliefBadge,   2); grid.Children.Add(reliefBadge);
                    Grid.SetColumn(barangayBlock, 3); grid.Children.Add(barangayBlock);
                    row.Child = grid;
                    CenterDetailsFamiliesPanel.Children.Add(row);
                }
            }
            CenterDetailsOverlay.Visibility = Visibility.Visible;
        }

        private void CloseCenterDetailsOverlay_Click(object sender, RoutedEventArgs e)
            => CenterDetailsOverlay.Visibility = Visibility.Collapsed;

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
            var sp     = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Spacing = 6 };
            sp.Children.Add(new TextBlock
            {
                Text = main, FontFamily = new FontFamily("Consolas"), FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 58, 85, 112)),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            sp.Children.Add(new TextBlock
            {
                Text = sub, FontFamily = new FontFamily("Consolas"), FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 30, 48, 66)),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            border.Child = sp;
            return border;
        }


        private void NavAnalysis_Click(object sender, RoutedEventArgs e)
        {
            RefreshAllData();
            ShowSection("Analysis");
        }

        private void AnalysisRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshAllData();
            LoadBrgyAnalysisCharts();
        }

        private async void InitBrgyChartWebViewAsync()
        {
            try
            {
                await BrgyChartWebView.EnsureCoreWebView2Async();
                BrgyChartWebView.CoreWebView2.Settings.IsWebMessageEnabled = false;
                _brgyChartWebViewReady = true;
            }
            catch (Exception ex)
            {
                BrgyChartLoading.Visibility = Visibility.Collapsed;
                ShowToast($"! Could not initialize analysis charts: {ex.Message}");
            }
        }

        private void LoadBrgyAnalysisCharts()
        {
            if (!_brgyChartWebViewReady) return;

            BrgyChartLoading.Visibility = Visibility.Visible;

            try
            {
            var brgy = SessionManager.AssignedBarangay ?? "";

            int totalIncidents = _incidents.Count;
            int totalFamilies  = _families.Count;
            int totalMembers   = _families.Sum(f => f.MemberCount);
            int activeCenters  = _centers.Count(c => c.CurrentOccupancy > 0);

            var centerNames = _centers.Select(c => c.Name).ToArray();
            var centerOcc   = _centers.Select(c => c.CurrentOccupancy).ToArray();
            var centerCap   = _centers.Select(c => c.Capacity).ToArray();

            var now         = DateTime.Now;
            var dateMap     = _incidents.ToDictionary(i => i.IncidentID, i => i.DateTime);
            var monthLabels = new System.Collections.Generic.List<string>();
            var monthData   = new System.Collections.Generic.List<int>();
            for (int m = 11; m >= 0; m--)
            {
                var d = now.AddMonths(-m);
                monthLabels.Add(d.ToString("MMM yy"));
                int count = 0;
                foreach (var f in _families)
                {
                    if (dateMap.TryGetValue(f.IncidentID, out var dtStr) &&
                        DateTime.TryParse(dtStr, out var dt) &&
                        dt.Year == d.Year && dt.Month == d.Month)
                        count++;
                }
                monthData.Add(count);
            }

            int relievedCount = _families.Count(f => f.ReliefStatus != "Pending");
            int pendingCount  = _families.Count - relievedCount;

            var outgoing    = _crossRepo.GetOutgoing(brgy);
            int reqPending  = outgoing.Count(r => r.Status == "Pending");
            int reqApproved = outgoing.Count(r => r.Status == "Approved");
            int reqRejected = outgoing.Count(r => r.Status == "Rejected");

            int repeatCount = _families.Count(f => f.IsRepeatDisplaced);
            int newCount    = _families.Count - repeatCount;

            string cnJs = JsonSerializer.Serialize(centerNames);
            string coJs = JsonSerializer.Serialize(centerOcc);
            string ccJs = JsonSerializer.Serialize(centerCap);
            string mlJs = JsonSerializer.Serialize(monthLabels.ToArray());
            string mdJs = JsonSerializer.Serialize(monthData.ToArray());

            string html = BuildBrgyChartsHtml(
                totalIncidents, totalFamilies, totalMembers, activeCenters,
                relievedCount, pendingCount,
                reqPending, reqApproved, reqRejected,
                repeatCount, newCount,
                cnJs, coJs, ccJs, mlJs, mdJs);

            BrgyChartWebView.NavigateToString(html);
            BrgyChartLoading.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                BrgyChartLoading.Visibility = Visibility.Collapsed;
                BrgyChartWebView.NavigateToString(
                    "<html><body style='background:#09131C;color:#ff7878;font-family:Consolas,monospace;padding:24px'>" +
                    $"Could not load analysis charts: {System.Net.WebUtility.HtmlEncode(ex.Message)}</body></html>");
            }
        }

        private static string BuildBrgyChartsHtml(
            int totalIncidents, int totalFamilies, int totalMembers, int activeCenters,
            int relievedCount, int pendingCount,
            int reqPending, int reqApproved, int reqRejected,
            int repeatCount, int newCount,
            string cnJs, string coJs, string ccJs,
            string mlJs, string mdJs)
        {
            int    totalRelief = relievedCount + pendingCount;
            string reliefPct   = totalRelief > 0
                ? $"{relievedCount * 100 / totalRelief}%" : "N/A";

            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html lang='en'><head><meta charset='UTF-8'/><style>");
            sb.Append("*{margin:0;padding:0;box-sizing:border-box;}");
            sb.Append("body{background:#09131C;color:#C8D8E8;font-family:Consolas,monospace;padding:16px;overflow-y:auto;}");
            sb.Append(".sr{display:flex;gap:8px;margin-bottom:12px;flex-wrap:wrap;}");
            sb.Append(".st{flex:1;min-width:110px;background:#0C1A26;border:1px solid #172435;padding:10px 12px;}");
            sb.Append(".sl{font-size:8px;letter-spacing:2px;color:#3A5570;margin-bottom:4px;}");
            sb.Append(".sv{font-size:20px;font-weight:bold;}");
            sb.Append(".grid2{display:grid;grid-template-columns:1fr 1fr;gap:10px;margin-bottom:10px;}");
            sb.Append(".bl{background:#0C1A26;border:1px solid #172435;padding:12px;}");
            sb.Append(".bt{font-size:9px;letter-spacing:1.5px;color:#4A8EC2;margin-bottom:10px;font-weight:bold;}");
            sb.Append("canvas{max-height:180px;}");
            sb.Append(".full{grid-column:1/-1;}");
            sb.Append("</style></head><body>");

            sb.Append("<div class='sr'>");
            sb.AppendFormat("<div class='st'><div class='sl'>INCIDENTS</div><div class='sv' style='color:#FF5252'>{0}</div></div>", totalIncidents);
            sb.AppendFormat("<div class='st'><div class='sl'>FAMILIES</div><div class='sv' style='color:#4A8EC2'>{0}</div></div>", totalFamilies);
            sb.AppendFormat("<div class='st'><div class='sl'>MEMBERS</div><div class='sv' style='color:#7A9BB8'>{0}</div></div>", totalMembers);
            sb.AppendFormat("<div class='st'><div class='sl'>ACTIVE CENTERS</div><div class='sv' style='color:#4A9A60'>{0}</div></div>", activeCenters);
            sb.AppendFormat("<div class='st'><div class='sl'>RELIEF COVERAGE</div><div class='sv' style='color:#FFC107'>{0}</div></div>", reliefPct);
            sb.Append("</div>");

            sb.Append("<div class='grid2'>");
            sb.Append("<div class='bl full'><div class='bt'>CENTER UTILIZATION (OCCUPANCY vs CAPACITY)</div><canvas id='cu'></canvas></div>");
            sb.Append("<div class='bl'><div class='bt'>FAMILIES DISPLACED / MONTH (LAST 12)</div><canvas id='mf'></canvas></div>");
            sb.Append("<div class='bl'><div class='bt'>RELIEF COVERAGE</div><canvas id='rc'></canvas></div>");
            sb.Append("<div class='bl'><div class='bt'>OUTGOING CROSS-BRGY REQUESTS</div><canvas id='cr'></canvas></div>");
            sb.Append("<div class='bl'><div class='bt'>DISPLACEMENT TYPE</div><canvas id='rd'></canvas></div>");
            sb.Append("</div>");

            sb.Append("<script src='https://cdnjs.cloudflare.com/ajax/libs/Chart.js/4.4.0/chart.umd.min.js'></script>");
            sb.Append("<script>");
            sb.Append("Chart.defaults.color='#7A9BB8';Chart.defaults.borderColor='#172435';");
            sb.Append("var G={color:'#172435'};");
            sb.AppendFormat("var CN={0},CO={1},CC={2},ML={3},MD={4};", cnJs, coJs, ccJs, mlJs, mdJs);
            sb.AppendFormat("var RLD={0},RP={1},RD={2};",
                JsonSerializer.Serialize(new[] { relievedCount, pendingCount }),
                JsonSerializer.Serialize(new[] { reqPending, reqApproved, reqRejected }),
                JsonSerializer.Serialize(new[] { newCount, repeatCount }));

            sb.Append("new Chart(document.getElementById('cu'),{type:'bar',");
            sb.Append("data:{labels:CN,datasets:[");
            sb.Append("{label:'Occupancy',data:CO,backgroundColor:'rgba(74,142,194,0.6)',borderColor:'#4A8EC2',borderWidth:1},");
            sb.Append("{label:'Capacity',data:CC,backgroundColor:'rgba(58,85,112,0.3)',borderColor:'#3A5570',borderWidth:1}]},");
            sb.Append("options:{responsive:true,scales:{x:{grid:G},y:{grid:G,ticks:{stepSize:1}}},plugins:{legend:{labels:{font:{family:'Consolas',size:9}}}}}});");

            sb.Append("new Chart(document.getElementById('mf'),{type:'line',");
            sb.Append("data:{labels:ML,datasets:[{label:'Families',data:MD,");
            sb.Append("borderColor:'#4A8EC2',backgroundColor:'rgba(74,142,194,0.15)',");
            sb.Append("tension:0.3,fill:true,pointRadius:4,pointBackgroundColor:'#4A8EC2'}]},");
            sb.Append("options:{responsive:true,plugins:{legend:{display:false}},scales:{x:{grid:G},y:{grid:G,ticks:{stepSize:1}}}}});");

            sb.Append("new Chart(document.getElementById('rc'),{type:'doughnut',");
            sb.Append("data:{labels:['With Relief','Pending'],datasets:[{data:RLD,");
            sb.Append("backgroundColor:['#4A9A60','#3A5570'],borderWidth:1}]},");
            sb.Append("options:{responsive:true,plugins:{legend:{position:'right',labels:{font:{family:'Consolas',size:9}}}}}});");

            sb.Append("new Chart(document.getElementById('cr'),{type:'doughnut',");
            sb.Append("data:{labels:['Pending','Approved','Rejected'],datasets:[{data:RP,");
            sb.Append("backgroundColor:['#FFC107','#4A9A60','#C05050'],borderWidth:1}]},");
            sb.Append("options:{responsive:true,plugins:{legend:{position:'right',labels:{font:{family:'Consolas',size:9}}}}}});");

            sb.Append("new Chart(document.getElementById('rd'),{type:'bar',");
            sb.Append("data:{labels:['New','Repeat'],datasets:[{data:RD,");
            sb.Append("backgroundColor:['rgba(74,142,194,0.6)','rgba(192,80,80,0.6)'],");
            sb.Append("borderColor:['#4A8EC2','#C05050'],borderWidth:1}]},");
            sb.Append("options:{responsive:true,plugins:{legend:{display:false}},");
            sb.Append("scales:{x:{grid:G},y:{grid:G,ticks:{stepSize:1}}}}});");

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

        private void BuildBrgyCitizenReportsSection()
        {
            var brgy = SessionManager.AssignedBarangay;
            List<CitizenReport> reports;
            try
            {
                reports = string.IsNullOrWhiteSpace(brgy)
                    ? _citizenReportRepo.GetAllForBarangayInbox()
                    : _citizenReportRepo.GetByBarangay(brgy);
            }
            catch (Exception ex)
            {
                BrgyCitizenReportsPanel.Children.Clear();
                BrgyCitizenReportsCount.Text = "Could not load reports";
                BrgyCitizenReportsPanel.Children.Add(new TextBlock
                {
                    Text = $"Could not load citizen reports: {ex.Message}",
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 230, 120, 120)),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 24, 0, 0)
                });
                return;
            }

            BrgyCitizenReportsPanel.Children.Clear();

            int verifiedCount = reports.Count(r => r.IsVerified);
            int pendingCount  = reports.Count - verifiedCount;
            BrgyCitizenReportsCount.Text = reports.Count == 0
                ? (string.IsNullOrWhiteSpace(brgy)
                    ? "No reports submitted yet."
                    : $"No reports submitted for Brgy. {brgy} yet.")
                : $"{reports.Count} total · {pendingCount} pending · {verifiedCount} verified";

            if (reports.Count == 0)
            {
                BrgyCitizenReportsPanel.Children.Add(new TextBlock
                {
                    Text                = "No citizen reports yet.",
                    FontFamily          = new FontFamily("Consolas"),
                    FontSize             = 11,
                    Foreground          = new SolidColorBrush(Color.FromArgb(255, 74, 107, 138)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin              = new Thickness(0, 24, 0, 0)
                });
                return;
            }

            foreach (var report in reports)
                BrgyCitizenReportsPanel.Children.Add(BuildBrgyReportCard(report));
        }

        private Border BuildBrgyReportCard(CitizenReport report)
        {
            bool verified = report.IsVerified;
            Color borderColor = verified
                ? Color.FromArgb(255, 76, 175, 80)
                : Color.FromArgb(255, 200, 154, 48);
            Color statusBg = verified
                ? Color.FromArgb(255, 12, 30, 18)
                : Color.FromArgb(255, 30, 24, 8);
            var consolas = new FontFamily("Consolas");

            var card = new Border
            {
                Background      = new SolidColorBrush(Color.FromArgb(255, 12, 26, 38)),
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
                Foreground = new SolidColorBrush(White)
            });
            headerLeft.Children.Add(new TextBlock
            {
                Text       = $"📞 {report.Phone}",
                FontFamily = consolas, FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 122, 155, 184))
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
                    CornerRadius = new CornerRadius(0),
                    Background = new SolidColorBrush(Color.FromArgb(255, 42, 16, 16)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 122, 32, 32)),
                    BorderThickness = new Thickness(1),
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 128, 112)),
                    FontFamily = consolas,
                    FontSize = 11,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold
                };
                removeBtn.Click += (s, e) =>
                {
                    try { _citizenReportRepo.DismissFromBarangayInbox(report.ReportID); } catch {  }
                    BuildBrgyCitizenReportsSection();
                    UpdateBrgyCitizenReportsButton();
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
                Foreground   = new SolidColorBrush(White),
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
                    Background          = new SolidColorBrush(Color.FromArgb(255, 26, 74, 122)),
                    BorderThickness     = new Thickness(0),
                    Padding             = new Thickness(10, 8, 10, 8),
                    CornerRadius        = new CornerRadius(0),
                    Content = new StackPanel
                    {
                        Orientation         = Orientation.Horizontal,
                        Spacing             = 6,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Children =
                        {
                            new TextBlock { Text = "📞", FontSize = 13, VerticalAlignment = VerticalAlignment.Center },
                            new TextBlock
                            {
                                Text             = "CALL TO VERIFY",
                                FontFamily       = consolas, FontSize = 11,
                                FontWeight       = Microsoft.UI.Text.FontWeights.Bold,
                                Foreground       = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
                                CharacterSpacing = 80,
                                VerticalAlignment = VerticalAlignment.Center
                            }
                        }
                    }
                };
                callBtn.Click += async (s, e) => await ShowBrgyCallDialog(report);
                stack.Children.Add(callBtn);
            }

            card.Child = stack;
            return card;
        }

        private async System.Threading.Tasks.Task ShowBrgyCallDialog(CitizenReport report)
        {
            var consolas = new FontFamily("Consolas");
            var panelBg  = new SolidColorBrush(Color.FromArgb(255, 12, 26, 38));
            var headerBg = new SolidColorBrush(Color.FromArgb(255, 15, 32, 53));
            var fieldBg  = new SolidColorBrush(Color.FromArgb(255, 9, 19, 28));
            var border   = new SolidColorBrush(Color.FromArgb(255, 23, 36, 53));
            var muted    = new SolidColorBrush(Color.FromArgb(255, 58, 85, 112));
            var text     = new SolidColorBrush(Color.FromArgb(255, 200, 216, 232));
            var accent   = new SolidColorBrush(Color.FromArgb(255, 74, 142, 194));
            var amber    = new SolidColorBrush(Color.FromArgb(255, 200, 154, 48));
            var green    = new SolidColorBrush(Color.FromArgb(255, 76, 175, 80));

            var ring = new ProgressRing
            {
                IsActive = true,
                Width = 56,
                Height = 56,
                Foreground = accent,
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
                Foreground       = accent,
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
                Background = new SolidColorBrush(Color.FromArgb(255, 32, 27, 13)),
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

            var popupClosed = new System.Threading.Tasks.TaskCompletionSource<bool>();
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

            DispatcherQueue.GetForCurrentThread().TryEnqueue(async () =>
            {
                await System.Threading.Tasks.Task.Delay(3000);
                ring.IsActive   = false;
                ring.Visibility = Visibility.Collapsed;
                checkText.Visibility = Visibility.Visible;
                signalText.Text = "VERIFICATION COMPLETE";
                signalText.Foreground = green;
                statusText.Text = "REPORT VERIFIED";
                statusText.Foreground = green;
                callBadge.Background = new SolidColorBrush(Color.FromArgb(255, 16, 43, 30));
                callBadge.BorderBrush = green;
                callBadgeText.Text = "VERIFIED";
                callBadgeText.Foreground = green;
                subText.Text = $"Report from {report.FullName} confirmed.\nMarked as verified in the system.";
                closeBtn.IsEnabled = true;
                closeBtn.Background = green;
                closeBtn.BorderBrush = green;
                closeBtn.Foreground = fieldBg;
                try { _citizenReportRepo.SetVerified(report.ReportID); } catch {  }
            });

            popup.IsOpen = true;
            await popupClosed.Task;
            BuildBrgyCitizenReportsSection();
            UpdateBrgyCitizenReportsButton();
        }
    }
}
