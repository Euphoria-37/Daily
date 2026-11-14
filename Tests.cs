using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace DailyTodo
{
    public static class Tests
    {
        static int count;
        static void Check(bool value, string name)
        {
            if (!value) throw new Exception("FAIL: " + name);
            Console.WriteLine("PASS: " + name);
            count++;
        }
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        static extern int GetWindowLong(IntPtr handle, int index);

        static void Pump(int milliseconds)
        {
            var frame = new System.Windows.Threading.DispatcherFrame();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
            timer.Tick += delegate { timer.Stop(); frame.Continue = false; };
            timer.Start();
            System.Windows.Threading.Dispatcher.PushFrame(frame);
        }

        [STAThread]
        public static int Main()
        {
            string dir = Path.Combine(Path.GetTempPath(), "DailyTodo-test-" + Guid.NewGuid());
            try
            {
                var store = new TodoStore(Path.Combine(dir, "tasks.xml"));
                Check(!store.Add(" \r\n "), "blank task rejected");
                Check(store.Add("  中文事项 <test> & emoji 🎉  "), "unicode task accepted");
                Check(store.Data.Items[0].Text == "中文事项 <test> & emoji 🎉", "text trimmed");
                Check(!store.Add(new string('x', 201)), "length limit enforced");
                string id = store.Data.Items[0].Id;
                store.Toggle(id);
                Check(store.Data.Items[0].Done, "completion enabled");
                store.Save();
                store = new TodoStore(store.FilePath);
                Check(store.Data.Items[0].Done && store.Data.Items[0].Text.Contains("<test> &"), "persist and restore escaped unicode");
                store.Toggle(id);
                Check(!store.Data.Items[0].Done, "completion reversible");
                store.Delete(id);
                Check(store.Data.Items.Count == 0, "delete task");
                store.Save();
                Check(File.Exists(store.FilePath + ".bak"), "previous save backed up");

                int quoteRequests = 0;
                Func<System.Threading.Tasks.Task<string>> quoteFetch = () => {
                    quoteRequests++;
                    return System.Threading.Tasks.Task.FromResult("每天向前一步。");
                };
                var day = new DateTime(2026, 11, 14, 23, 59, 0);
                Check(DailyQuote.RefreshAsync(store.Data, day, quoteFetch).GetAwaiter().GetResult(), "first daily quote fetched");
                Check(!DailyQuote.RefreshAsync(store.Data, day.AddSeconds(30), quoteFetch).GetAwaiter().GetResult() && quoteRequests == 1, "same system date uses cache");
                Check(DailyQuote.RefreshAsync(store.Data, day.AddMinutes(2), quoteFetch).GetAwaiter().GetResult() && quoteRequests == 2, "midnight fetches a new quote");
                store.Save();
                var quoteRestored = new TodoStore(store.FilePath);
                Check(DailyQuote.IsCurrent(quoteRestored.Data, day.AddMinutes(2)) && quoteRestored.Data.QuoteText == "每天向前一步。", "daily quote survives restart");
                bool invalidQuote = false;
                try { DailyQuote.RefreshAsync(store.Data, day.AddDays(2), () => System.Threading.Tasks.Task.FromResult("<html>bad gateway</html>")).GetAwaiter().GetResult(); }
                catch (FormatException) { invalidQuote = true; }
                Check(invalidQuote && store.Data.QuoteText == "每天向前一步。" && store.Data.QuoteDate == "2026-11-15", "invalid response preserves cached quote");
                bool offline = false;
                try { DailyQuote.RefreshAsync(store.Data, day.AddDays(2), () => { throw new System.Net.Http.HttpRequestException(); }).GetAwaiter().GetResult(); }
                catch (System.Net.Http.HttpRequestException) { offline = true; }
                Check(offline && store.Data.QuoteDate == "2026-11-15", "network failure does not mark date refreshed");

                var app = new Application();
                var window = new MainWindow(store, false);
                window.Show();
                window.UpdateLayout();
                Check(((TextBlock)window.FindName("QuoteLabel")).Text == "每天向前一步。", "main interface displays cached daily quote");
                Check(!window.ShowInTaskbar, "window excluded from taskbar");
                Check(((Grid)window.Content).Children.Count == 9, "all eight resize edges and corners installed");
                Check(app.Windows.Count == 1, "only the visible task window remains registered");
                var input = (TextBox)window.FindName("TaskInput");
                input.Text = "界面添加测试";
                ((Button)window.FindName("AddButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(store.Data.Items.Count == 1 && input.Text == "", "add through actual button");
                var rows = (StackPanel)window.FindName("TaskRows");
                var grid = (Grid)((Border)rows.Children[0]).Child;
                ((System.Windows.Controls.Primitives.ToggleButton)grid.Children[0]).IsChecked = true;
                Check(store.Data.Items[0].Done, "circle toggles actual task");
                ((System.Windows.Controls.Primitives.ToggleButton)grid.Children[0]).IsChecked = false;
                Check(!store.Data.Items[0].Done, "circle reverses actual task");
                ((Button)grid.Children[2]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(store.Data.Items.Count == 0 && rows.Children.Count == 0, "delete through actual row button");
                input.Text = new string('长', 200);
                ((Button)window.FindName("AddButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var pin = (System.Windows.Controls.Primitives.ToggleButton)window.FindName("PinButton");
                pin.IsChecked = true;
                Check(window.Topmost && (GetWindowLong(new WindowInteropHelper(window).Handle, -20) & 8) != 0, "native topmost flag enabled");
                pin.IsChecked = false;
                Check(!window.Topmost && (GetWindowLong(new WindowInteropHelper(window).Handle, -20) & 8) == 0, "native topmost flag disabled");
                window.Width = 340;
                window.Height = 400;
                window.UpdateLayout();
                Check(input.ActualWidth > 100 && rows.ActualWidth > 200, "usable layout at minimum size");
                var longRow = (Grid)((Border)rows.Children[0]).Child;
                Check(((TextBlock)longRow.Children[1]).ActualHeight > 30, "long text wraps without horizontal overflow");
                for (int i = 0; i < 30; i++)
                {
                    input.Text = "事项 " + i;
                    ((Button)window.FindName("AddButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                window.UpdateLayout();
                var scroll = (ScrollViewer)window.FindName("TaskScroller");
                Check(scroll.ScrollableHeight > 0 && scroll.ViewportHeight > 0, "many tasks remain scrollable");
                window.DockTo(-1, new Rect(0, 0, 1920, 1080));
                window.CollapseDock();
                Check(window.DockCollapsed && window.Width == 18 && window.Left == 0, "left dock collapses to narrow strip");
                window.ExpandDock();
                Check(!window.DockCollapsed && window.Width == 340 && window.Height == 400, "hover expansion restores full size");
                window.DockTo(1, new Rect(0, 0, 1920, 1080));
                window.CollapseDock();
                Check(window.Left == 1902 && window.Height == 100, "right dock stays inside screen edge");
                window.ExpandDock();
                Check(((Grid)window.Content).Children.Count == 9, "expanded window retains resize controls");
                window.DockTo(2, new Rect(0, 0, 1920, 1080));
                window.CollapseDock();
                Check(window.Top == 0 && window.Height == 18 && window.Width == 100, "top dock collapses to horizontal strip");
                window.ExpandDock();
                Check(window.Top == -12 && window.Width == 340 && window.Height == 400, "top expansion restores full window");
                foreach (int side in new[] { -1, 1, 2 })
                {
                    window.DockTo(side, new Rect(0, 0, 1920, 1080));
                    window.CollapseDock();
                    var hover = new Point(window.Left + window.Width / 2, window.Top + window.Height / 2);
                    if (side == -1) hover.X = 0;
                    if (side == 1) hover.X = 1919;
                    if (side == 2) hover.Y = 0;
                    var tick = DateTime.UtcNow;
                    window.UpdateDockHover(hover, false, tick);
                    Check(!window.DockCollapsed, "edge hover expands dock " + side);
                    for (int n = 1; n <= 20; n++) window.UpdateDockHover(hover, false, tick.AddMilliseconds(n * 100));
                    Check(!window.DockCollapsed, "stationary pointer at strip stays expanded " + side);
                    var outside = new Point(960, 1000);
                    window.UpdateDockHover(outside, false, tick.AddSeconds(3));
                    window.UpdateDockHover(outside, false, tick.AddSeconds(3.1));
                    Check(!window.DockCollapsed, "leave delay prevents premature collapse " + side);
                    window.UpdateDockHover(outside, false, tick.AddSeconds(3.25));
                    Check(window.DockCollapsed, "pointer outside collapses after delay " + side);
                    window.UpdateDockHover(hover, false, tick.AddSeconds(4));
                    Check(!window.DockCollapsed, "returning pointer expands again " + side);
                    window.UpdateDockHover(outside, true, tick.AddSeconds(5));
                    window.UpdateDockHover(outside, true, tick.AddSeconds(6));
                    Check(!window.DockCollapsed, "mouse drag does not collapse panel " + side);
                }
                var originalCursor = System.Windows.Forms.Cursor.Position;
                try
                {
                    var screen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(window).Handle).WorkingArea;
                    var origin = window.PointFromScreen(new Point(screen.Left, screen.Top));
                    var end = window.PointFromScreen(new Point(screen.Right, screen.Bottom));
                    var area = new Rect(origin.X + window.Left, origin.Y + window.Top, end.X - origin.X, end.Y - origin.Y);
                    foreach (int side in new[] { -1, 1, 2 })
                    {
                        System.Windows.Forms.Cursor.Position = new System.Drawing.Point(screen.Left + screen.Width / 2, screen.Bottom - 30);
                        window.DockTo(side, area);
                        window.CollapseDock();
                        window.UpdateLayout();
                        var target = window.PointToScreen(new Point(side == -1 ? 1 : side == 1 ? window.Width - 1 : window.Width / 2,
                            side == 2 ? 1 : window.Height / 2));
                        System.Windows.Forms.Cursor.Position = new System.Drawing.Point((int)target.X, (int)target.Y);
                        Pump(150);
                        Check(!window.DockCollapsed, "real cursor and timer expand edge " + side);
                        bool collapsedWhileHovering = false;
                        SizeChangedEventHandler observe = delegate { if (window.DockCollapsed) collapsedWhileHovering = true; };
                        window.SizeChanged += observe;
                        Pump(850);
                        window.SizeChanged -= observe;
                        Check(!window.DockCollapsed && !collapsedWhileHovering, "real stationary hover has no collapse transitions " + side);
                        System.Windows.Forms.Cursor.Position = new System.Drawing.Point(screen.Left + screen.Width / 2, screen.Bottom - 30);
                        Pump(400);
                        Check(window.DockCollapsed, "real cursor leave collapses edge " + side);
                        window.ExpandDock();
                    }
                }
                finally { System.Windows.Forms.Cursor.Position = originalCursor; }
                window.Close();
                Check(app.Windows.Count == 0, "closing task window leaves no hidden windows");
                var restored = new TodoStore(store.FilePath);
                Check(restored.Data.Items.Count == 31, "window changes survive restart");
                Check(!restored.Data.Pinned && restored.Data.Width == 340, "pin and window size restored");

                File.WriteAllText(store.FilePath, "broken xml");
                bool rejected = false;
                try { new TodoStore(store.FilePath); } catch (InvalidDataException) { rejected = true; }
                Check(rejected && File.ReadAllText(store.FilePath) == "broken xml", "corrupt data preserved and reported");
                app.Shutdown();
                Console.WriteLine("All " + count + " checks passed.");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }
    }
}
