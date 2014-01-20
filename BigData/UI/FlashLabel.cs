using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace BigData.UI {

    /// <summary>
    /// A label that displays a message at the top of the screen for a
    /// short period of time. Use this to notify users of events like the
    /// display being reloaded or an email being sent.
    /// </summary>
    class FlashLabel : Label {

        /// <summary>
        /// Create and initialize a new FlashLabel
        /// </summary>
        public FlashLabel() {
            FontFamily = new FontFamily("Segoe UI Light");
            FontSize = 30;
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            VerticalAlignment = System.Windows.VerticalAlignment.Top;
            VerticalContentAlignment = System.Windows.VerticalAlignment.Center;
            HorizontalContentAlignment = System.Windows.HorizontalAlignment.Center;
            Height = 60;
            RenderTransform = new TranslateTransform(0, -Height);

            Loaded += AnimateIn;
        }

        /// <summary>
        /// The duration to show this flash message for
        /// </summary>
        public TimeSpan FlashDuration { get; set; }

        /// <summary>
        /// Raised after the flash message has animated off the screen
        /// </summary>
        public event RoutedEventHandler Done {
            add { AddHandler(DoneEvent, value); }
            remove { RemoveHandler(DoneEvent, value); }
        }

        void AnimateIn(object sender, EventArgs args) {
            var animation = new DoubleAnimation {
                From = -Height,
                To = 0,
                Duration = AnimationDuration,
                EasingFunction = AnimationEase,
            };

            animation.Completed += BeginTimer;

            RenderTransform.ApplyAnimationClock(TranslateTransform.YProperty,
                animation.CreateClock());
        }

        void BeginTimer(object sender, EventArgs args) {
            var timer = new DispatcherTimer {
                Interval = FlashDuration
            };
            timer.Tick += AnimateOut;
            timer.Start();
        }

        void AnimateOut(object sender, EventArgs args) {
            var animation = new DoubleAnimation {
                From = 0,
                To = -Height,
                Duration = AnimationDuration,
                EasingFunction = AnimationEase,
            };

            animation.Completed += delegate {
                RaiseEvent(new RoutedEventArgs(DoneEvent));
            };

            RenderTransform.ApplyAnimationClock(
                TranslateTransform.YProperty,
                animation.CreateClock());
        }

        static Duration AnimationDuration = TimeSpan.FromSeconds(0.25);
        static IEasingFunction AnimationEase = new CubicEase { EasingMode = EasingMode.EaseInOut };
        static readonly RoutedEvent DoneEvent = EventManager.RegisterRoutedEvent(
            "Done",
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(FlashLabel));
    }
}
