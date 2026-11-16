using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DailyTodo
{
    public partial class MainWindow
    {
        int dockSide;
        internal bool DockCollapsed { get; private set; }
        Rect expandedBounds;
        Rect dockArea;
        Grid fullContent;
        Border dockStrip;
        DispatcherTimer collapseTimer;
        Rect stripBounds;
        DateTime? pointerOutsideSince;
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct CursorPoint { public int X; public int Y; }
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool GetCursorPos(out CursorPoint point);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr handle);

        void InitializeDocking()
        {
            fullContent = (Grid)Content;
            collapseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            collapseTimer.Tick += delegate {
                var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                CursorPoint cursor;
                if (!IsWindowVisible(handle) || !GetCursorPos(out cursor)) { pointerOutsideSince = null; return; }
                var local = PointFromScreen(new Point(cursor.X, cursor.Y));
                UpdateDockHover(new Point(local.X + Left, local.Y + Top), Mouse.LeftButton == MouseButtonState.Pressed, DateTime.UtcNow);
            };
            Closed += delegate { collapseTimer.Stop(); };
            dockStrip = new Border {
                Background = (Brush)new BrushConverter().ConvertFromString("#FCFBF8"),
                BorderBrush = (Brush)new BrushConverter().ConvertFromString("#C8D5C8"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
                Cursor = Cursors.Hand, ToolTip = "移入展开每日待办",
                Child = new Border { Width = 3, Height = 36, CornerRadius = new CornerRadius(1.5),
                    Background = (Brush)new BrushConverter().ConvertFromString("#65876A"),
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };
        }

        internal void UpdateDockHover(Point pointer, bool dragging, DateTime now)
        {
            if (checkingUpdate || modalDepth > 0) { pointerOutsideSince = null; return; }
            if (dockSide == 0) return;
            // Rounded corners and transparent shadow margins can produce MouseLeave
            // without the pointer actually leaving the panel or its original strip.
            bool inside = new Rect(Left, Top, Width, Height).Contains(pointer)
                || (!DockCollapsed && stripBounds.Contains(pointer));
            if (inside)
            {
                pointerOutsideSince = null;
                ExpandDock();
            }
            else if (dragging) pointerOutsideSince = null;
            else if (!pointerOutsideSince.HasValue) pointerOutsideSince = now;
            else if ((now - pointerOutsideSince.Value).TotalMilliseconds >= 200) CollapseDock();
        }

        void Undock()
        {
            ExpandDock();
            dockSide = 0;
            pointerOutsideSince = null;
            collapseTimer.Stop();
        }

        void TryDock()
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var screen = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
            Point a = PointFromScreen(new Point(screen.Left, screen.Top));
            Point b = PointFromScreen(new Point(screen.Right, screen.Bottom));
            var area = new Rect(a.X + Left, a.Y + Top, b.X - a.X, b.Y - a.Y);
            // The visible rounded panel has a 12 DIP shadow margin.
            if (Top <= area.Top + 24) DockTo(2, area);
            else if (Left <= area.Left + 24) DockTo(-1, area);
            else if (Left + Width >= area.Right - 24) DockTo(1, area);
        }

        internal void DockTo(int side, Rect area)
        {
            ExpandDock();
            dockSide = side;
            dockArea = area;
            stripBounds = Rect.Empty;
            pointerOutsideSince = null;
            if (side == 2) Left = Math.Max(area.Left, Math.Min(Left, area.Right - Width));
            else Left = side < 0 ? area.Left - 12 : area.Right - Width + 12;
            Top = Math.Max(area.Top, Math.Min(Top, area.Bottom - Height));
            if (side == 2) Top = area.Top - 12;
            expandedBounds = new Rect(Left, Top, Width, Height);
            collapseTimer.Start();
        }

        internal void CollapseDock()
        {
            if (dockSide == 0 || DockCollapsed) return;
            expandedBounds = new Rect(Left, Top, Width, Height);
            DockCollapsed = true;
            Content = null;
            Content = dockStrip;
            ResizeMode = ResizeMode.NoResize;
            MinWidth = 0;
            MinHeight = 0;
            var marker = (Border)dockStrip.Child;
            marker.Width = dockSide == 2 ? 36 : 3;
            marker.Height = dockSide == 2 ? 3 : 36;
            Width = dockSide == 2 ? 100 : 18;
            Height = dockSide == 2 ? 18 : 100;
            Left = dockSide == 2 ? Math.Max(dockArea.Left, Math.Min(expandedBounds.Left + (expandedBounds.Width - Width) / 2, dockArea.Right - Width)) : dockSide < 0 ? dockArea.Left : dockArea.Right - Width;
            Top = dockSide == 2 ? dockArea.Top : Math.Max(dockArea.Top, Math.Min(expandedBounds.Top + 36, dockArea.Bottom - Height));
            stripBounds = new Rect(Left, Top, Width, Height);
        }

        internal void ExpandDock()
        {
            if (!DockCollapsed) return;
            DockCollapsed = false;
            Content = null;
            Content = fullContent;
            Width = expandedBounds.Width;
            Height = expandedBounds.Height;
            Left = expandedBounds.Left;
            Top = expandedBounds.Top;
            MinWidth = 340;
            MinHeight = 400;
            ResizeMode = ResizeMode.CanResizeWithGrip;
        }
    }
}


