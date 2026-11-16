using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace DailyTodo
{
    public class ThemedWindow : Window
    {
        public readonly Grid Body = new Grid();
        internal readonly Border Shell;
        internal readonly Button WindowClose;
        public static Brush Color(string value) { return (Brush)new BrushConverter().ConvertFromString(value); }

        public ThemedWindow(Window owner, string title, double width, double height)
        {
            Title = "Daily · " + title;
            if (owner != null) { Owner = owner; Resources = owner.Resources; Icon = owner.Icon; }
            else Resources.Add(typeof(Button), XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'>
                <Setter Property='Foreground' Value='#526159'/><Setter Property='Background' Value='#EBF0E7'/><Setter Property='Padding' Value='16,8'/><Setter Property='Cursor' Value='Hand'/>
                <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border CornerRadius='8' Background='{TemplateBinding Background}' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border></ControlTemplate></Setter.Value></Setter></Style>"));
            Width = width; Height = height; MinWidth = 340; MinHeight = 200;
            MaxHeight = Math.Max(200, SystemParameters.WorkArea.Height);
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            ShowInTaskbar = false; ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
            Topmost = owner != null && owner.Topmost;
            FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI"); FontSize = 13; Foreground = Color("#293831");
            var layout = new Grid { Margin = new Thickness(22, 12, 22, 22) };
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var header = new Grid { Background = Brushes.Transparent };
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            header.Children.Add(new TextBlock { Text = title, FontSize = 17, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            WindowClose = Button("×", "关闭窗口"); WindowClose.Width = 32; WindowClose.Height = 32; WindowClose.Padding = new Thickness(0); WindowClose.FontSize = 21;
            WindowClose.Click += delegate { Close(); };
            Grid.SetColumn(WindowClose, 1); header.Children.Add(WindowClose);
            header.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) {
                DependencyObject node = e.OriginalSource as DependencyObject;
                while (node != null) { if (node is System.Windows.Controls.Primitives.ButtonBase) return; node = VisualTreeHelper.GetParent(node); }
                if (e.ButtonState == MouseButtonState.Pressed) DragMove();
            };
            layout.Children.Add(header); Grid.SetRow(Body, 1); layout.Children.Add(Body);
            Shell = new Border { CornerRadius = new CornerRadius(20), Margin = new Thickness(12), Background = Color("#FCFBF8"),
                BorderBrush = Color("#DDE1D9"), BorderThickness = new Thickness(1), Child = layout,
                Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 3, Opacity = 0.16, Color = Colors.Black } };
            Content = Shell;
            PreviewKeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
            Loaded += delegate {
                var screen = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle).WorkingArea;
                var a = PointFromScreen(new Point(screen.Left, screen.Top));
                var b = PointFromScreen(new Point(screen.Right, screen.Bottom));
                double x = a.X + Left, y = a.Y + Top;
                Left = Math.Max(x, Math.Min(Left, x + b.X - a.X - ActualWidth));
                Top = Math.Max(y, Math.Min(Top, y + b.Y - a.Y - ActualHeight));
            };
        }

        public static Button Button(string text, string name)
        {
            var button = new Button { Content = text, Padding = new Thickness(14, 8, 14, 8), Background = Color("#EBF0E7"), MinHeight = 32 };
            System.Windows.Automation.AutomationProperties.SetName(button, name);
            return button;
        }
    }

    public class DailyDialog : ThemedWindow
    {
        internal readonly Button AcceptButton;
        internal readonly Button CancelButton;
        internal readonly Button FeedbackButton;
        internal const string FeedbackUrl = "https://github.com/march7th-26/Daily/issues";
        public DailyDialog(Window owner, string title, string message, string accept, string cancel, bool feedback = false)
            : base(owner, title, 440, 300)
        {
            if (feedback) { Width = 400; Height = 280; }
            Body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Body.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 12, 0, 20),
                Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, LineHeight = 23, Foreground = Color("#526159") } });
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            if (cancel != null) { CancelButton = Button(cancel, cancel); CancelButton.Margin = new Thickness(0, 0, 10, 0); CancelButton.IsCancel = true; CancelButton.Click += delegate { DialogResult = false; }; buttons.Children.Add(CancelButton); }
            AcceptButton = Button(accept, accept); AcceptButton.Background = Color("#3F7059"); AcceptButton.Foreground = Brushes.White;
            AcceptButton.IsDefault = true; AcceptButton.Click += delegate { DialogResult = true; };
            var footer = new Grid();
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.ColumnDefinitions.Add(new ColumnDefinition());
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            buttons.Children.Add(AcceptButton); Grid.SetColumn(buttons, 2); footer.Children.Add(buttons);
            Grid.SetRow(footer, 1); Body.Children.Add(footer);
            if (feedback)
            {
                var content = new StackPanel { Orientation = Orientation.Horizontal };
                content.Children.Add(new System.Windows.Shapes.Path { Width = 17, Height = 17, Stretch = Stretch.Uniform,
                    Stroke = Color("#62746A"), StrokeThickness = 1.7, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                    Data = Geometry.Parse("M 9,19 C 4,20 4,16 2,16 M 15,22 L 15,18 C 15,17 14.7,16.3 14,16 C 19,15.5 21,13 21,9 C 21,7 20,6 19,5 C 19.4,4 19.4,2.5 19,2 C 17,2 15,3 14,4 C 12.5,3.6 11.5,3.6 10,4 C 9,3 7,2 5,2 C 4.6,2.5 4.6,4 5,5 C 4,6 3,7 3,9 C 3,13 5,15.5 10,16 C 9.3,16.3 9,17 9,18 L 9,22") });
                var text = new TextBlock { Text = "用户反馈", Margin = new Thickness(7, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                content.Children.Add(text);
                FeedbackButton = Button("", "用户反馈 · GitHub");
                FeedbackButton.Content = content; FeedbackButton.Background = Brushes.Transparent;
                FeedbackButton.Foreground = Color("#62746A"); FeedbackButton.Padding = new Thickness(8, 4, 8, 4);
                FeedbackButton.HorizontalAlignment = HorizontalAlignment.Left;
                FeedbackButton.VerticalAlignment = VerticalAlignment.Center; FeedbackButton.Margin = new Thickness(-8, 0, 0, 0);
                FeedbackButton.FontSize = 12;
                FeedbackButton.ToolTip = FeedbackUrl;
                FeedbackButton.Click += delegate {
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(FeedbackUrl) { UseShellExecute = true }); }
                    catch (Exception ex) {
                        if (!(ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException || ex is System.IO.FileNotFoundException)) throw;
                        text.Text = "无法打开，请重试";
                    }
                };
                footer.Children.Add(FeedbackButton);
            }
        }
        public static void Info(Window owner, string title, string message, bool feedback = false) { new DailyDialog(owner, title, message, "知道了", null, feedback).ShowDialog(); }
        public static bool Confirm(Window owner, string title, string message, string accept, string cancel, bool feedback = false)
        { return new DailyDialog(owner, title, message, accept, cancel, feedback).ShowDialog() == true; }
    }
}
