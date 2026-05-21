using CPE262_FINAL_PROJECT.Models;
using CPE262_FINAL_PROJECT.Repositories;
using CPE262_FINAL_PROJECT.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Microsoft.UI.Xaml.Navigation;

using UIEllipse = Microsoft.UI.Xaml.Shapes.Ellipse;
using UIRectangle = Microsoft.UI.Xaml.Shapes.Rectangle;

namespace CPE262_FINAL_PROJECT.Views
{
    public sealed partial class CitizenDashboardPage : Page
    {
        private readonly IncidentRepository _incidentRepo = new();
        private readonly FamilyRepository _familyRepo = new();
        private readonly ReliefRecordRepository _reliefRepo = new();
        private readonly BarangayRepository _barangayRepo = new();
        private readonly EvacuationCenterRepository _evacRepo = new();
        private readonly CitizenReportRepository _citizenReportRepo = new();
        private readonly DSWDMessageRepository _dswdMsgRepo = new();


        private List<Incident> _allIncidents = new();
        private string _activeFilter = "All";
        private string _activeSection = "Feed";
        private bool _reportOverlayInitialized = false;

        private bool _mapCoreReady = false;
        private bool _mapHtmlLoaded = false;
        private Incident? _pendingFocusIncident = null;

        private List<EvacuationCenter> _reliefEvacCenters = new();
        private string _reliefPhase = "Gate";
        private int? _reliefLinkedIncidentId = null;
        private Incident? _dswdTargetIncident = null;

        private Family? _lastFoundFamily = null;
        private Incident? _lastFoundInc = null;
        private EvacuationCenter? _lastFoundCenter = null;

        private Window? _parentWindow = null;

        private static readonly Color ActiveSelectedBg = Color.FromArgb(255, 42, 13, 16);
        private static readonly Color ActiveDefaultBg = Color.FromArgb(255, 11, 21, 32);
        private static readonly Color ActiveDefaultFg = Color.FromArgb(255, 79, 108, 138);
        private static readonly Color UCSelectedBg = Color.FromArgb(255, 42, 30, 6);
        private static readonly Color FireOutSelectedBg = Color.FromArgb(255, 7, 26, 24);
        private static readonly Color OtherDefaultBg = Color.FromArgb(255, 11, 21, 32);
        private static readonly Color OtherDefaultFg = Color.FromArgb(255, 79, 108, 138);
        private static readonly Color White = Color.FromArgb(255, 255, 255, 255);

        private static readonly Color NavRed = Color.FromArgb(255, 230, 57, 70);
        private static readonly Color NavGray = Color.FromArgb(255, 71, 85, 105);
        private static readonly Color NavDarkBg = Color.FromArgb(255, 23, 32, 43);

        private static Color AlarmStripeColor(int level) => level switch
        {
            1 => Color.FromArgb(255, 255, 193, 7),
            2 => Color.FromArgb(255, 255, 104, 0),
            _ => Color.FromArgb(255, 192, 80, 80)
        };

        public CitizenDashboardPage()
        {
            InitializeComponent();
            OperatorName.Text = SessionManager.FullName;

            var timer = Microsoft.UI.Dispatching.DispatcherQueue
                            .GetForCurrentThread().CreateTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += (_, _) => ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
            timer.Start();

            LoadIncidentFeed();
            SetSidebarActive("Feed");
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _parentWindow = ((App)Application.Current).MainWindow;
            if (_parentWindow != null) _parentWindow.Activated += Window_Activated;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            if (_parentWindow != null) _parentWindow.Activated -= Window_Activated;
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs e)
        {
            if (e.WindowActivationState != WindowActivationState.Deactivated)
            {
                LoadIncidentFeed();
                RefreshDSWDMessagesSection();
            }
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            SessionManager.Logout();
            Frame.Navigate(typeof(LoginPage));
        }

        private void SetSidebarActive(string section)
        {
            _activeSection = section;

            NavFeedIndicator.Fill       = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            NavReliefIndicator.Fill     = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            SidebarFeedBtn.Background   = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            SidebarReliefBtn.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            NavFeedText.Foreground      = new SolidColorBrush(NavGray);
            NavReliefText.Foreground    = new SolidColorBrush(NavGray);

            switch (section)
            {
                case "Feed":
                    NavFeedIndicator.Fill     = new SolidColorBrush(NavRed);
                    SidebarFeedBtn.Background = new SolidColorBrush(NavDarkBg);
                    NavFeedText.Foreground    = new SolidColorBrush(White);
                    break;
                case "Relief":
                    NavReliefIndicator.Fill     = new SolidColorBrush(NavRed);
                    SidebarReliefBtn.Background = new SolidColorBrush(NavDarkBg);
                    NavReliefText.Foreground    = new SolidColorBrush(White);
                    break;
            }

            FeedScrollView.Visibility   = section == "Feed" ? Visibility.Visible : Visibility.Collapsed;
            ReliefScrollView.Visibility = section == "Relief" ? Visibility.Visible : Visibility.Collapsed;
            if (section != "Feed")
                FeedBackToTopBtn.Visibility = Visibility.Collapsed;
            else
                UpdateFeedBackToTopButton();

            HeaderSectionTitle.Text = section == "Relief" ? "RELIEF INFO" : "INCIDENT FEED";

            if (section == "Relief" && _reliefPhase == "Gate")
                ShowReliefGate();
            else if (section == "Relief" && _reliefPhase == "Result")
                RefreshDSWDMessagesSection();
        }

        private void SidebarFeed_Click(object sender, RoutedEventArgs e) => SetSidebarActive("Feed");
        private void SidebarRelief_Click(object sender, RoutedEventArgs e) => SetSidebarActive("Relief");

        private void FeedScrollView_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
            => UpdateFeedBackToTopButton();

        private void FeedBackToTop_Click(object sender, RoutedEventArgs e)
        {
            FeedScrollView.ChangeView(null, 0, null);
            FeedBackToTopBtn.Visibility = Visibility.Collapsed;
        }

        private void UpdateFeedBackToTopButton()
        {
            bool hasScrolledDown = FeedScrollView.ScrollableHeight > 0 &&
                                   FeedScrollView.VerticalOffset > 24;

            FeedBackToTopBtn.Visibility = _activeSection == "Feed" && hasScrolledDown
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void LoadIncidentFeed()
        {
            try
            {
                _allIncidents = _incidentRepo.GetAll();
                RenderIncidentFeed();
            }
            catch (Exception ex)
            {
                _allIncidents = new();
                ShowIncidentFeedError($"Could not load incidents: {ex.Message}");
            }
        }

        private void RenderIncidentFeed()
        {
            int activeCount = _allIncidents.Count(i => i.Status == "Active");
            int ucCount = _allIncidents.Count(i => i.Status == "Under Control");
            int foCount = _allIncidents.Count(i => i.Status == "Fire Out");
            ActiveCountBadge.Text       = $"{activeCount} ACTIVE INCIDENT{(activeCount != 1 ? "S" : "")}";
            ActiveFilterText.Text       = $"ACTIVE \u00B7 {activeCount}";
            UnderControlFilterText.Text = $"UNDER CONTROL \u00B7 {ucCount}";
            FireOutFilterText.Text      = $"FIRE OUT \u00B7 {foCount}";

            DateTime? fromDate = null;
            DateTime? toDate = null;
            try
            {
                if (DateFromPicker.SelectedDate.HasValue)
                    fromDate = DateFromPicker.SelectedDate.Value.Date;
                if (DateToPicker.SelectedDate.HasValue)
                    toDate = DateToPicker.SelectedDate.Value.Date;
            }
            catch { }

            var filtered = _allIncidents.Where(i =>
            {
                if (_activeFilter != "All" && i.Status != _activeFilter) return false;
                if ((fromDate.HasValue || toDate.HasValue) && DateTime.TryParse(i.DateTime, out var dt))
                {
                    if (fromDate.HasValue && dt.Date < fromDate.Value) return false;
                    if (toDate.HasValue   && dt.Date > toDate.Value) return false;
                }
                return true;
            }).ToList();

            IncidentFeedPanel.Children.Clear();

            if (filtered.Count == 0)
            {
                string msg = _activeFilter == "All"
                    ? "No incidents match the current filter."
                    : $"No \"{_activeFilter}\" incidents match the current filter.";
                IncidentFeedPanel.Children.Add(new Border
                {
                    Padding = new Thickness(20),
                    Child   = new TextBlock
                    {
                        Text                = msg,
                        FontFamily          = new FontFamily("Consolas"),
                        FontSize            = 11,
                        Foreground          = new SolidColorBrush(Color.FromArgb(255, 74, 107, 138)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        TextWrapping        = TextWrapping.Wrap
                    }
                });
                return;
            }

            foreach (var inc in filtered)
            {
                try
                {
                    var families = _familyRepo.GetByIncident(inc.IncidentID);
                    int reliefCount = 0;
                    foreach (var f in families)
                        reliefCount += _reliefRepo.GetByFamily(f.FamilyID).Count;
                    IncidentFeedPanel.Children.Add(BuildIncidentCard(inc, families.Count, reliefCount));
                }
                catch (Exception ex)
                {
                    IncidentFeedPanel.Children.Add(BuildInlineError($"Could not load incident #{inc.IncidentID}: {ex.Message}"));
                }
            }
        }

        private void ShowIncidentFeedError(string message)
        {
            IncidentFeedPanel.Children.Clear();
            IncidentFeedPanel.Children.Add(BuildInlineError(message));
        }

        private static Border BuildInlineError(string message) => new()
        {
            Padding = new Thickness(18, 14, 18, 14),
            Background = new SolidColorBrush(Color.FromArgb(255, 24, 8, 8)),
            BorderThickness = new Thickness(3, 0, 0, 0),
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 230, 90, 90)),
            Child = new TextBlock
            {
                Text = message,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 230, 120, 120)),
                TextWrapping = TextWrapping.Wrap
            }
        };

        private void DateFilter_Changed(DatePicker sender, DatePickerSelectedValueChangedEventArgs args)
            => RenderIncidentFeed();

        private void ClearDate_Click(object sender, RoutedEventArgs e)
        {
            DateFromPicker.SelectedDate = null;
            DateToPicker.SelectedDate   = null;
            RenderIncidentFeed();
        }

        private void FilterPill_Click(object sender, RoutedEventArgs e)
        {
            var tag = (sender as Button)?.Tag as string;
            if (string.IsNullOrEmpty(tag)) return;
            _activeFilter = (_activeFilter == tag) ? "All" : tag!;
            UpdatePillVisuals();
            RenderIncidentFeed();
        }

        private void UpdatePillVisuals()
        {
            if (_activeFilter == "Active")
            {
                ActiveFilterBtn.Background  = new SolidColorBrush(ActiveSelectedBg);
                ActiveFilterBtn.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 230, 57, 70));
                ActiveFilterText.Foreground = new SolidColorBrush(Color.FromArgb(255, 230, 57, 70));
                ActiveFilterText.FontWeight = Microsoft.UI.Text.FontWeights.Bold;
            }
            else
            {
                ActiveFilterBtn.Background  = new SolidColorBrush(ActiveDefaultBg);
                ActiveFilterBtn.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 20, 36, 51));
                ActiveFilterText.Foreground = new SolidColorBrush(ActiveDefaultFg);
                ActiveFilterText.FontWeight = Microsoft.UI.Text.FontWeights.Bold;
            }

            if (_activeFilter == "Under Control")
            {
                UnderControlFilterBtn.Background  = new SolidColorBrush(UCSelectedBg);
                UnderControlFilterBtn.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 245, 158, 11));
                UnderControlFilterText.Foreground = new SolidColorBrush(Color.FromArgb(255, 245, 158, 11));
                UnderControlFilterText.FontWeight = Microsoft.UI.Text.FontWeights.Bold;
            }
            else
            {
                UnderControlFilterBtn.Background  = new SolidColorBrush(OtherDefaultBg);
                UnderControlFilterBtn.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 20, 36, 51));
                UnderControlFilterText.Foreground = new SolidColorBrush(OtherDefaultFg);
                UnderControlFilterText.FontWeight = Microsoft.UI.Text.FontWeights.Bold;
            }

            if (_activeFilter == "Fire Out")
            {
                FireOutFilterBtn.Background  = new SolidColorBrush(FireOutSelectedBg);
                FireOutFilterBtn.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 20, 184, 166));
                FireOutFilterText.Foreground = new SolidColorBrush(Color.FromArgb(255, 20, 184, 166));
                FireOutFilterText.FontWeight = Microsoft.UI.Text.FontWeights.Bold;
            }
            else
            {
                FireOutFilterBtn.Background  = new SolidColorBrush(OtherDefaultBg);
                FireOutFilterBtn.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 20, 36, 51));
                FireOutFilterText.Foreground = new SolidColorBrush(OtherDefaultFg);
                FireOutFilterText.FontWeight = Microsoft.UI.Text.FontWeights.Bold;
            }
        }

        private UIElement BuildIncidentCard(Incident inc, int familyCount, int reliefCount)
        {
            Color statusColor; string statusLabel;
            switch (inc.Status)
            {
                case "Under Control":
                    statusColor = Color.FromArgb(255, 245, 158, 11); statusLabel = "UNDER CONTROL"; break;
                case "Fire Out":
                    statusColor = Color.FromArgb(255, 48, 161, 147); statusLabel = "FIRE OUT"; break;
                default:
                    statusColor = Color.FromArgb(255, 230, 57, 70); statusLabel = "ACTIVE"; break;
            }

            Color stripeColor = AlarmStripeColor(inc.AlarmLevel);
            bool affected = IsCitizenAffected(inc);

            string timeAgo = "";
            if (DateTime.TryParse(inc.DateTime, out var dt))
            {
                var span = DateTime.Now - dt;
                timeAgo = span.TotalDays  >= 1 ? $"{(int)span.TotalDays}d ago"
                        : span.TotalHours >= 1 ? $"{(int)span.TotalHours}h ago"
                        : $"{(int)span.TotalMinutes}m ago";
            }

            var wrapper = new Border
            {
                CornerRadius    = new CornerRadius(0),
                BorderThickness = new Thickness(1),
                BorderBrush     = new SolidColorBrush(affected
                    ? Color.FromArgb(255, 245, 158, 11)
                    : Color.FromArgb(255, 20, 32, 46))
            };

            var card = new Border
            {
                Background      = new SolidColorBrush(Color.FromArgb(255, 18, 28, 39)),
                BorderThickness = new Thickness(4, 0, 0, 0),
                BorderBrush     = new SolidColorBrush(stripeColor),
                CornerRadius    = new CornerRadius(0)
            };

            var inner = new StackPanel();

            if (!string.IsNullOrWhiteSpace(inc.PhotoPath) && File.Exists(inc.PhotoPath))
            {
                try
                {
                    inner.Children.Add(new Border
                    {
                        Height = 400,
                        Child  = new Microsoft.UI.Xaml.Controls.Image
                        {
                            Source  = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(inc.PhotoPath)),
                            Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill
                        }
                    });
                }
                catch { inner.Children.Add(BuildNoPhotoBanner()); }
            }
            else
            {
                inner.Children.Add(BuildNoPhotoBanner());
            }

            var hdrGrid = new Grid { Padding = new Thickness(14, 14, 16, 12) };
            hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var alarmSquare = new Border
            {
                Width        = 44,
                Height      = 44,
                Background   = new SolidColorBrush(Color.FromArgb(50, statusColor.R, statusColor.G, statusColor.B)),
                CornerRadius = new CornerRadius(0),
                Child        = new TextBlock
                {
                    Text                = inc.AlarmLevel.ToString(),
                    FontSize            = 20,
                    FontWeight          = Microsoft.UI.Text.FontWeights.ExtraBold,
                    FontFamily          = new FontFamily("Consolas"),
                    Foreground          = new SolidColorBrush(statusColor),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center
                }
            };
            Grid.SetColumn(alarmSquare, 0);

            var infoStack = new StackPanel
            {
                Spacing           = 4,
                Margin            = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            infoStack.Children.Add(new TextBlock
            {
                Text       = inc.Barangay,
                FontSize   = 15,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 217, 227, 242))
            });

            string sitioLine = string.IsNullOrWhiteSpace(inc.Sitio)
                ? $"Brgy. {inc.Barangay}"
                : $"Sitio {inc.Sitio} · Brgy. {inc.Barangay}";
            infoStack.Children.Add(new TextBlock
            {
                Text       = sitioLine,
                FontFamily = new FontFamily("Consolas"),
                FontSize   = 9,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 71, 85, 105))
            });

            var subRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            subRow.Children.Add(new Border
            {
                Background   = new SolidColorBrush(Color.FromArgb(255, 30, 16, 16)),
                CornerRadius = new CornerRadius(0),
                Padding      = new Thickness(6, 2, 6, 2),
                Child        = new TextBlock
                {
                    Text       = $"ALARM {inc.AlarmLevel}",
                    FontFamily = new FontFamily("Consolas"),
                    FontSize   = 9,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(statusColor)
                }
            });
            if (!string.IsNullOrEmpty(timeAgo))
                subRow.Children.Add(new TextBlock
                {
                    Text              = timeAgo,
                    FontFamily        = new FontFamily("Consolas"),
                    FontSize          = 9,
                    Foreground        = new SolidColorBrush(Color.FromArgb(255, 71, 85, 105)),
                    VerticalAlignment = VerticalAlignment.Center
                });
            if (affected)
                subRow.Children.Add(new Border
                {
                    Background   = new SolidColorBrush(Color.FromArgb(255, 42, 30, 10)),
                    CornerRadius = new CornerRadius(0),
                    Padding      = new Thickness(6, 2, 6, 2),
                    Child        = new TextBlock
                    {
                        Text       = "AFFECTS YOU",
                        FontFamily = new FontFamily("Consolas"),
                        FontSize   = 9,
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 245, 158, 11))
                    }
                });
            infoStack.Children.Add(subRow);
            Grid.SetColumn(infoStack, 1);

            var statusPill = new Border
            {
                Background        = new SolidColorBrush(Color.FromArgb(40, statusColor.R, statusColor.G, statusColor.B)),
                BorderBrush       = new SolidColorBrush(statusColor),
                BorderThickness   = new Thickness(1),
                CornerRadius      = new CornerRadius(0),
                Padding           = new Thickness(10, 4, 10, 4),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text       = statusLabel,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize   = 9,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(statusColor)
                }
            };
            Grid.SetColumn(statusPill, 2);

            hdrGrid.Children.Add(alarmSquare);
            hdrGrid.Children.Add(infoStack);
            hdrGrid.Children.Add(statusPill);
            inner.Children.Add(hdrGrid);

            var desc = string.IsNullOrEmpty(inc.CauseOfFire)
                ? $"Incident reported in {inc.Barangay}."
                : inc.CauseOfFire;
            inner.Children.Add(new TextBlock
            {
                Text         = desc,
                FontFamily   = new FontFamily("Consolas"),
                FontSize     = 11,
                Foreground   = new SolidColorBrush(Color.FromArgb(255, 71, 85, 105)),
                TextWrapping = TextWrapping.Wrap,
                MaxHeight    = 36,
                Padding      = new Thickness(16, 0, 16, 12)
            });

            var footer = new Border
            {
                BorderThickness = new Thickness(0, 1, 0, 0),
                BorderBrush     = new SolidColorBrush(Color.FromArgb(255, 15, 24, 34)),
                Padding         = new Thickness(16, 10, 16, 12)
            };
            var footerGrid = new Grid();

            var statsRow = new StackPanel
            {
                Orientation       = Orientation.Horizontal,
                Spacing           = 16,
                VerticalAlignment = VerticalAlignment.Center
            };
            statsRow.Children.Add(new StackPanel
            {
                Spacing = 1,
                Children =
            {
                new TextBlock { Text = "FAMILIES", FontFamily = new FontFamily("Consolas"), FontSize = 8, Foreground = new SolidColorBrush(Color.FromArgb(255, 71, 85, 105)), CharacterSpacing = 120 },
                new TextBlock { Text = familyCount.ToString(), FontFamily = new FontFamily("Consolas"), FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromArgb(255, 245, 158, 11)) }
            }
            });
            statsRow.Children.Add(new Border { Width = 1, Height = 28, Background = new SolidColorBrush(Color.FromArgb(255, 20, 32, 46)) });
            statsRow.Children.Add(new StackPanel
            {
                Spacing = 1,
                Children =
            {
                new TextBlock { Text = "RELIEF", FontFamily = new FontFamily("Consolas"), FontSize = 8, Foreground = new SolidColorBrush(Color.FromArgb(255, 71, 85, 105)), CharacterSpacing = 120 },
                new TextBlock { Text = reliefCount.ToString(), FontFamily = new FontFamily("Consolas"), FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromArgb(255, 48, 161, 147)) }
            }
            });
            footerGrid.Children.Add(statsRow);

            var actionStack = new StackPanel
            {
                Orientation         = Orientation.Horizontal,
                Spacing             = 8,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment   = VerticalAlignment.Center
            };

            if (affected)
            {
                var msgBtn = new Button
                {
                    Background      = new SolidColorBrush(Color.FromArgb(255, 7, 26, 20)),
                    BorderThickness = new Thickness(1),
                    BorderBrush     = new SolidColorBrush(Color.FromArgb(255, 48, 161, 147)),
                    Padding         = new Thickness(10, 6, 10, 6),
                    CornerRadius    = new CornerRadius(0),
                    Content         = new TextBlock { Text = "MESSAGE DSWD", FontFamily = new FontFamily("Consolas"), FontSize = 9, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromArgb(255, 48, 161, 147)), CharacterSpacing = 60 }
                };
                msgBtn.Click += async (s, e) => await ShowDSWDMessageDialog(inc);
                actionStack.Children.Add(msgBtn);
            }

            var viewBtn = new Button
            {
                Background      = new SolidColorBrush(Color.FromArgb(255, 20, 32, 46)),
                BorderThickness = new Thickness(1),
                BorderBrush     = new SolidColorBrush(Color.FromArgb(255, 30, 46, 64)),
                Padding         = new Thickness(10, 6, 10, 6),
                CornerRadius    = new CornerRadius(0),
                Content         = new TextBlock { Text = "VIEW DETAILS", FontFamily = new FontFamily("Consolas"), FontSize = 9, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromArgb(255, 148, 163, 184)), CharacterSpacing = 60 }
            };
            viewBtn.Click += (s, e) => OpenIncidentDetails(inc);
            actionStack.Children.Add(viewBtn);

            footerGrid.Children.Add(actionStack);
            footer.Child = footerGrid;
            inner.Children.Add(footer);
            card.Child = inner;
            wrapper.Child = card;
            return wrapper;
        }

        private static Border BuildNoPhotoBanner() => new()
        {
            Height     = 400,
            Background = new SolidColorBrush(Color.FromArgb(255, 9, 19, 28)),
            Child = new TextBlock
            {
                Text                = "NO PHOTO ON FILE",
                FontFamily          = new FontFamily("Consolas"),
                FontSize            = 10,
                FontWeight          = Microsoft.UI.Text.FontWeights.Bold,
                Foreground          = new SolidColorBrush(Color.FromArgb(255, 42, 64, 96)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                CharacterSpacing    = 150
            }
        };

        private void OpenIncidentDetails(Incident inc)
        {
            DetailContent.Children.Clear();

            Color statusColor; string statusLabel;
            switch (inc.Status)
            {
                case "Under Control":
                    statusColor = Color.FromArgb(255, 245, 158, 11); statusLabel = "UNDER CONTROL"; break;
                case "Fire Out":
                    statusColor = Color.FromArgb(255, 48, 161, 147); statusLabel = "FIRE OUT"; break;
                default:
                    statusColor = Color.FromArgb(255, 230, 57, 70); statusLabel = "ACTIVE"; break;
            }

            DetailAlarmText.Text                = $"ALARM {inc.AlarmLevel}";
            DetailStatusBadgeText.Text          = statusLabel;
            DetailAlarmBadge.Background         = new SolidColorBrush(statusColor);
            DetailStatusBadgeBorder.Background  = new SolidColorBrush(Color.FromArgb(50, statusColor.R, statusColor.G, statusColor.B));
            DetailStatusBadgeBorder.BorderBrush = new SolidColorBrush(statusColor);
            DetailColorBg.Background            = new SolidColorBrush(statusColor);

            if (!string.IsNullOrWhiteSpace(inc.PhotoPath) && File.Exists(inc.PhotoPath))
            {
                try
                {
                    DetailHeaderImage.Source     = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(inc.PhotoPath));
                    DetailHeaderImage.Visibility = Visibility.Visible;
                    DetailColorBg.Visibility     = Visibility.Collapsed;
                }
                catch
                {
                    DetailHeaderImage.Visibility = Visibility.Collapsed;
                    DetailColorBg.Visibility     = Visibility.Visible;
                }
            }
            else
            {
                DetailHeaderImage.Visibility = Visibility.Collapsed;
                DetailColorBg.Visibility     = Visibility.Visible;
            }

            var titleBlock = new StackPanel { Spacing = 3 };
            titleBlock.Children.Add(new TextBlock
            {
                Text       = inc.Barangay,
                FontSize   = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.ExtraBold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 217, 227, 242))
            });
            if (!string.IsNullOrWhiteSpace(inc.Sitio))
                titleBlock.Children.Add(new TextBlock
                {
                    Text       = $"Sitio {inc.Sitio}",
                    FontFamily = new FontFamily("Consolas"),
                    FontSize   = 11,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 71, 85, 105))
                });
            DetailContent.Children.Add(titleBlock);

            var metaBorder = new Border
            {
                Background      = new SolidColorBrush(Color.FromArgb(255, 15, 24, 34)),
                CornerRadius    = new CornerRadius(0),
                BorderThickness = new Thickness(1),
                BorderBrush     = new SolidColorBrush(Color.FromArgb(255, 20, 32, 46)),
                Padding         = new Thickness(16, 14, 16, 14)
            };
            var metaStack = new StackPanel { Spacing = 12 };
            metaStack.Children.Add(BuildDetailRow("CAUSE", string.IsNullOrWhiteSpace(inc.CauseOfFire) ? "—" : inc.CauseOfFire));
            metaStack.Children.Add(BuildDetailRow("DATE / TIME", string.IsNullOrWhiteSpace(inc.DateTime) ? "—" : inc.DateTime));
            metaStack.Children.Add(BuildDetailRow("DSWD STATUS", string.IsNullOrWhiteSpace(inc.DSDWStatus) ? "—" : inc.DSDWStatus));
            metaBorder.Child = metaStack;
            DetailContent.Children.Add(metaBorder);

            if (IsCitizenAffected(inc))
            {
                DetailDSWDBtn.Visibility = Visibility.Visible;
                DetailDSWDBtn.Tag        = inc;
                DetailDSWDBtn.Click     -= DetailDSWD_Click;
                DetailDSWDBtn.Click     += DetailDSWD_Click;
            }
            else
            {
                DetailDSWDBtn.Visibility = Visibility.Collapsed;
            }

            DetailPanelCol.Width = new GridLength(400);

            DetailMapLoading.Visibility = Visibility.Visible;
            LoadDetailMap(inc);
        }

        private async void LoadDetailMap(Incident inc)
        {
            try
            {
                _pendingFocusIncident = inc;

                if (!_mapCoreReady)
                {
                    await DetailMapWebView.EnsureCoreWebView2Async();
                    DetailMapWebView.CoreWebView2.Settings.IsWebMessageEnabled = true;
                    DetailMapWebView.NavigationCompleted += DetailMap_NavigationCompleted;
                    _mapCoreReady = true;

                    var mapPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Map", "citizen_map.html");
                    DetailMapWebView.Source = new Uri(mapPath);
                    return;
                }

                if (_mapHtmlLoaded)
                {
                    DetailMapLoading.Visibility = Visibility.Collapsed;
                    SendFocusIncident(inc);
                    _pendingFocusIncident = null;
                }
            }
            catch
            {
                DetailMapLoading.Visibility = Visibility.Collapsed;
                _pendingFocusIncident = null;
            }
        }

        private void DetailMap_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    _mapHtmlLoaded = true;
                    DetailMapLoading.Visibility = Visibility.Collapsed;

                    SendAllIncidents();

                    if (_pendingFocusIncident != null)
                    {
                        SendFocusIncident(_pendingFocusIncident);
                        _pendingFocusIncident = null;
                    }
                }
                catch {  }
            });
        }

        private void SendAllIncidents()
        {
            var sb = new StringBuilder();
            sb.Append("{\"type\":\"load_incidents\",\"incidents\":[");
            bool first = true;
            foreach (var inc in _allIncidents)
            {
                if (inc.GPSLat == 0 && inc.GPSLong == 0) continue;
                if (!first) sb.Append(',');
                first = false;
                var brgy = inc.Barangay.Replace("\"", "\\\"");
                var sitio = (inc.Sitio ?? "").Replace("\"", "\\\"");
                var cause = (inc.CauseOfFire ?? "").Replace("\"", "\\\"");
                var date = (inc.DateTime ?? "").Replace("\"", "\\\"");
                var status = inc.Status.Replace("\"", "\\\"");
                sb.Append($"{{\"id\":{inc.IncidentID},\"lat\":{inc.GPSLat},\"lng\":{inc.GPSLong}," +
                           $"\"barangay\":\"{brgy}\",\"sitio\":\"{sitio}\"," +
                           $"\"alarmLevel\":{inc.AlarmLevel},\"status\":\"{status}\"," +
                           $"\"cause\":\"{cause}\",\"date\":\"{date}\"}}");
            }
            sb.Append("]}");
            DetailMapWebView.CoreWebView2.PostWebMessageAsString(sb.ToString());
        }

        private void SendFocusIncident(Incident inc)
        {
            var brgy = inc.Barangay.Replace("\"", "\\\"");
            var sitio = (inc.Sitio ?? "").Replace("\"", "\\\"");
            var cause = (inc.CauseOfFire ?? "").Replace("\"", "\\\"");
            var date = (inc.DateTime ?? "").Replace("\"", "\\\"");
            var status = inc.Status.Replace("\"", "\\\"");
            var json = $"{{\"type\":\"focus_incident\",\"id\":{inc.IncidentID}," +
                       $"\"lat\":{inc.GPSLat},\"lng\":{inc.GPSLong}," +
                       $"\"barangay\":\"{brgy}\",\"sitio\":\"{sitio}\"," +
                       $"\"alarmLevel\":{inc.AlarmLevel},\"status\":\"{status}\"," +
                       $"\"cause\":\"{cause}\",\"date\":\"{date}\"}}";
            DetailMapWebView.CoreWebView2.PostWebMessageAsString(json);
        }

        private async void DetailDSWD_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).Tag is Incident inc)
                await ShowDSWDMessageDialog(inc);
        }

        private async void ReliefDSWD_Click(object sender, RoutedEventArgs e)
        {
            if (_reliefLinkedIncidentId == null) return;
            var inc = _allIncidents.FirstOrDefault(i => i.IncidentID == _reliefLinkedIncidentId.Value);
            if (inc == null) return;
            await ShowDSWDMessageDialog(inc);
        }

        private void CloseDetail_Click(object sender, RoutedEventArgs e)
        {
            DetailPanelCol.Width         = new GridLength(0);
            DetailHeaderImage.Visibility = Visibility.Collapsed;
            DetailColorBg.Visibility     = Visibility.Visible;
            _pendingFocusIncident        = null;
        }

        private static Grid BuildDetailRow(string label, string value)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var lbl = new TextBlock
            {
                Text              = label,
                FontFamily        = new FontFamily("Consolas"),
                FontSize          = 9,
                Foreground        = new SolidColorBrush(Color.FromArgb(255, 74, 107, 138)),
                CharacterSpacing  = 150,
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetColumn(lbl, 0);
            var val = new TextBlock
            {
                Text         = value,
                TextWrapping = TextWrapping.Wrap,
                FontFamily   = new FontFamily("Consolas"),
                FontSize     = 12,
                FontWeight   = Microsoft.UI.Text.FontWeights.Bold,
                Foreground   = new SolidColorBrush(White)
            };
            Grid.SetColumn(val, 1);
            g.Children.Add(lbl);
            g.Children.Add(val);
            return g;
        }

        private static bool IsCitizenAffected(Incident inc)
        {
            var brgy = SessionManager.AssignedBarangay;
            if (string.IsNullOrWhiteSpace(brgy)) return false;
            if (!string.Equals(brgy, inc.Barangay, StringComparison.OrdinalIgnoreCase)) return false;
            return inc.Status == "Active" || inc.Status == "Under Control";
        }

        private void ShowReliefGate()
        {
            _reliefPhase                 = "Gate";
            _reliefLinkedIncidentId      = null;
            ReliefGatePanel.Visibility   = Visibility.Visible;
            ReliefNoPanel.Visibility     = Visibility.Collapsed;
            ReliefFormPanel.Visibility   = Visibility.Collapsed;
            ReliefResultPanel.Visibility = Visibility.Collapsed;
        }

        private void YesAffected_Click(object sender, RoutedEventArgs e)
        {
            _reliefPhase = "Form";
            ReliefGatePanel.Visibility   = Visibility.Collapsed;
            ReliefNoPanel.Visibility     = Visibility.Collapsed;
            ReliefFormPanel.Visibility   = Visibility.Visible;
            ReliefResultPanel.Visibility = Visibility.Collapsed;
            ReliefLookupErrorPanel.Visibility = Visibility.Collapsed;
            ReliefHeadNameBox.Text = string.Empty;

            try
            {
                ReliefEvacCombo.Items.Clear();
                var brgy = SessionManager.AssignedBarangay;
                _reliefEvacCenters = string.IsNullOrWhiteSpace(brgy)
                    ? _evacRepo.GetAll()
                    : _evacRepo.GetByBarangay(brgy);
                foreach (var c in _reliefEvacCenters)
                    ReliefEvacCombo.Items.Add(c.Name);
                if (_reliefEvacCenters.Count > 0) ReliefEvacCombo.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                _reliefEvacCenters = new();
                ShowReliefLookupError($"Could not load evacuation centers: {ex.Message}");
            }
        }

        private void NoBrowsing_Click(object sender, RoutedEventArgs e)
        {
            _reliefPhase = "No";
            ReliefGatePanel.Visibility   = Visibility.Collapsed;
            ReliefNoPanel.Visibility     = Visibility.Visible;
            ReliefFormPanel.Visibility   = Visibility.Collapsed;
            ReliefResultPanel.Visibility = Visibility.Collapsed;
            BuildNoPathSummary();
        }

        private void BuildNoPathSummary()
        {
            ReliefNoContent.Children.Clear();
            var consolas = new FontFamily("Consolas");

            ReliefNoContent.Children.Add(new TextBlock
            {
                Text             = "CITY-WIDE RELIEF OVERVIEW",
                FontFamily       = consolas,
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground       = new SolidColorBrush(Color.FromArgb(255, 160, 180, 200)),
                CharacterSpacing = 100
            });
            ReliefNoContent.Children.Add(new TextBlock
            {
                Text         = "You are viewing city-wide aggregate data. Individual household records are only available to registered affected residents.",
                FontFamily   = consolas,
                FontSize = 10,
                Foreground   = new SolidColorBrush(Color.FromArgb(255, 74, 107, 138)),
                TextWrapping = TextWrapping.Wrap
            });
            ReliefNoContent.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(255, 17, 28, 40)) });

            int total = _allIncidents.Count;
            int active = _allIncidents.Count(i => i.Status == "Active");
            int uc = _allIncidents.Count(i => i.Status == "Under Control");
            int fo = _allIncidents.Count(i => i.Status == "Fire Out");

            var statRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
            statRow.Children.Add(BuildMiniStat("TOTAL INCIDENTS", total.ToString(), Color.FromArgb(255, 148, 163, 184)));
            statRow.Children.Add(BuildMiniStat("ACTIVE", active.ToString(), Color.FromArgb(255, 230, 57, 70)));
            statRow.Children.Add(BuildMiniStat("UNDER CONTROL", uc.ToString(), Color.FromArgb(255, 245, 158, 11)));
            statRow.Children.Add(BuildMiniStat("FIRE OUT", fo.ToString(), Color.FromArgb(255, 20, 184, 166)));
            ReliefNoContent.Children.Add(statRow);

            ReliefNoContent.Children.Add(new Border
            {
                Background   = new SolidColorBrush(Color.FromArgb(255, 15, 24, 34)),
                CornerRadius = new CornerRadius(0),
                Padding      = new Thickness(16, 12, 16, 12),
                Child = new TextBlock
                {
                    Text         = "To view specific relief distributions for your household, go back and select \"YES, I WAS AFFECTED\".",
                    FontFamily   = consolas,
                    FontSize = 10,
                    Foreground   = new SolidColorBrush(Color.FromArgb(255, 74, 107, 138)),
                    TextWrapping = TextWrapping.Wrap
                }
            });

            var backBtn = new Button
            {
                Background      = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                BorderThickness = new Thickness(1),
                BorderBrush     = new SolidColorBrush(Color.FromArgb(255, 30, 45, 61)),
                CornerRadius    = new CornerRadius(0),
                Padding         = new Thickness(14, 8, 14, 8),
                HorizontalAlignment = HorizontalAlignment.Center,
                Content = new TextBlock { Text = "← BACK TO GATE", FontFamily = consolas, FontSize = 9, Foreground = new SolidColorBrush(Color.FromArgb(255, 74, 107, 138)), CharacterSpacing = 80 }
            };
            backBtn.Click += BackToGate_Click;
            ReliefNoContent.Children.Add(backBtn);
        }

        private void BackToGate_Click(object sender, RoutedEventArgs e) => ShowReliefGate();

        private void BackToForm_Click(object sender, RoutedEventArgs e)
        {
            _reliefPhase                      = "Form";
            _reliefLinkedIncidentId           = null;
            ReliefDSWDBtn.Visibility          = Visibility.Collapsed;
            ReliefFormPanel.Visibility        = Visibility.Visible;
            ReliefResultPanel.Visibility      = Visibility.Collapsed;
            ReliefLookupErrorPanel.Visibility = Visibility.Collapsed;
            ReliefHeadNameBox.Text            = string.Empty;
        }

        private void ReliefLookup_Click(object sender, RoutedEventArgs e)
        {
            ReliefLookupErrorPanel.Visibility = Visibility.Collapsed;

            int selectedIdx = ReliefEvacCombo.SelectedIndex;
            if (selectedIdx < 0 || selectedIdx >= _reliefEvacCenters.Count)
            {
                ShowReliefLookupError("Please select an evacuation center.");
                return;
            }

            var headName = (ReliefHeadNameBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(headName))
            {
                ShowReliefLookupError("Please enter the head of household's full name.");
                return;
            }

            var center = _reliefEvacCenters[selectedIdx];
            try
            {
                var family = _familyRepo.GetByHeadNameAndCenter(headName, center.CenterID);
                if (family == null)
                    ShowLookupNotFound(headName, center.Name);
                else
                {
                    var inc = _allIncidents.FirstOrDefault(i => i.IncidentID == family.IncidentID);
                    ShowLookupFound(family, inc, center, _reliefRepo.GetByFamily(family.FamilyID));
                }
            }
            catch (Exception ex)
            {
                ShowReliefLookupError($"Lookup failed: {ex.Message}");
            }
        }

        private void ShowReliefLookupError(string msg)
        {
            ReliefLookupErrorText.Text        = msg;
            ReliefLookupErrorPanel.Visibility = Visibility.Visible;
        }

        private void ShowLookupNotFound(string name, string centerName)
        {
            _reliefPhase                 = "Result";
            _reliefLinkedIncidentId      = null;
            ReliefDSWDBtn.Visibility     = Visibility.Collapsed;
            ReliefFormPanel.Visibility   = Visibility.Collapsed;
            ReliefResultPanel.Visibility = Visibility.Visible;
            ReliefResultContent.Children.Clear();

            var consolas = new FontFamily("Consolas");
            var card = new Border
            {
                Background      = new SolidColorBrush(Color.FromArgb(255, 22, 8, 8)),
                CornerRadius    = new CornerRadius(0),
                BorderThickness = new Thickness(0),
                Padding         = new Thickness(20, 18, 20, 18)
            };
            var innerCard = new Border
            {
                BorderThickness = new Thickness(4, 0, 0, 0),
                BorderBrush     = new SolidColorBrush(Color.FromArgb(255, 192, 80, 80)),
                Padding         = new Thickness(16, 0, 0, 0),
                Child = new StackPanel
                {
                    Spacing = 8,
                    Children =
                {
                    new TextBlock { Text = "FAMILY NOT REGISTERED", FontFamily = consolas, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.ExtraBold, Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 100, 100)), CharacterSpacing = 80 },
                    new TextBlock { Text = $"No record found for \"{name}\" at {centerName}.", FontFamily = consolas, FontSize = 11, Foreground = new SolidColorBrush(Color.FromArgb(255, 200, 160, 160)), TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = "Please contact your Barangay Official to register your household first.", FontFamily = consolas, FontSize = 10, Foreground = new SolidColorBrush(Color.FromArgb(255, 140, 100, 100)), TextWrapping = TextWrapping.Wrap }
                }
                }
            };
            card.Child = innerCard;
            ReliefResultContent.Children.Add(card);
        }

        private void ShowLookupFound(Family family, Incident? inc, EvacuationCenter center, List<ReliefRecord> records)
        {
            _reliefPhase                 = "Result";
            _reliefLinkedIncidentId      = family.IncidentID;
            _lastFoundFamily             = family;
            _lastFoundInc                = inc;
            _lastFoundCenter             = center;
            ReliefDSWDBtn.Visibility     = Visibility.Visible;
            ReliefFormPanel.Visibility   = Visibility.Collapsed;
            ReliefResultPanel.Visibility = Visibility.Visible;
            ReliefResultContent.Children.Clear();

            var consolas = new FontFamily("Consolas");

            var summaryCard = new Border
            {
                Background      = new SolidColorBrush(Color.FromArgb(255, 9, 20, 30)),
                CornerRadius    = new CornerRadius(0),
                BorderThickness = new Thickness(0),
                Padding         = new Thickness(0)
            };
            var summaryInner = new Border
            {
                BorderThickness = new Thickness(4, 0, 0, 0),
                BorderBrush     = new SolidColorBrush(Color.FromArgb(255, 255, 193, 7)),
                Padding         = new Thickness(16, 16, 16, 16)
            };
            var summaryStack = new StackPanel { Spacing = 10 };
            summaryStack.Children.Add(new TextBlock { Text = "HOUSEHOLD RELIEF RECORD", FontFamily = consolas, FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 193, 7)), CharacterSpacing = 100 });
            summaryStack.Children.Add(BuildDetailRow2("HEAD OF FAMILY", family.HeadName, consolas));
            summaryStack.Children.Add(BuildDetailRow2("MEMBERS", family.MemberCount.ToString(), consolas));
            summaryStack.Children.Add(BuildDetailRow2("EVAC CENTER", center.Name, consolas));
            summaryStack.Children.Add(BuildDetailRow2("RELIEF STATUS", family.ReliefStatus, consolas));
            if (inc != null)
            {
                summaryStack.Children.Add(BuildDetailRow2("INCIDENT", $"#{inc.IncidentID} · Brgy. {inc.Barangay}", consolas));
                if (!string.IsNullOrWhiteSpace(inc.Sitio))
                    summaryStack.Children.Add(BuildDetailRow2("SITIO", inc.Sitio, consolas));
            }
            summaryInner.Child = summaryStack;
            summaryCard.Child  = summaryInner;
            ReliefResultContent.Children.Add(summaryCard);

            ReliefResultContent.Children.Add(new TextBlock
            {
                Text             = $"RELIEF ITEMS RECEIVED  ({records.Count})",
                FontFamily       = consolas,
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground       = new SolidColorBrush(Color.FromArgb(255, 255, 193, 7)),
                CharacterSpacing = 120,
                Margin = new Thickness(0, 4, 0, 0)
            });

            if (records.Count == 0)
            {
                ReliefResultContent.Children.Add(new Border
                {
                    Background      = new SolidColorBrush(Color.FromArgb(255, 15, 26, 38)),
                    CornerRadius    = new CornerRadius(0),
                    BorderThickness = new Thickness(1),
                    BorderBrush     = new SolidColorBrush(Color.FromArgb(255, 30, 45, 61)),
                    Padding         = new Thickness(18, 14, 18, 14),
                    Child = new TextBlock
                    {
                        Text             = "NO RELIEF DISTRIBUTED YET — check back later",
                        FontFamily       = consolas,
                        FontSize = 10,
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        Foreground       = new SolidColorBrush(Color.FromArgb(255, 200, 154, 0)),
                        CharacterSpacing = 60
                    }
                });
            }
            else
            {
                var tableHost = new Border
                {
                    Background      = new SolidColorBrush(Color.FromArgb(255, 15, 26, 38)),
                    CornerRadius    = new CornerRadius(0),
                    BorderThickness = new Thickness(1),
                    BorderBrush     = new SolidColorBrush(Color.FromArgb(255, 30, 45, 61))
                };
                var tableStack = new StackPanel();
                tableStack.Children.Add(BuildReliefTableRow(true, "AGENCY", "ITEM", "QTY", "DATE"));
                foreach (var r in records)
                    tableStack.Children.Add(BuildReliefTableRow(false, r.AgencyName, r.ItemType, r.Quantity.ToString(), r.DateDistributed));
                tableHost.Child = tableStack;
                ReliefResultContent.Children.Add(tableHost);
            }

            var dswdNotice = new Border
            {
                BorderThickness = new Thickness(4, 0, 0, 0),
                BorderBrush     = new SolidColorBrush(Color.FromArgb(255, 96, 165, 250)),
                Background      = new SolidColorBrush(Color.FromArgb(255, 10, 26, 46)),
                Padding         = new Thickness(16, 14, 16, 14),
                Margin          = new Thickness(0, 4, 0, 0)
            };
            var noticeStack = new StackPanel { Spacing = 8 };
            noticeStack.Children.Add(new TextBlock
            {
                Text             = "DSWD NOTICE",
                FontFamily       = consolas,
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.ExtraBold,
                Foreground       = new SolidColorBrush(Color.FromArgb(255, 96, 165, 250)),
                CharacterSpacing = 100
            });
            noticeStack.Children.Add(new TextBlock
            {
                Text         = "If you have not yet been visited by a DSWD social worker, coordinate with your barangay official or visit the nearest DSWD field office. A social worker will assist your household with needs assessment, referral services, and additional relief assistance.",
                FontFamily   = consolas,
                FontSize = 10,
                Foreground   = new SolidColorBrush(Color.FromArgb(200, 147, 197, 253)),
                TextWrapping = TextWrapping.Wrap
            });
            dswdNotice.Child = noticeStack;
            ReliefResultContent.Children.Add(dswdNotice);

            var msgsContainer = new StackPanel { Tag = "DswdMsgsContainer", Spacing = 6 };
            ReliefResultContent.Children.Add(msgsContainer);
            BuildDSWDMessagesUI(msgsContainer, inc);
        }

        private void BuildDSWDMessagesUI(StackPanel container, Incident? inc)
        {
            container.Children.Clear();
            var consolas = new FontFamily("Consolas");

            List<DSWDMessage> myMsgs;
            try
            {
                myMsgs = _dswdMsgRepo.GetBySender(SessionManager.UserID);
            }
            catch (Exception ex)
            {
                container.Children.Add(BuildInlineError($"Could not load DSWD messages: {ex.Message}"));
                return;
            }
            var relatedMsgs = myMsgs
                .Where(m => inc != null && m.IncidentID == inc.IncidentID)
                .OrderByDescending(m => m.SentAt)
                .ToList();

            if (relatedMsgs.Count == 0) return;

            container.Children.Add(new TextBlock
            {
                Text             = $"YOUR DSWD MESSAGES ({relatedMsgs.Count})",
                FontFamily       = consolas,
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground       = new SolidColorBrush(Color.FromArgb(255, 96, 165, 250)),
                CharacterSpacing = 120,
                Margin = new Thickness(0, 8, 0, 0)
            });

            foreach (var msg in relatedMsgs)
            {
                Color accent = msg.Status == "Approved" ? Color.FromArgb(255, 76, 175, 144)
                             : msg.Status == "Rejected" ? Color.FromArgb(255, 192, 80, 80)
                             : Color.FromArgb(255, 192, 154, 48);

                var msgCard = new Border
                {
                    BorderThickness = new Thickness(3, 0, 0, 0),
                    BorderBrush     = new SolidColorBrush(accent),
                    Background      = new SolidColorBrush(Color.FromArgb(255, 9, 19, 28)),
                    Padding         = new Thickness(14, 12, 14, 12),
                    Margin          = new Thickness(0, 0, 0, 4)
                };
                var msgStack = new StackPanel { Spacing = 6 };

                var topRow = new Grid();
                var badgeRow = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    VerticalAlignment = VerticalAlignment.Center
                };
                badgeRow.Children.Add(new Border
                {
                    Padding    = new Thickness(8, 3, 8, 3),
                    Background = new SolidColorBrush(Color.FromArgb(40, accent.R, accent.G, accent.B)),
                    Child = new TextBlock
                    {
                        Text       = msg.Status.ToUpper(),
                        FontFamily = consolas,
                        FontSize = 8,
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        Foreground = new SolidColorBrush(accent)
                    }
                });
                badgeRow.Children.Add(new TextBlock
                {
                    Text              = msg.SentAt,
                    FontFamily        = consolas,
                    FontSize = 8,
                    Foreground        = new SolidColorBrush(Color.FromArgb(255, 58, 85, 112)),
                    VerticalAlignment = VerticalAlignment.Center
                });
                topRow.Children.Add(badgeRow);

                var deleteBtn = new Button
                {
                    Content = new TextBlock
                    {
                        Text = "x DELETE",
                        FontFamily = consolas,
                        FontSize = 8,
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 80, 100, 120)),
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold
                    },
                    Background      = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                    BorderThickness = new Thickness(0),
                    Padding         = new Thickness(6, 2, 0, 2),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment   = VerticalAlignment.Center
                };
                deleteBtn.Click += (s, _) =>
                {
                    try
                    {
                        _dswdMsgRepo.HideFromCitizenHistory(msg.MessageID, SessionManager.UserID);
                        RefreshDSWDMessagesSection();
                    }
                    catch (Exception ex)
                    {
                        container.Children.Insert(0, new TextBlock
                        {
                            Text = $"Delete failed: {ex.Message}",
                            FontFamily = consolas,
                            FontSize = 10,
                            Foreground = new SolidColorBrush(Color.FromArgb(255, 230, 90, 90)),
                            TextWrapping = TextWrapping.Wrap
                        });
                    }
                };
                topRow.Children.Add(deleteBtn);
                msgStack.Children.Add(topRow);

                msgStack.Children.Add(new TextBlock
                {
                    Text         = msg.Message,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily   = consolas,
                    FontSize = 10,
                    Foreground   = new SolidColorBrush(Color.FromArgb(255, 180, 200, 220))
                });

                if (msg.Status == "Rejected" && !string.IsNullOrWhiteSpace(msg.RejectionReason))
                {
                    msgStack.Children.Add(new Border
                    {
                        Background  = new SolidColorBrush(Color.FromArgb(255, 28, 12, 12)),
                        Padding     = new Thickness(10, 7, 10, 7),
                        Child = new StackPanel
                        {
                            Spacing = 2,
                            Children =
                        {
                            new TextBlock { Text = "REASON FOR REJECTION",
                                FontFamily = consolas, FontSize = 8,
                                Foreground = new SolidColorBrush(Color.FromArgb(255, 192, 80, 80)),
                                FontWeight = Microsoft.UI.Text.FontWeights.Bold, CharacterSpacing = 150 },
                            new TextBlock { Text = msg.RejectionReason,
                                FontFamily = consolas, FontSize = 10,
                                Foreground = new SolidColorBrush(Color.FromArgb(255, 220, 150, 150)),
                                TextWrapping = TextWrapping.Wrap }
                        }
                        }
                    });
                }

                msgCard.Child = msgStack;
                container.Children.Add(msgCard);
            }
        }

        private void RefreshDSWDMessagesSection()
        {
            if (_reliefPhase != "Result") return;

            StackPanel? container = null;
            foreach (var child in ReliefResultContent.Children)
                if (child is StackPanel sp && sp.Tag is string t && t == "DswdMsgsContainer")
                { container = sp; break; }

            if (container == null) return;
            BuildDSWDMessagesUI(container, _lastFoundInc);
        }

        private static Grid BuildDetailRow2(string label, string value, FontFamily consolas)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var lbl = new TextBlock { Text = label, FontFamily = consolas, FontSize = 9, Foreground = new SolidColorBrush(Color.FromArgb(255, 74, 107, 138)), CharacterSpacing = 120, VerticalAlignment = VerticalAlignment.Top };
            var val = new TextBlock { Text = value, FontFamily = consolas, FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(White), TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(lbl, 0); Grid.SetColumn(val, 1);
            g.Children.Add(lbl); g.Children.Add(val);
            return g;
        }

        private static Grid BuildReliefTableRow(bool isHeader, string c1, string c2, string c3, string c4)
        {
            var g = new Grid
            {
                Padding    = new Thickness(14, 10, 14, 10),
                Background = isHeader
                    ? new SolidColorBrush(Color.FromArgb(255, 13, 30, 46))
                    : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0))
            };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            for (int i = 0; i < 4; i++)
            {
                string text = i switch { 0 => c1, 1 => c2, 2 => c3, _ => c4 };
                var tb = new TextBlock
                {
                    Text             = text,
                    TextWrapping     = TextWrapping.Wrap,
                    FontFamily       = new FontFamily("Consolas"),
                    FontSize         = isHeader ? 9 : 10,
                    Foreground       = new SolidColorBrush(isHeader ? Color.FromArgb(255, 74, 107, 138) : White),
                    FontWeight       = isHeader ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.SemiBold,
                    CharacterSpacing = isHeader ? 150 : 0,
                    Margin           = new Thickness(0, 0, 6, 0)
                };
                Grid.SetColumn(tb, i);
                g.Children.Add(tb);
            }
            return g;
        }

        private static StackPanel BuildMiniStat(string label, string value, Color valueColor)
        {
            var sp = new StackPanel { Spacing = 2 };
            sp.Children.Add(new TextBlock { Text = label, FontFamily = new FontFamily("Consolas"), FontSize = 8, Foreground = new SolidColorBrush(Color.FromArgb(255, 74, 107, 138)), CharacterSpacing = 120 });
            sp.Children.Add(new TextBlock { Text = value, FontFamily = new FontFamily("Consolas"), FontSize = 22, FontWeight = Microsoft.UI.Text.FontWeights.ExtraBold, Foreground = new SolidColorBrush(valueColor) });
            return sp;
        }



        private void ReportFireBtn_Click(object sender, RoutedEventArgs e) => OpenReportOverlay();
        private void CloseReportBtn_Click(object sender, RoutedEventArgs e)
            => ReportFireOverlay.Visibility = Visibility.Collapsed;

        private async void OpenReportOverlay()
        {
            if (string.IsNullOrEmpty(SessionManager.PhoneNumber))
            {
                await FireTrackDialog.ShowInfoAsync(
                    this,
                    "PHONE NUMBER REQUIRED",
                    "Your account doesn't have a phone number on file. Please re-register or contact your barangay.",
                    "OK");
                return;
            }

            ReporterNameText.Text  = SessionManager.FullName;
            ReporterPhoneText.Text = SessionManager.PhoneNumber;

            if (!_reportOverlayInitialized)
            {
                try
                {
                    foreach (var b in _barangayRepo.GetAllNames())
                        ReportBarangayCombo.Items.Add(b);
                    _reportOverlayInitialized = true;
                }
                catch (Exception ex)
                {
                    ShowReportError($"Could not load barangays: {ex.Message}");
                    ReportFireOverlay.Visibility = Visibility.Visible;
                    return;
                }
            }

            if (!string.IsNullOrEmpty(SessionManager.AssignedBarangay))
            {
                int matchIdx = -1;
                for (int i = 0; i < ReportBarangayCombo.Items.Count; i++)
                {
                    if ((ReportBarangayCombo.Items[i] as string) == SessionManager.AssignedBarangay)
                    { matchIdx = i; break; }
                }
                ReportBarangayCombo.SelectedIndex = matchIdx;
            }
            else
            {
                ReportBarangayCombo.SelectedIndex = -1;
            }

            ReportAddressBox.Text          = string.Empty;
            ReportErrorBanner.Visibility   = Visibility.Collapsed;
            ReportSuccessBanner.Visibility = Visibility.Collapsed;
            SubmitNormalState.Visibility   = Visibility.Visible;
            SubmitLoadingState.Visibility  = Visibility.Collapsed;
            SubmitReportBtn.IsEnabled      = true;
            ReportFireOverlay.Visibility   = Visibility.Visible;
        }

        private async void SubmitReportBtn_Click(object sender, RoutedEventArgs e)
        {
            ReportErrorBanner.Visibility   = Visibility.Collapsed;
            ReportSuccessBanner.Visibility = Visibility.Collapsed;

            var address = (ReportAddressBox.Text ?? string.Empty).Trim();
            var barangay = ReportBarangayCombo.SelectedItem as string;

            if (string.IsNullOrWhiteSpace(address))
            { ShowReportError("Please enter an incident address or landmark."); return; }
            if (string.IsNullOrWhiteSpace(barangay))
            { ShowReportError("Please select a barangay."); return; }

            SubmitReportBtn.IsEnabled     = false;
            SubmitNormalState.Visibility  = Visibility.Collapsed;
            SubmitLoadingState.Visibility = Visibility.Visible;

            await Task.Delay(700);
            try
            {
                _citizenReportRepo.Insert(new CitizenReport
                {
                    ReporterID = SessionManager.UserID,
                    FullName   = SessionManager.FullName,
                    Phone      = SessionManager.PhoneNumber,
                    Address    = address,
                    Barangay   = barangay!,
                    Status     = "Pending",
                    IsVerified = false
                });

                ReportSuccessText.Text         = "Report submitted. BFP and your barangay have been notified.";
                ReportSuccessBanner.Visibility = Visibility.Visible;
                await Task.Delay(1500);
                ReportFireOverlay.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                ShowReportError($"Submission failed: {ex.Message}");
            }
            finally
            {
                SubmitNormalState.Visibility  = Visibility.Visible;
                SubmitLoadingState.Visibility = Visibility.Collapsed;
                SubmitReportBtn.IsEnabled     = true;
            }
        }

        private void ShowReportError(string msg)
        {
            ReportErrorText.Text         = msg;
            ReportErrorBanner.Visibility = Visibility.Visible;
        }

        private Task ShowDSWDMessageDialog(Incident inc)
        {
            _dswdTargetIncident             = inc;
            DSWDContextText.Text            = $"Re: Incident #{inc.IncidentID} \u00B7 Brgy. {inc.Barangay} \u00B7 Status: {inc.Status}";
            DSWDMessageBox.Text             = string.Empty;
            DSWDErrorBanner.Visibility      = Visibility.Collapsed;
            DSWDSuccessBanner.Visibility    = Visibility.Collapsed;
            DSWDSendNormalState.Visibility  = Visibility.Visible;
            DSWDSendLoadingState.Visibility = Visibility.Collapsed;
            DSWDSendBtn.IsEnabled           = true;
            DSWDOverlay.Visibility          = Visibility.Visible;
            return Task.CompletedTask;
        }

        private void CloseDSWDBtn_Click(object sender, RoutedEventArgs e)
            => DSWDOverlay.Visibility = Visibility.Collapsed;

        private async void DSWDSendBtn_Click(object sender, RoutedEventArgs e)
        {
            DSWDErrorBanner.Visibility   = Visibility.Collapsed;
            DSWDSuccessBanner.Visibility = Visibility.Collapsed;

            var text = (DSWDMessageBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(text))
            {
                DSWDErrorText.Text         = "Please type a message before sending.";
                DSWDErrorBanner.Visibility = Visibility.Visible;
                return;
            }
            if (_dswdTargetIncident == null) return;

            DSWDSendBtn.IsEnabled           = false;
            DSWDSendNormalState.Visibility  = Visibility.Collapsed;
            DSWDSendLoadingState.Visibility = Visibility.Visible;

            await Task.Delay(500);
            try
            {
                _dswdMsgRepo.Insert(new DSWDMessage
                {
                    SenderID   = SessionManager.UserID,
                    IncidentID = _dswdTargetIncident.IncidentID,
                    Message    = text
                });
                DSWDSuccessText.Text         = "Your message has been sent to DSWD successfully.";
                DSWDSuccessBanner.Visibility = Visibility.Visible;
                await Task.Delay(1500);
                DSWDOverlay.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                DSWDErrorText.Text         = $"Send failed: {ex.Message}";
                DSWDErrorBanner.Visibility = Visibility.Visible;
            }
            finally
            {
                DSWDSendNormalState.Visibility  = Visibility.Visible;
                DSWDSendLoadingState.Visibility = Visibility.Collapsed;
                DSWDSendBtn.IsEnabled           = true;
            }
        }
    }
}
