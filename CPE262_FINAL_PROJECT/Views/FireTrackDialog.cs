using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using System.Threading.Tasks;
using Windows.UI;

namespace CPE262_FINAL_PROJECT.Views
{
    internal static class FireTrackDialog
    {
        public static Task ShowInfoAsync(Page owner, string title, string message, string closeText = "CLOSE")
            => ShowAsync(owner, title, BuildMessage(message), primaryText: null, closeText).ContinueWith(_ => { });

        public static Task<bool> ShowConfirmAsync(
            Page owner,
            string title,
            string message,
            string primaryText,
            string closeText = "CANCEL",
            bool danger = false)
            => ShowAsync(owner, title, BuildMessage(message), primaryText, closeText, danger);

        public static Task<bool> ShowCustomConfirmAsync(
            Page owner,
            string title,
            UIElement content,
            string primaryText,
            string closeText = "CANCEL",
            bool danger = false)
            => ShowAsync(owner, title, content, primaryText, closeText, danger);

        private static Task<bool> ShowAsync(
            Page owner,
            string title,
            UIElement body,
            string? primaryText,
            string closeText,
            bool danger = false)
        {
            var tcs = new TaskCompletionSource<bool>();
            var consolas = new FontFamily("Consolas");
            var panelBg = new SolidColorBrush(Color.FromArgb(255, 12, 26, 38));
            var headerBg = new SolidColorBrush(Color.FromArgb(255, 15, 32, 53));
            var fieldBg = new SolidColorBrush(Color.FromArgb(255, 9, 19, 28));
            var border = new SolidColorBrush(Color.FromArgb(255, 23, 36, 53));
            var muted = new SolidColorBrush(Color.FromArgb(255, 58, 85, 112));
            var text = new SolidColorBrush(Color.FromArgb(255, 200, 216, 232));
            var accent = danger
                ? new SolidColorBrush(Color.FromArgb(255, 192, 80, 80))
                : new SolidColorBrush(Color.FromArgb(255, 74, 142, 194));

            var overlay = new Grid
            {
                Width = owner.ActualWidth > 0 ? owner.ActualWidth : 1100,
                Height = owner.ActualHeight > 0 ? owner.ActualHeight : 720,
                Background = new SolidColorBrush(Color.FromArgb(204, 0, 0, 0))
            };

            var header = new Border
            {
                Background = headerBg,
                BorderBrush = border,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(22, 16, 22, 14),
                Child = new StackPanel
                {
                    Spacing = 3,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = title,
                            FontFamily = consolas,
                            FontSize = 14,
                            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                            Foreground = text,
                            CharacterSpacing = 120
                        },
                        new TextBlock
                        {
                            Text = "FIRETRACK SYSTEM PROMPT",
                            FontFamily = consolas,
                            FontSize = 9,
                            Foreground = muted,
                            CharacterSpacing = 90
                        }
                    }
                }
            };

            var contentBox = new Border
            {
                Background = fieldBg,
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16, 14, 16, 14),
                Child = body
            };

            var buttonRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            Button BuildButton(string label, SolidColorBrush bg, SolidColorBrush fg, SolidColorBrush br)
            {
                return new Button
                {
                    Content = new TextBlock
                    {
                        Text = label,
                        FontFamily = consolas,
                        FontSize = 10,
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        Foreground = fg,
                        CharacterSpacing = 80
                    },
                    Background = bg,
                    BorderBrush = br,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(14, 9, 14, 9),
                    CornerRadius = new CornerRadius(3)
                };
            }

            var cancelBtn = BuildButton(closeText, fieldBg, muted, border);
            buttonRow.Children.Add(cancelBtn);

            Button? primaryBtn = null;
            if (!string.IsNullOrWhiteSpace(primaryText))
            {
                primaryBtn = BuildButton(primaryText!, accent, new SolidColorBrush(Color.FromArgb(255, 9, 19, 28)), accent);
                buttonRow.Children.Add(primaryBtn);
            }

            var panel = new Border
            {
                Width = 440,
                MaxWidth = 520,
                Background = panelBg,
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new StackPanel
                {
                    Children =
                    {
                        header,
                        new StackPanel
                        {
                            Spacing = 14,
                            Padding = new Thickness(22, 18, 22, 18),
                            Children = { contentBox, buttonRow }
                        }
                    }
                }
            };
            overlay.Children.Add(panel);

            var popup = new Popup
            {
                XamlRoot = owner.XamlRoot,
                Child = overlay,
                IsLightDismissEnabled = false
            };

            SizeChangedEventHandler? resizeHandler = null;
            resizeHandler = (s, e) =>
            {
                overlay.Width = owner.ActualWidth;
                overlay.Height = owner.ActualHeight;
            };
            owner.SizeChanged += resizeHandler;

            void Close(bool result)
            {
                popup.IsOpen = false;
                if (resizeHandler is not null) owner.SizeChanged -= resizeHandler;
                tcs.TrySetResult(result);
            }

            cancelBtn.Click += (s, e) => Close(false);
            if (primaryBtn is not null) primaryBtn.Click += (s, e) => Close(true);

            popup.IsOpen = true;
            return tcs.Task;
        }

        private static TextBlock BuildMessage(string message) => new()
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 200, 216, 232))
        };
    }
}
