    using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Documents;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BigData.UI {

    /// <summary>
    /// InfoGrid is a fullscreen overlay for the display that shows a
    /// Publication in detail.
    /// </summary>
    public class InfoGrid : Grid {

        /// <summary>
        /// Create and initialize a new InfoGrid
        /// </summary>
        /// <param name="pub">The publication to show</param>
        public InfoGrid(Publication pub) {
            publication = pub;

            Opacity = 0;
            Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0));

            SetupGrid();

            AddCoverImage();
            AddInfoPanel();

            SetupInputPanel();

            Loaded += AnimateIn;
            StylusSystemGesture += BackgroundGesture;
        }

        /// <summary>
        /// Raised after the InfoGrid has animated off the screen
        /// </summary>
        public event RoutedEventHandler Done {
            add { AddHandler(DoneEvent, value); }
            remove { RemoveHandler(DoneEvent, value); }
        }

        /// <summary>
        /// Raised after the InfoGrid has sent a user an email
        /// </summary>
        public event RoutedEventHandler EmailSent {
            add { AddHandler(EmailSentEvent, value); }
            remove { RemoveHandler(EmailSentEvent, value); }
        }

        Publication publication;
        StackPanel inputPanel;
        TextBox usernameBox;
        TextBlock borrowLabel;
        TextBlock description;
        StackPanel infoPanel;

        void SetupGrid() {
            ColumnDefinitions.Add(new ColumnDefinition() {
                Width = new GridLength(2, GridUnitType.Star)
            });
            ColumnDefinitions.Add(new ColumnDefinition() {
                Width = new GridLength(3, GridUnitType.Star)
            });
        }

        void AddCoverImage() {
            var image = new Image() {
                Source = publication.CoverImage,
                Margin = new Thickness { Left = 0, Right = 0, Top = 200, Bottom = 200 },
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(image, 0);
            Children.Add(image);
        }

        void AddInfoPanel() {
            infoPanel = new StackPanel {
                Orientation = Orientation.Vertical,
            };
            Grid.SetColumn(infoPanel, 1);
            Children.Add(infoPanel);

            AddTitle();
            AddAuthor();
            AddDescription();
            AddBorrowLabel();
        }

        void AddTitle() {
            var title = new TextBlock {
                Text = publication.Title,
                Foreground = Brushes.White,
                FontSize = 50,
                LineHeight = 50,
                LineStackingStrategy = System.Windows.LineStackingStrategy.BlockLineHeight,
                FontFamily = new FontFamily("Segoe UI Light"),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 700,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Margin = new Thickness(50, 200, 50, 0),
            };
            infoPanel.Children.Add(title);
        }

        void AddAuthor() {
            var author = new TextBlock {
                Text = publication.Authors.DefaultIfEmpty("").First(),
                Foreground = Brushes.White,
                FontSize = 40,
                FontFamily = new FontFamily("Segoe UI Light"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(50, 0, 50, 0),
            };
            infoPanel.Children.Add(author);
        }

        void AddDescription() {
            description = new TextBlock {
                Text = publication.Description,
                Foreground = Brushes.White,
                FontSize = 26,
                FontFamily = new FontFamily("Segoe UI Light"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(50, 0, 0, 0),
                TextTrimming = TextTrimming.WordEllipsis,
                MaxHeight = 150,
                MaxWidth = 700,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            };
            infoPanel.Children.Add(description);
        }

        void AddBorrowLabel() {
            borrowLabel = new TextBlock {
                Text = "Borrow Now ›",
                Foreground = Brushes.White,
                FontSize = 40,
                FontFamily = new FontFamily("Segoe UI Light"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(50, 20, 50, 0),
            };
            borrowLabel.StylusSystemGesture += BorrowLabelGesture;
            infoPanel.Children.Add(borrowLabel);
        }

        void SetupInputPanel() {
            inputPanel = new StackPanel {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(50, 20, 0, 0),
            };

            AddUsernameTextBox();
            AddSendLabel();
        }

        void AddUsernameTextBox() {
            usernameBox = new TextBox {
                FontSize = 36,
                Text = "Username",
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Width = 400,
            };
            usernameBox.KeyUp += UsernameKeyUp;
            inputPanel.Children.Add(usernameBox);
        }

        void AddSendLabel() {
            var sendLabel = new TextBlock {
                Text = "Send ›",
                Foreground = Brushes.White,
                FontSize = 36,
                FontFamily = new FontFamily("Segoe UI Light"),
                Margin = new Thickness(20, 0, 0, 0)
            };
            sendLabel.StylusSystemGesture += SendLabelGesture;
            inputPanel.Children.Add(sendLabel);
        }

        void BorrowLabelGesture(object sender, StylusSystemGestureEventArgs args) {
            if (args.SystemGesture != SystemGesture.Tap) { return; }

            RemoveBorrowLabel();
            args.Handled = true;
        }

        void SendLabelGesture(object sender, StylusSystemGestureEventArgs args) {
            if (args.SystemGesture != SystemGesture.Tap) { return; }

            SendEMail();
            args.Handled = true;
        }

        void BackgroundGesture(object sender, StylusSystemGestureEventArgs args) {
            if (args.SystemGesture != SystemGesture.Tap) { return; }

            AnimateOut();
        }

        void UsernameKeyUp(object sender, KeyEventArgs args) {
            if (args.Key != Key.Enter && args.Key != Key.Return) { return; }

            SendEMail();
        }

        void SendEMail() {
            Emailer.Emailer.emailSend(usernameBox.Text, publication);
            AnimateOut();
            RaiseEvent(new RoutedEventArgs(InfoGrid.EmailSentEvent));
        }

        void RemoveBorrowLabel() {
            if (!infoPanel.Children.Contains(borrowLabel)) { return; }

            var outAnimation = new DoubleAnimation {
                From = 1,
                To = 0,
                Duration = new Duration(TimeSpan.FromSeconds(EASE_IN_TIME)),
            };

            outAnimation.Completed += delegate {
                infoPanel.Children.Remove(borrowLabel);
                infoPanel.Children.Remove(description);
                AddInputPanel();
            };

            description.ApplyAnimationClock(TextBlock.OpacityProperty, outAnimation.CreateClock());
            borrowLabel.ApplyAnimationClock(TextBlock.OpacityProperty, outAnimation.CreateClock());
        }

        void AddInputPanel() {
            if (infoPanel.Children.Contains(inputPanel)) { return; }
            infoPanel.Children.Add(inputPanel);

            var inAnimation = new DoubleAnimation {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromSeconds(0.1)),
            };

            inAnimation.Completed += delegate {
                usernameBox.SelectAll();
                usernameBox.Focus();

                Process.Start(@"C:\Program Files\Common Files\Microsoft Shared\ink\TabTip.exe");
            };

            inputPanel.ApplyAnimationClock(Grid.OpacityProperty, inAnimation.CreateClock());
        }

        void AnimateIn(object sender, RoutedEventArgs e) {
            var animation = new DoubleAnimation(1, new Duration(TimeSpan.FromSeconds(EASE_IN_TIME)));
            var clock = animation.CreateClock();

            ApplyAnimationClock(Grid.OpacityProperty, clock);
        }

        void AnimateOut() {
            var animation = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(EASE_IN_TIME)));
            var clock = animation.CreateClock();

            ApplyAnimationClock(Grid.OpacityProperty, clock);
            clock.Completed += (s, e2) => {
                var args = new RoutedEventArgs(DoneEvent);
                RaiseEvent(args);
            };

            HideTouchKeyboard();
        }

        const double EASE_IN_TIME = 0.1; // seconds

        static readonly RoutedEvent DoneEvent = EventManager.RegisterRoutedEvent(
            "Done", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(InfoGrid)
        );

        static readonly RoutedEvent EmailSentEvent = EventManager.RegisterRoutedEvent(
            "EmailSent", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(InfoGrid)
        );

        [DllImport("NativeWrappers.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern bool HideTouchKeyboard();
    }
}
