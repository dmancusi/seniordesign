using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace BigData.UI {

    /// <summary>
    /// The main window of the program.
    /// 
    /// Like most Windows apps, the main window "owns" all of the application
    /// components. This way, when the main window is closed, the application
    /// terminates.
    /// </summary>
    class MainWindow : Window {

        /// <summary>
        /// Create and initialize a new MainWindow
        /// </summary>
        public MainWindow() {
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            Visibility = Visibility.Visible;
            Title = "Digital Publication Display";

            Loaded += delegate { UpdateDisplay(); };
            Loaded += StartServer;
            Closed += StopServer;

            // set up a 1 by 3 grid to hold PublicationCanvas objects
            grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.RowDefinitions.Add(new RowDefinition());
            grid.RowDefinitions.Add(new RowDefinition());
            grid.RowDefinitions.Add(new RowDefinition());
            Content = grid;

            publicationCache = new OCLC.Database();
            server = new Management_Interface.ManagementServer();

            DisableEdgeGestures();
        }

        OCLC.Database publicationCache;
        Management_Interface.ManagementServer server;
        Grid grid;

        bool HasCredentials() {
            if (Properties.Settings.Default.RSSUri == null || Properties.Settings.Default.RSSUri.Length == 0) {
                FlashMessage("No book list - Enter one in the management interface", Brushes.LightSalmon);
                return false;
            }

            if (Properties.Settings.Default.WSKey == null || Properties.Settings.Default.WSKey.Length == 0) {
                FlashMessage("No WSKey - Enter one in the management interface", Brushes.LightSalmon);
                return false;
            }

            if (Properties.Settings.Default.MailPassword == null || Properties.Settings.Default.MailPassword.Length == 0) {
                FlashMessage("No Mail Password - Enter one in the management interface", Brushes.LightSalmon);
                return false;
            }

            return true;
        }

        async void UpdateDisplay() {
            if (!HasCredentials()) { return; }

            FlashMessage("Loading...", Brushes.LightYellow);

            var allPublications = await publicationCache.GetPublications();

            // divide publications into three groups
            var groups = allPublications
                .Select((pub, i) => new { pub, i })
                .GroupBy(group => group.i % 3, group => group.pub);

            // for each group, construct a new PublicationCanvas
            var views = groups
                .Select(pubs => new PublicationCanvas(
                    pubs.ToArray(),
                    grid.RowDefinitions.First().ActualHeight))
                .ToArray();

            // remove existing PublicationCanvases (if any)
            var canvases = grid.Children.OfType<PublicationCanvas>().ToArray();
            foreach (var canvas in canvases) {
                grid.Children.Remove(canvas);
            }

            // add new PublicationCanvases
            for (int i = 0; i < views.Length; i++) {
                Grid.SetRow(views[i], i);
                grid.Children.Add(views[i]);
                views[i].PublicationSelected += ShowPublicationInfo;
            }

            FlashMessage("Loaded", Brushes.LightGreen);
        }

        void StartServer(object sender, EventArgs args) {
            server.StartServer();
            server.UpdateDatabaseAction = async delegate {
                if (!HasCredentials()) { return; }
                await publicationCache.UpdateDatabase();
                UpdateDisplay();
            };
        }

        void StopServer(object sender, EventArgs args) {
            server.Dispose();
        }

        void FlashMessage(string text, Brush background) {
            var label = new FlashLabel {
                Content = text,
                Background = background,
                FlashDuration = TimeSpan.FromSeconds(5),
            };
            Grid.SetRow(label, 0);
            Grid.SetColumn(label, 0);
            Grid.SetZIndex(label, int.MaxValue);
            grid.Children.Add(label);

            label.Done += delegate { grid.Children.Remove(label); };
        }

        void ShowPublicationInfo(object sender, PublicationSelectedArgs args) {
            // if we're already displaying a publication info grid, remove it
            var children = grid.Children.OfType<InfoGrid>().ToArray();
            foreach (var child in children) {
                grid.Children.Remove(child);
            }

            var view = new InfoGrid(args.Publication);
            Grid.SetRow(view, 0);
            Grid.SetRowSpan(view, 3);
            Grid.SetZIndex(view, 10);
            grid.Children.Add(view);

            view.EmailSent += delegate {
                FlashMessage("Email Sent!", Brushes.LightGreen);
            };

            view.Done += delegate { grid.Children.Remove(view); };
        }

        void DisableEdgeGestures() {
            var ih = new WindowInteropHelper(this);
            var hwnd = ih.EnsureHandle();

            var success = SetTouchDisableProperty(hwnd, true);
            if (!success) {
                Console.Error.WriteLine("Failed to set touch disable property");
            }
        }

        [DllImport("NativeWrappers.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern bool SetTouchDisableProperty(IntPtr hwnd, bool fDisableTouch);
    }
}
