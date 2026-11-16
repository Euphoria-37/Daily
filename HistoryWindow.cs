using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DailyTodo
{
    public class HistoryWindow : ThemedWindow
    {
        readonly TodoStore store;
        readonly Action changed;
        internal readonly StackPanel Records = new StackPanel();
        readonly TextBlock count = new TextBlock { Foreground = Color("#67756C"), Margin = new Thickness(0, 4, 0, 16) };
        public HistoryWindow(Window owner, TodoStore store, Action changed) : base(owner, "历史记录", 510, 580)
        {
            this.store = store; this.changed = changed;
            Body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Body.RowDefinitions.Add(new RowDefinition());
            Body.Children.Add(count);
            var scroll = new ScrollViewer { Content = Records, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            Grid.SetRow(scroll, 1); Body.Children.Add(scroll);
            Render();
        }
        internal static string TimeLabel(DateTime? time) { return time.HasValue ? time.Value.ToLocalTime().ToString("yyyy/MM/dd HH:mm") : "未记录"; }
        internal void Render()
        {
            Records.Children.Clear();
            var items = store.Data.Items.Where(t => t.Archived).OrderByDescending(t => t.ArchivedAt ?? DateTime.MinValue).ToList();
            count.Text = "已归档 " + items.Count + " 条 · 仅保存在本机";
            if (items.Count == 0) Records.Children.Add(new TextBlock { Text = "还没有归档记录\n归档后的待办会保留在这里。", TextAlignment = TextAlignment.Center, Margin = new Thickness(12, 70, 12, 12), Foreground = Color("#67756C"), LineHeight = 26 });
            foreach (var item in items)
            {
                var content = new StackPanel();
                content.Children.Add(new TextBlock { Text = item.Text, TextWrapping = TextWrapping.Wrap, FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) });
                content.Children.Add(new TextBlock { Text = "创建  " + TimeLabel(item.CreatedAt) + "\n完成  " + (item.Done ? TimeLabel(item.CompletedAt) : "未完成") + "\n归档  " + TimeLabel(item.ArchivedAt),
                    Foreground = Color("#67756C"), LineHeight = 22 });
                var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
                var restore = Button("恢复到待办", "恢复：" + item.Text);
                restore.Click += delegate { store.Restore(item.Id); changed(); Render(); };
                var delete = Button("删除", "删除历史记录：" + item.Text); delete.Margin = new Thickness(8, 0, 0, 0); delete.Foreground = Color("#93665E");
                delete.Click += delegate {
                    if (DailyDialog.Confirm(this, "删除记录", "删除后无法从历史记录恢复这条待办。", "删除", "保留")) { store.Delete(item.Id); changed(); Render(); }
                };
                actions.Children.Add(restore); actions.Children.Add(delete); content.Children.Add(actions);
                Records.Children.Add(new Border { CornerRadius = new CornerRadius(12), Background = Color("#F0F2EC"), BorderBrush = Color("#E2E6DD"), BorderThickness = new Thickness(1), Padding = new Thickness(16), Margin = new Thickness(0, 0, 0, 12), Child = content });
            }
        }
    }

    public partial class MainWindow
    {
        int modalDepth;
        void ShowHistory()
        {
            if (finishActiveEdit != null) finishActiveEdit();
            ExpandDock();
            modalDepth++;
            try { new HistoryWindow(this, store, delegate { RenderTasks(); Save(); }).ShowDialog(); }
            finally { modalDepth--; }
        }
        void Inform(string title, string message)
        {
            modalDepth++;
            try { DailyDialog.Info(this, title, message); } finally { modalDepth--; }
        }
        bool Confirm(string title, string message, string accept, string cancel)
        {
            modalDepth++;
            try { return DailyDialog.Confirm(this, title, message, accept, cancel); } finally { modalDepth--; }
        }
    }
}
