using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;

namespace DailyTodo
{
    public partial class MainWindow : Window
    {
        readonly TodoStore store;
        readonly TextBox input;
        readonly StackPanel rows;
        readonly ToggleButton pin;
        readonly DispatcherTimer dateTimer;
        bool saveFailed;
        System.Windows.Forms.NotifyIcon tray;
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr h, int command);

        void AddResizeEdges()
        {
            var body = (UIElement)Content;
            Content = null;
            var outer = new Grid();
            outer.Children.Add(body);
            Content = outer;
            for (int direction = 1; direction <= 8; direction++)
            {
                int d = direction;
                bool left = d == 1 || d == 4 || d == 7;
                bool right = d == 2 || d == 5 || d == 8;
                bool top = d == 3 || d == 4 || d == 5;
                bool bottom = d == 6 || d == 7 || d == 8;
                var edge = new Border { Background = Brushes.Transparent, Margin = new Thickness(10),
                    HorizontalAlignment = left ? HorizontalAlignment.Left : right ? HorizontalAlignment.Right : HorizontalAlignment.Stretch,
                    VerticalAlignment = top ? VerticalAlignment.Top : bottom ? VerticalAlignment.Bottom : VerticalAlignment.Stretch,
                    Cursor = d <= 2 ? Cursors.SizeWE : d == 3 || d == 6 ? Cursors.SizeNS : d == 4 || d == 8 ? Cursors.SizeNWSE : Cursors.SizeNESW };
                if (left || right) edge.Width = 8;
                if (top || bottom) edge.Height = 8;
                edge.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) {
                    ReleaseCapture();
                    SendMessage(new System.Windows.Interop.WindowInteropHelper(this).Handle, 0x112, new IntPtr(0xF000 + d), IntPtr.Zero);
                    e.Handled = true;
                };
                outer.Children.Add(edge);
            }
        }

        T Part<T>(string name) where T : FrameworkElement { return (T)FindName(name); }

        public MainWindow(TodoStore store, bool onlineQuotes = true)
        {
            this.store = store;
            Window view;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DailyTodo.MainWindow.xaml"))
                view = (Window)XamlReader.Load(stream);
            Resources = view.Resources;
            var content = view.Content;
            view.Content = null;
            Content = content;
            NameScope.SetNameScope(this, NameScope.GetNameScope(view));
            Title = view.Title;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            ShowInTaskbar = false;
            AddResizeEdges();
            FontFamily = view.FontFamily;
            FontSize = view.FontSize;
            Foreground = view.Foreground;
            MinWidth = 340;
            MinHeight = 400;
            Width = Finite(store.Data.Width) ? Math.Max(MinWidth, Math.Min(store.Data.Width, SystemParameters.WorkArea.Width)) : 390;
            Height = Finite(store.Data.Height) ? Math.Max(MinHeight, Math.Min(store.Data.Height, SystemParameters.WorkArea.Height)) : 540;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            if (Finite(store.Data.Left) && Finite(store.Data.Top))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = Math.Max(SystemParameters.VirtualScreenLeft, Math.Min(store.Data.Left, SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - Width));
                Top = Math.Max(SystemParameters.VirtualScreenTop, Math.Min(store.Data.Top, SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - Height));
            }

            input = Part<TextBox>("TaskInput");
            rows = Part<StackPanel>("TaskRows");
            pin = Part<ToggleButton>("PinButton");
            pin.IsChecked = store.Data.Pinned;
            Topmost = store.Data.Pinned;
            UpdatePin();
            pin.Checked += PinChanged;
            pin.Unchecked += PinChanged;
            Part<Button>("CloseButton").Click += delegate { Close(); };
            Part<Button>("MinimizeButton").Click += delegate { ShowWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle, 0); };
            Part<Grid>("TitleBar").MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                DependencyObject target = e.OriginalSource as DependencyObject;
                while (target != null)
                {
                    if (target is ButtonBase) return;
                    target = VisualTreeHelper.GetParent(target);
                }
                if (e.ButtonState == MouseButtonState.Pressed) { Undock(); DragMove(); TryDock(); }
            };
            Part<Button>("AddButton").Click += delegate { AddTask(); };
            input.TextChanged += delegate { Part<TextBlock>("InputHint").Visibility = input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; };
            input.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { AddTask(); e.Handled = true; } };
            PreviewKeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control) { input.Focus(); e.Handled = true; } };
            Closing += OnClosing;
            Loaded += delegate { input.Focus(); };
            dateTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            dateTimer.Tick += async delegate { UpdateDate(); await RefreshQuoteAsync(); };
            dateTimer.Start();
            Closed += delegate { dateTimer.Stop(); };
            tray = new System.Windows.Forms.NotifyIcon { Text = "Daily", Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location), Visible = true };
            InitializeDocking();
            Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(tray.Icon.Handle, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            Action restore = delegate { ExpandDock(); ShowWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle, 9); Activate(); };
            tray.DoubleClick += delegate { restore(); };
            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("显示待办", null, delegate { restore(); });
            menu.Items.Add("退出", null, delegate { Close(); });
            tray.ContextMenuStrip = menu;
            Closed += delegate { tray.Visible = false; tray.Icon.Dispose(); tray.Dispose(); menu.Dispose(); };
            UpdateDate();
            RenderTasks();
            InitializeQuote(onlineQuotes);
            // XamlReader creates a temporary Window registered with Application.
            // Release it after transferring its content so closing this window exits.
            view.Close();
        }

        static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }

        void UpdateDate()
        {
            Part<TextBlock>("DateLabel").Text = DateTime.Now.ToString("M月d日 · dddd", CultureInfo.GetCultureInfo("zh-CN"));
        }

        void UpdatePin() { pin.ToolTip = Topmost ? "已置顶 · 点击取消" : "点击置顶到其他窗口之上"; }
        void PinChanged(object sender, RoutedEventArgs e)
        {
            Topmost = pin.IsChecked == true;
            store.Data.Pinned = Topmost;
            UpdatePin();
            Save();
        }

        void AddTask()
        {
            if (!store.Add(input.Text)) { input.Focus(); return; }
            input.Clear();
            RenderTasks();
            Save();
            Part<ScrollViewer>("TaskScroller").ScrollToEnd();
            input.Focus();
        }

        void RenderTasks()
        {
            rows.Children.Clear();
            foreach (var item in store.Data.Items)
            {
                var row = new Grid { Margin = new Thickness(0, 5, 0, 5) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
                var circle = new ToggleButton { Style = (Style)Resources["CircleStyle"], IsChecked = item.Done, VerticalAlignment = VerticalAlignment.Top };
                AutomationProperties.SetName(circle, "完成状态：" + item.Text);
                var label = new TextBlock { Text = item.Text, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(7, 8, 4, 8), FontSize = 13 };
                Grid.SetColumn(label, 1);
                Action update = delegate
                {
                    label.TextDecorations = item.Done ? TextDecorations.Strikethrough : null;
                    label.Foreground = (Brush)new BrushConverter().ConvertFromString(item.Done ? "#778277" : "#293831");
                    circle.ToolTip = item.Done ? "标记为未完成" : "标记为已完成";
                };
                update();
                RoutedEventHandler toggle = delegate
                {
                    store.Toggle(item.Id);
                    update();
                    UpdateProgress();
                    Save();
                };
                circle.Checked += toggle;
                circle.Unchecked += toggle;
                var delete = new Button { Content = "×", FontSize = 17, Padding = new Thickness(0), Width = 26, Height = 30, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 3, 0, 0), ToolTip = "删除事项" };
                AutomationProperties.SetName(delete, "删除：" + item.Text);
                Grid.SetColumn(delete, 2);
                delete.Click += delegate { store.Delete(item.Id); RenderTasks(); Save(); input.Focus(); };
                row.Children.Add(circle);
                row.Children.Add(label);
                row.Children.Add(delete);
                rows.Children.Add(new Border { Child = row, BorderBrush = (Brush)new BrushConverter().ConvertFromString("#ECEEE7"), BorderThickness = new Thickness(0, 0, 0, 1) });
            }
            Part<StackPanel>("EmptyState").Visibility = store.Data.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateProgress();
        }

        void UpdateProgress()
        {
            int total = store.Data.Items.Count;
            int done = store.Data.Items.Count(t => t.Done);
            Part<TextBlock>("ProgressLabel").Text = total == 0 ? "今日，从容开始" : done == total ? "全部完成，做得很好！" : "已完成 " + done + " / " + total;
            Part<ProgressBar>("Progress").Value = total == 0 ? 0 : (double)done / total;
        }

        bool Save()
        {
            try
            {
                store.Save();
                saveFailed = false;
                Part<TextBlock>("SaveLabel").Text = "本机自动保存";
                Part<TextBlock>("SaveLabel").ToolTip = store.FilePath;
                return true;
            }
            catch (Exception ex)
            {
                if (!(ex is System.IO.IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException)) throw;
                saveFailed = true;
                Part<TextBlock>("SaveLabel").Text = "保存失败";
                Part<TextBlock>("SaveLabel").ToolTip = ex.Message;
                return false;
            }
        }

        void OnClosing(object sender, CancelEventArgs e)
        {
            ExpandDock();
            store.Data.Left = Left;
            store.Data.Top = Top;
            store.Data.Width = Width;
            store.Data.Height = Height;
            Save();
            if (saveFailed && MessageBox.Show(this, "当前更改未能保存。仍要退出吗？\n数据位置：" + store.FilePath,
                "每日待办", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) e.Cancel = true;
        }
    }
}


