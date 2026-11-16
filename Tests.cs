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
        static DragEventArgs TestDrop(DependencyObject target)
        {
            // WPF constructs these args internally during OLE drag/drop.
            var args = (DragEventArgs)Activator.CreateInstance(typeof(DragEventArgs),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null,
                new object[] { new DataObject("Daily.TaskMove", "test"), DragDropKeyStates.None, DragDropEffects.Move, target, new Point(1, 1) }, null);
            args.RoutedEvent = DragDrop.DropEvent;
            return args;
        }
        static void TestOrdering(string root)
        {
            var store = new TodoStore(Path.Combine(root, "order.xml"));
            store.Add("A"); store.Add("B"); store.Add("C"); store.Add("D");
            var a = store.Data.Items[0]; var b = store.Data.Items[1]; var c = store.Data.Items[2]; var d = store.Data.Items[3];
            store.Toggle(c.Id); store.Archive(b.Id);
            var created = c.CreatedAt; var completed = c.CompletedAt;
            Func<string, string, bool, bool> reorder = store.Move;
            Check(reorder(c.Id, a.Id, false) && store.Data.Items[0] == c, "move older or completed task to first position");
            Check(reorder(c.Id, d.Id, true) && store.Data.Items[3] == c, "move task down after last visible item");
            Check(!reorder(c.Id, c.Id, false) && !reorder(b.Id, a.Id, false) && !reorder(a.Id, b.Id, false)
                && !reorder("missing", a.Id, false), "self archived and missing reorder targets are rejected");
            Check(!reorder(c.Id, d.Id, true), "already adjacent reorder is a no-op");
            store.Save();
            var restored = new TodoStore(store.FilePath);
            Check(string.Join(",", restored.Data.Items.ConvertAll(t => t.Text)) == "A,B,D,C", "manual order survives restart including archived records");
            Check(c.CreatedAt == created && c.CompletedAt == completed && c.Done && b.Archived && store.Data.Items.Count == 4,
                "reorder preserves all task content timestamps and states");
        }
        static void Check(bool value, string name)
        {
            if (!value) throw new Exception("FAIL: " + name);
            Console.WriteLine("PASS: " + name);
            count++;
        }
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        static extern int GetWindowLong(IntPtr handle, int index);
        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);

        static void Pump(int milliseconds)
        {
            var frame = new System.Windows.Threading.DispatcherFrame();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
            timer.Tick += delegate { timer.Stop(); frame.Continue = false; };
            timer.Start();
            System.Windows.Threading.Dispatcher.PushFrame(frame);
        }

        static void TestUpdates(string root)
        {
            var notes = new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<ReleaseInfo>(
                "{\"tag_name\":\"v1.4.2\",\"body\":\"- 新增功能\\n- 修复问题 <示例>\"}");
            Check(UpdateService.ReleaseNotes(notes).Contains("v1.4.2\n- 新增功能\n- 修复问题 <示例>"),
                "GitHub release notes deserialize and retain multiline plain text");
            notes.body = " ";
            Check(UpdateService.ReleaseNotes(notes).Contains("暂未提供"), "missing release notes have explicit fallback");
            Check(UpdateService.ParseVersion("v1.10.0") > UpdateService.ParseVersion("v1.2.0"), "update versions compare numerically");
            var release = new ReleaseInfo { tag_name = "v" + BuildVersion.Value, assets = new[] {
                new ReleaseAsset { name = "Daily.exe", state = "uploaded", size = 2048,
                    browser_download_url = "https://github.com/march7th-26/Daily/releases/download/v" + BuildVersion.Value + "/Daily.exe",
                    digest = "sha256:" + new string('0', 64) }
            }};
            Check(UpdateService.ValidateRelease(release) == release.assets[0], "only named official release asset accepted");
            release.assets[0].browser_download_url = "https://example.com/Daily.exe";
            bool rejected = false;
            try { UpdateService.ValidateRelease(release); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "foreign update URL rejected");
            release.assets[0].browser_download_url = "https://github.com/march7th-26/Daily/releases/download/v" + BuildVersion.Value + "/Daily.exe";
            release.assets[0].digest = null;
            rejected = false;
            try { UpdateService.ValidateRelease(release); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "missing update checksum rejected");
            release.assets[0].digest = "sha256:" + new string('0', 64);
            release.prerelease = true;
            rejected = false;
            try { UpdateService.ValidateRelease(release); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "prerelease excluded from updates");
            release.prerelease = false;
            release.assets[0].size = UpdateService.MaxDownloadSize + 1;
            rejected = false;
            try { UpdateService.ValidateRelease(release); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "oversize update rejected");

            string dir = Path.Combine(root, "update-fixture");
            Directory.CreateDirectory(dir);
            string candidate = Path.Combine(dir, "candidate.exe");
            string target = Path.Combine(dir, "Daily.exe");
            string production = Path.Combine(Path.GetDirectoryName(typeof(Tests).Assembly.Location), "DailyTodo.exe");
            File.Copy(production, candidate);
            File.Copy(production, target);
            File.AppendAllText(target, "previous-build-marker");
            byte[] original = File.ReadAllBytes(target);
            var data = new TodoStore(Path.Combine(dir, "tasks.xml"));
            data.Add("保留历史待办");
            data.Toggle(data.Data.Items[0].Id);
            data.Save();
            string history = File.ReadAllText(data.FilePath);
            var asset = release.assets[0];
            asset.size = new FileInfo(candidate).Length;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            using (var stream = File.OpenRead(candidate))
                asset.digest = "sha256:" + BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            var version = UpdateService.ParseVersion(BuildVersion.Value);
            UpdateService.VerifyFile(candidate, asset, version);
            Check(true, "download verified against hash and executable version");
            rejected = false;
            try { UpdateService.VerifyFile(candidate, asset, new Version(99, 0, 0, 0)); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "incorrect executable version rejected");
            int launches = 0;
            UpdateInstaller.ReplaceAndRestart(target, candidate, asset, version, path => { if (path == target) launches++; });
            Check(launches == 1 && File.ReadAllBytes(target).Length == asset.size && File.ReadAllBytes(target + ".previous").Length == original.Length,
                "update atomically replaces executable and retains previous build");
            Check(File.ReadAllText(data.FilePath) == history && new TodoStore(data.FilePath).Data.Items[0].Done, "update preserves history and completed state byte for byte");
            File.Copy(target + ".previous", target, true);
            bool failed = false;
            try { UpdateInstaller.ReplaceAndRestart(target, candidate, asset, version, path => { throw new IOException("simulated launch failure"); }); }
            catch (IOException) { failed = true; }
            Check(failed && Convert.ToBase64String(File.ReadAllBytes(target)) == Convert.ToBase64String(original), "failed restart rolls back previous executable");
            Check(File.ReadAllText(data.FilePath) == history, "rollback leaves all task data unchanged");
            File.AppendAllText(candidate, "corrupted");
            rejected = false;
            try { UpdateInstaller.ReplaceAndRestart(target, candidate, asset, version, path => { launches++; }); } catch (InvalidDataException) { rejected = true; }
            Check(rejected && Convert.ToBase64String(File.ReadAllBytes(target)) == Convert.ToBase64String(original), "corrupt payload cannot replace existing app");

            string stage = Path.Combine(UpdateService.StagingRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            try
            {
                string fixture = Path.Combine(Path.GetDirectoryName(production), "update-fixture", "DailyTodo.exe");
                string helper = Path.Combine(stage, "helper.exe");
                File.Copy(production, helper);
                File.Copy(fixture, Path.Combine(stage, "candidate.exe"));
                File.Copy(fixture, target, true);
                File.AppendAllText(target, "previous-integration-build");
                asset.size = new FileInfo(fixture).Length;
                using (var sha = System.Security.Cryptography.SHA256.Create())
                using (var stream = File.OpenRead(fixture))
                    asset.digest = "sha256:" + BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                var job = new UpdateJob { Target = target, Tag = "v" + BuildVersion.Value, Asset = asset,
                    SourcePid = int.MaxValue, SourceStarted = 1 };
                string jobPath = Path.Combine(stage, "job.json");
                File.WriteAllText(jobPath, new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(job));
                using (var worker = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(helper, "--apply-update \"" + jobPath + "\"")
                    { UseShellExecute = false, CreateNoWindow = true, WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden }))
                {
                    if (!worker.WaitForExit(10000)) { worker.Kill(); worker.WaitForExit(); throw new Exception("Update helper timed out"); }
                    Check(worker.ExitCode == 0, "standalone update helper replaces and relaunches app");
                }
                string marker = Path.Combine(dir, "update-started.txt");
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (!File.Exists(marker) && DateTime.UtcNow < deadline) System.Threading.Thread.Sleep(25);
                Check(File.Exists(marker), "replacement executable starts successfully");
                Check(File.ReadAllText(data.FilePath) == history, "full helper update preserves existing task data");
            }
            finally { UpdateService.DeleteStage(stage); }
        }

        static void TestHistory(string root)
        {
            var history = new TodoStore(Path.Combine(root, "history", "tasks.xml"));
            history.Add("归档测试");
            var item = history.Data.Items[0];
            var created = item.CreatedAt;
            Check(created.HasValue && created.Value.Kind == DateTimeKind.Utc, "new item records UTC creation time");
            history.Toggle(item.Id);
            var completed = item.CompletedAt;
            Check(completed.HasValue && item.Done, "completion records completion time");
            history.Edit(item.Id, "归档前修改");
            Check(item.CreatedAt == created && item.CompletedAt == completed, "editing preserves creation and completion timestamps");
            Check(history.Archive(item.Id) && item.ArchivedAt.HasValue && item.Done && history.Data.Items.Count == 1, "archive moves state without duplication or deletion");
            Check(!history.Archive(item.Id), "repeat archive is ignored");
            history.Save();
            history = new TodoStore(history.FilePath);
            item = history.Data.Items[0];
            Check(item.Archived && item.CreatedAt == created && item.CompletedAt == completed && item.ArchivedAt.HasValue, "archive and all timestamps survive restart");
            Check(history.Restore(item.Id) && !item.Archived && item.ArchivedAt == null && item.Done && item.CreatedAt == created && item.CompletedAt == completed, "restore preserves task state and original times");
            history.Toggle(item.Id);
            Check(!item.Done && item.CompletedAt == null, "undo completion clears completion time");
            Check(history.Archive(item.Id) && !item.Done && item.CompletedAt == null, "unfinished task can be archived");
            history.Delete(item.Id);
            Check(history.Data.Items.Count == 0, "delete removes archived record");
            string legacy = Path.Combine(root, "legacy.xml");
            File.WriteAllText(legacy, "<TodoData><Items><TodoItem><Id>legacy-id</Id><Text>旧事项</Text><Done>true</Done></TodoItem></Items></TodoData>");
            var old = new TodoStore(legacy);
            Check(old.Data.Items[0].Done && old.Data.Items[0].CreatedAt == null && old.Data.Items[0].CompletedAt == null && !old.Data.Items[0].Archived,
                "legacy tasks keep content and do not fabricate dates");
            Check(HistoryWindow.TimeLabel(null) == "未记录", "missing historical dates have an explicit label");
        }

        static void TestDailyUpdateStatus(string root)
        {
            var cache = new TodoStore(Path.Combine(root, "update-status", "tasks.xml"));
            var day = new DateTime(2026, 11, 16, 10, 0, 0);
            Check(UpdateStatus.NeedsCheck(cache.Data, day), "first launch needs daily update check");
            UpdateStatus.Record(cache.Data, new ReleaseInfo { tag_name = "v" + BuildVersion.Value }, day);
            Check(!UpdateStatus.NeedsCheck(cache.Data, day.AddHours(3)) && !UpdateStatus.HasUpdate(cache.Data), "same-day current release uses cache without badge");
            Check(UpdateStatus.NeedsCheck(cache.Data, day.AddDays(1)), "new system date requires another update check");
            var newer = new ReleaseInfo { tag_name = "v99.0.0", assets = new[] { new ReleaseAsset {
                name = "Daily.exe", state = "uploaded", size = 2048, digest = "sha256:" + new string('0', 64),
                browser_download_url = "https://github.com/march7th-26/Daily/releases/download/v99.0.0/Daily.exe" } } };
            UpdateStatus.Record(cache.Data, newer, day);
            cache.Save();
            cache = new TodoStore(cache.FilePath);
            Check(UpdateStatus.HasUpdate(cache.Data) && !UpdateStatus.NeedsCheck(cache.Data, day), "new-release badge state survives same-day restart");
            string date = cache.Data.UpdateCheckDate;
            bool rejected = false;
            try { UpdateStatus.Record(cache.Data, new ReleaseInfo { tag_name = "invalid" }, day.AddDays(1)); } catch (InvalidDataException) { rejected = true; }
            Check(rejected && cache.Data.UpdateCheckDate == date && UpdateStatus.HasUpdate(cache.Data), "failed check preserves cached badge and permits retry");
            cache.Data.UpdateCheckBuild = "0.0.0";
            Check(UpdateStatus.NeedsCheck(cache.Data, day), "installed new build checks again on the same day");
        }

        [STAThread]
        public static int Main()
        {
            string dir = Path.Combine(Path.GetTempPath(), "DailyTodo-test-" + Guid.NewGuid());
            try
            {
                TestUpdates(dir);
                TestHistory(dir);
                TestDailyUpdateStatus(dir);
                var store = new TodoStore(Path.Combine(dir, "tasks.xml"));
                Check(!store.Add(" \r\n "), "blank task rejected");
                Check(store.Add("  中文事项 <test> & emoji 🎉  "), "unicode task accepted");
                Check(store.Data.Items[0].Text == "中文事项 <test> & emoji 🎉", "text trimmed");
                Check(!store.Add(new string('x', 201)), "length limit enforced");
                string id = store.Data.Items[0].Id;
                Check(store.Edit(id, "  修正后的事项 <test> &  ") && store.Data.Items[0].Id == id && store.Data.Items.Count == 1, "edit replaces existing item without changing identity");
                Check(!store.Edit(id, "  ") && !store.Edit(id, new string('x', 201)), "invalid edits preserve original text");
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

                TestOrdering(dir);
                var app = new Application();
                var window = new MainWindow(store, false);
                window.Show();
                window.UpdateLayout();
                Check(((TextBlock)window.FindName("QuoteLabel")).Text == "每天向前一步。", "main interface displays cached daily quote");
                Check(!window.ShowInTaskbar, "window excluded from taskbar");
                var header = (Grid)window.FindName("TitleBar");
                var pinControl = (FrameworkElement)window.FindName("PinButton");
                var updateControl = (Button)window.FindName("UpdateButton");
                var minimizeControl = (FrameworkElement)window.FindName("MinimizeButton");
                var closeControl = (FrameworkElement)window.FindName("CloseButton");
                Check(pinControl.TransformToAncestor(header).Transform(new Point()).X < updateControl.TransformToAncestor(header).Transform(new Point()).X
                    && updateControl.TransformToAncestor(header).Transform(new Point()).X < minimizeControl.TransformToAncestor(header).Transform(new Point()).X
                    && minimizeControl.TransformToAncestor(header).Transform(new Point()).X < closeControl.TransformToAncestor(header).Transform(new Point()).X,
                    "header places pin and update left, minimize and close right");
                Check(updateControl.IsEnabled && System.Windows.Automation.AutomationProperties.GetName(updateControl) == "检查更新", "header update icon is enabled and accessible");
                store.Data.LatestReleaseTag = "v99.0.0";
                window.RefreshUpdateBadge();
                Check(((System.Windows.Shapes.Ellipse)window.FindName("UpdateBadge")).Visibility == Visibility.Visible && updateControl.ToolTip.ToString().Contains("99.0.0"), "newer release shows red badge with version tooltip");
                store.Data.LatestReleaseTag = "v" + BuildVersion.Value;
                window.RefreshUpdateBadge();
                Check(((System.Windows.Shapes.Ellipse)window.FindName("UpdateBadge")).Visibility == Visibility.Collapsed, "badge disappears when installed version is current");
                Check(((Grid)window.Content).Children.Count == 9, "all eight resize edges and corners installed");
                Check(app.Windows.Count == 1, "only the visible task window remains registered");
                var dialog = new DailyDialog(window, "检查更新", "现有待办和设置会保留。", "下载并更新", "稍后再说", true);
                var feedbackContent = (StackPanel)dialog.FeedbackButton.Content;
                Check(dialog.FeedbackButton.ToolTip.ToString() == DailyDialog.FeedbackUrl
                    && DailyDialog.FeedbackUrl == "https://github.com/march7th-26/Daily/issues"
                    && feedbackContent.Children[0] is System.Windows.Shapes.Path && ((TextBlock)feedbackContent.Children[1]).Text == "用户反馈",
                    "update dialog includes vector GitHub icon and user feedback link");
                Check(dialog.Shell.CornerRadius.TopLeft == 20 && dialog.Shell.Background.ToString() == "#FFFCFBF8" && !dialog.ShowInTaskbar, "update dialog shares rounded cream theme and hides from taskbar");
                window.Dispatcher.BeginInvoke(new Action(delegate {
                    dialog.UpdateLayout();
                    Point feedbackPosition = dialog.FeedbackButton.TranslatePoint(new Point(), dialog.Body);
                    Point acceptPosition = dialog.AcceptButton.TranslatePoint(new Point(), dialog.Body);
                    Check(Math.Abs(feedbackPosition.Y + dialog.FeedbackButton.ActualHeight / 2 - acceptPosition.Y - dialog.AcceptButton.ActualHeight / 2) < 1
                        && feedbackPosition.X + dialog.FeedbackButton.ActualWidth <= dialog.CancelButton.TranslatePoint(new Point(), dialog.Body).X,
                        "feedback and update actions share one footer row without overlap");
                    dialog.AcceptButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }));
                Check(dialog.ShowDialog() == true, "themed confirmation returns affirmative choice");
                var cancelDialog = new DailyDialog(window, "删除记录", "确认后删除。", "删除", "保留");
                Check(cancelDialog.FeedbackButton == null && dialog.Width == 400 && dialog.Height == 280,
                    "update dialog uses compact dimensions without changing other dialogs");
                window.Dispatcher.BeginInvoke(new Action(delegate { cancelDialog.CancelButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }));
                Check(cancelDialog.ShowDialog() == false, "themed cancel preserves negative choice");
                var infoDialog = new DailyDialog(window, "检查更新", new string('测', 4000), "知道了", null, true);
                window.Dispatcher.BeginInvoke(new Action(delegate {
                    infoDialog.UpdateLayout();
                    var messageScroll = (ScrollViewer)infoDialog.Body.Children[0];
                    Point feedbackPosition = infoDialog.FeedbackButton.TranslatePoint(new Point(), infoDialog.Body);
                    Point acceptPosition = infoDialog.AcceptButton.TranslatePoint(new Point(), infoDialog.Body);
                    Check(messageScroll.ScrollableHeight > 0 && Math.Abs(feedbackPosition.Y + infoDialog.FeedbackButton.ActualHeight / 2
                        - acceptPosition.Y - infoDialog.AcceptButton.ActualHeight / 2) < 1,
                        "long notes scroll while feedback and acknowledge remain on same row");
                    infoDialog.AcceptButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }));
                infoDialog.ShowDialog();
                bool openedHistory = false;
                window.Dispatcher.BeginInvoke(new Action(delegate {
                    foreach (Window child in window.OwnedWindows) if (child is HistoryWindow) { openedHistory = true; child.Close(); break; }
                }));
                ((Button)window.FindName("HistoryButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(openedHistory, "history icon opens themed history window");
                var tray = (System.Windows.Forms.NotifyIcon)typeof(MainWindow).GetField("tray", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(window);
                Check(System.Linq.Enumerable.Any(System.Linq.Enumerable.Cast<System.Windows.Forms.ToolStripItem>(tray.ContextMenuStrip.Items), i => i.Text == "检查更新…"), "tray exposes update command");
                var input = (TextBox)window.FindName("TaskInput");
                input.Text = "界面添加测试";
                ((Button)window.FindName("AddButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(store.Data.Items.Count == 1 && input.Text == "", "add through actual button");
                var rows = (StackPanel)window.FindName("TaskRows");
                store.Add("拖动排序测试");
                var movingTask = store.Data.Items[1];
                window.RenderTasks(); window.UpdateLayout();
                var targetCard = (Border)rows.Children[0];
                var dragField = typeof(MainWindow).GetField("draggedItem", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                dragField.SetValue(window, movingTask);
                var drop = TestDrop(targetCard);
                targetCard.RaiseEvent(drop);
                dragField.SetValue(window, null);
                Check(drop.Handled && drop.Effects == DragDropEffects.Move && store.Data.Items[0] == movingTask
                    && ((TextBlock)((Grid)((Border)rows.Children[0]).Child).Children[1]).Text == movingTask.Text,
                    "dropping task above another updates rendered order");
                Check(new TodoStore(store.FilePath).Data.Items[0].Id == movingTask.Id, "drop saves order immediately");
                var foreignDrop = TestDrop(rows.Children[0]);
                ((Border)rows.Children[0]).RaiseEvent(foreignDrop);
                Check(foreignDrop.Effects == DragDropEffects.None && store.Data.Items[0] == movingTask, "external or cancelled drag cannot reorder tasks");
                store.Delete(movingTask.Id); window.RenderTasks(); store.Save();
                var grid = (Grid)((Border)rows.Children[0]).Child;
                input.Focus(); Pump(20);
                Check(((Button)grid.Children[2]).Opacity == (grid.IsMouseOver ? 1 : 0), "row actions follow actual mouse hover while input is focused");
                ((System.Windows.Controls.Primitives.ToggleButton)grid.Children[0]).Focus(); Pump(20);
                Check(((Button)grid.Children[2]).Opacity == 1 && ((Button)grid.Children[4]).Opacity == 1, "keyboard focus reveals both archive and delete actions");
                ((System.Windows.Controls.Primitives.ToggleButton)grid.Children[0]).IsChecked = true;
                Check(store.Data.Items[0].Done, "circle toggles actual task");
                ((System.Windows.Controls.Primitives.ToggleButton)grid.Children[0]).IsChecked = false;
                Check(!store.Data.Items[0].Done, "circle reverses actual task");
                var source = PresentationSource.FromVisual(window);
                var circleButton = (System.Windows.Controls.Primitives.ToggleButton)grid.Children[0];
                circleButton.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, source, 0, System.Windows.Input.Key.F2) { RoutedEvent = UIElement.KeyDownEvent });
                var editor = (TextBox)grid.Children[3];
                Check(editor.Visibility == Visibility.Visible, "row enters inline editing");
                editor.ApplyTemplate();
                Check(((Border)editor.Template.FindName("EditorChrome", editor)).CornerRadius.TopLeft == 10
                    && editor.Template.FindName("PART_ContentHost", editor) != null, "inline editor has rounded border and functional text host");
                editor.Text = "修改后的界面事项";
                editor.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, source, 0, System.Windows.Input.Key.Enter) { RoutedEvent = UIElement.KeyDownEvent });
                Check(store.Data.Items[0].Text == "修改后的界面事项" && store.Data.Items.Count == 1 && !store.Data.Items[0].Done, "Enter saves edit without adding or completing item");
                circleButton.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, source, 0, System.Windows.Input.Key.F2) { RoutedEvent = UIElement.KeyDownEvent });
                editor.Text = "不应保存";
                editor.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, source, 0, System.Windows.Input.Key.Escape) { RoutedEvent = UIElement.KeyDownEvent });
                Check(store.Data.Items[0].Text == "修改后的界面事项" && new TodoStore(store.FilePath).Data.Items[0].Text == "修改后的界面事项", "Escape cancels and saved edit survives restart");
                Check(window.SaveBeforeUpdate() && new TodoStore(store.FilePath + ".update.bak").Data.Items[0].Text == "修改后的界面事项", "update backup contains current edited task");
                File.Delete(store.FilePath + ".update.bak");
                Directory.CreateDirectory(store.FilePath + ".update.bak");
                Check(!window.SaveBeforeUpdate(), "backup failure blocks update");
                Directory.Delete(store.FilePath + ".update.bak");
                var archiveAction = (Button)grid.Children[4];
                Check(archiveAction.Style == (Style)window.Resources["RowActionStyle"] && ((Button)grid.Children[2]).Style == archiveAction.Style, "archive and delete share hover and keyboard-focus visibility style");
                archiveAction.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(store.Data.Items.Count == 1 && store.Data.Items[0].Archived && rows.Children.Count == 0, "archive button removes item from active list without deleting it");
                var historyWindow = new HistoryWindow(window, store, delegate { window.RenderTasks(); store.Save(); });
                historyWindow.Show();
                historyWindow.UpdateLayout();
                Check(historyWindow.Shell.CornerRadius.TopLeft == 20 && historyWindow.Records.Children[0] is Border, "history window and record cards use shared rounded theme");
                var recordCard = (Border)historyWindow.Records.Children[0];
                var recordContent = (StackPanel)recordCard.Child;
                Check(recordCard.CornerRadius.TopLeft == 12 && ((TextBlock)recordContent.Children[1]).Text.Contains("创建") && ((TextBlock)recordContent.Children[1]).Text.Contains("未完成"), "history displays creation and completion status");
                var historyActions = (StackPanel)recordContent.Children[2];
                ((Button)historyActions.Children[0]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(!store.Data.Items[0].Archived && rows.Children.Count == 1 && historyWindow.Records.Children[0] is TextBlock, "history restore button returns item and updates both views");
                historyWindow.Close();
                grid = (Grid)((Border)rows.Children[0]).Child;
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
                var normalContent = window.Content;
                var surface = (Border)window.FindName("WindowSurface");
                var body = (Grid)window.FindName("TodoBody");
                window.Width = 3440;
                window.UpdateLayout();
                Point bodyOrigin = body.TranslatePoint(new Point(), surface);
                Check(body.ActualWidth <= 560 && Math.Abs(bodyOrigin.X - (surface.ActualWidth - body.ActualWidth) / 2) < 1,
                    "ultrawide window centers bounded reading column");
                Check(pinControl.Parent == minimizeControl.Parent && surface.CornerRadius.TopLeft == 20,
                    "wide normal window groups window controls and keeps rounded shell");
                window.Width = 340;
                window.UpdateLayout();
                var snapArea = new Rect(0, 0, 1200, 900);
                window.DockTo(2, snapArea);
                window.CollapseDock();
                window.WindowState = WindowState.Maximized;
                Pump(100);
                window.CollapseDock();
                Check(!window.DockCollapsed && window.Content == normalContent && window.ActualWidth > 340,
                    "maximizing collapsed dock restores full usable content");
                Check(surface.Margin == new Thickness(0) && surface.CornerRadius.TopLeft == 0 && surface.Effect == null && surface.BorderThickness == new Thickness(0),
                    "maximized shell has no transparent inset rounded corners or shadow");
                Check(pinControl.Parent == minimizeControl.Parent && body.ActualWidth <= 560 && input.ActualWidth > 100,
                    "maximized content stays readable and window buttons remain together");
                window.TryDock(new Size(window.ActualWidth, window.ActualHeight));
                window.UpdateDockHover(new Point(-10000, -10000), false, DateTime.UtcNow.AddSeconds(2));
                Check(!window.DockCollapsed && window.WindowState == WindowState.Maximized,
                    "maximized window cannot enter edge auto-hide");
                window.WindowState = WindowState.Normal;
                window.Left = 0; window.Top = 0; window.Width = 600; window.Height = 700;
                window.UpdateLayout();
                window.TryDock(new Size(340, 400));
                window.CollapseDock();
                Check(!window.DockCollapsed && window.Width == 600 && window.Height == 700,
                    "Windows snap resize takes priority over edge docking");
                window.DockTo(-1, snapArea);
                window.CollapseDock();
                Check(window.DockCollapsed, "edge docking remains available after system restore");
                SendMessage(new WindowInteropHelper(window).Handle, 0x0231, IntPtr.Zero, IntPtr.Zero);
                window.CollapseDock();
                Check(!window.DockCollapsed && window.Content == normalContent,
                    "native move resize loop expands dock and suspends auto-hide");
                SendMessage(new WindowInteropHelper(window).Handle, 0x0232, IntPtr.Zero, IntPtr.Zero);
                window.ExpandDock();
                window.Width = 340; window.Height = 400;
                window.UpdateLayout();
                var longRow = (Grid)((Border)rows.Children[0]).Child;
                Check(surface.Margin == new Thickness(12) && surface.CornerRadius.TopLeft == 20 && surface.Effect != null
                    && pinControl.Parent == updateControl.Parent,
                    "restored compact window recovers original shell and button layout");
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
                    var checkingField = typeof(MainWindow).GetField("checkingUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    checkingField.SetValue(window, true);
                    try
                    {
                        window.UpdateDockHover(outside, false, tick.AddSeconds(8));
                        window.UpdateDockHover(outside, false, tick.AddSeconds(9));
                        Check(!window.DockCollapsed, "update prompt keeps panel expanded " + side);
                    }
                    finally { checkingField.SetValue(window, false); }
                }
                var originalCursor = System.Windows.Forms.Cursor.Position;
                try
                {
                    // Synthetic geometry tests above queue native window moves.
                    // Drain those moves before deriving the physical screen origin.
                    var updateFlag = typeof(MainWindow).GetField("checkingUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    updateFlag.SetValue(window, true);
                    Pump(75);
                    var screen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(window).Handle).WorkingArea;
                    var origin = window.PointFromScreen(new Point(screen.Left, screen.Top));
                    var end = window.PointFromScreen(new Point(screen.Right, screen.Bottom));
                    var area = new Rect(origin.X + window.Left, origin.Y + window.Top, end.X - origin.X, end.Y - origin.Y);
                    updateFlag.SetValue(window, false);
                    foreach (int side in new[] { -1, 1, 2 })
                    {
                        System.Windows.Forms.Cursor.Position = new System.Drawing.Point(screen.Left + screen.Width / 2, screen.Bottom - 30);
                        window.DockTo(side, area);
                        window.CollapseDock();
                        window.UpdateLayout();
                        // Let the native transparent HWND catch up before PointToScreen.
                        Pump(75);
                        var target = window.PointToScreen(new Point(side == -1 ? 1 : side == 1 ? window.Width - 1 : window.Width / 2,
                            side == 2 ? 1 : window.Height / 2));
                        System.Windows.Forms.Cursor.Position = new System.Drawing.Point((int)target.X, (int)target.Y);
                        Pump(150);
                        if (window.DockCollapsed) Console.WriteLine("Hover diagnostic: side=" + side + " target=" + target + " actual=" + System.Windows.Forms.Cursor.Position + " window=" + new Rect(window.Left, window.Top, window.Width, window.Height));
                        Check(!window.DockCollapsed, "real cursor and timer expand edge " + side);
                        bool collapsedWhileHovering = false;
                        SizeChangedEventHandler observe = delegate { if (window.DockCollapsed) collapsedWhileHovering = true; };
                        window.SizeChanged += observe;
                        Pump(850);
                        window.SizeChanged -= observe;
                        if (window.DockCollapsed || collapsedWhileHovering) Console.WriteLine("Hover diagnostic: side=" + side + " target=" + target + " actual=" + System.Windows.Forms.Cursor.Position + " window=" + new Rect(window.Left, window.Top, window.Width, window.Height));
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
