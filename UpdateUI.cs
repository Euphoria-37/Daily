using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace DailyTodo
{
    public partial class MainWindow
    {
        bool checkingUpdate;
        string pendingUpdate;

        void InitializeUpdates(System.Windows.Forms.ContextMenuStrip menu, bool automatic)
        {
            autoUpdateEnabled = automatic;
            RefreshUpdateBadge();
            Loaded += async delegate { await AutoCheckUpdatesAsync(); };
            menu.Items.Add(new System.Windows.Forms.ToolStripMenuItem("Daily v" + BuildVersion.Value) { Enabled = false });
            var check = menu.Items.Add("检查更新…");
            var updateButton = Part<System.Windows.Controls.Button>("UpdateButton");
            Func<Task> checkForUpdates = async delegate {
                if (checkingUpdate) return;
                checkingUpdate = true;
                check.Enabled = false;
                updateButton.IsEnabled = false;
                updateButton.ToolTip = "正在检查更新…";
                string candidate = null;
                try
                {
                    ExpandDock();
                    ShowWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle, 9);
                    Activate();
                    check.Text = "正在检查更新…";
                    var release = await FetchUpdateAsync();
                    if (quoteClosed) return;
                    UpdateStatus.Record(store.Data, release, DateTime.Now);
                    RefreshUpdateBadge();
                    Save();
                    if (UpdateService.ParseVersion(release.tag_name) <= UpdateService.ParseVersion(BuildVersion.Value))
                    {
                        Inform("检查更新", "当前版本 v" + BuildVersion.Value + " 已是最新，无需更新。" + UpdateService.ReleaseNotes(release), true);
                        return;
                    }
                    UpdateService.ValidateRelease(release);
                    if (!Confirm("发现更新", "发现新版本 " + release.tag_name + "（当前 v" + BuildVersion.Value + "）。\n下载更新后将重启，现有待办和设置会保留。" + UpdateService.ReleaseNotes(release), "下载并更新", "稍后再说", true)) return;
                    check.Text = "正在下载更新…";
                    updateButton.ToolTip = "正在下载更新…";
                    Part<System.Windows.Controls.TextBlock>("SaveLabel").Text = "正在下载更新…";
                    candidate = await UpdateService.DownloadAsync(release);
                    if (quoteClosed) return;
                    pendingUpdate = UpdateInstaller.Prepare(candidate, release);
                    Close();
                    // A cancelled close must never leave a deferred installation armed.
                    if (!quoteClosed) pendingUpdate = null;
                }
                catch (Exception ex)
                {
                    pendingUpdate = null;
                    if (!quoteClosed) Inform("检查更新", "无法完成更新，请稍后重试。\n" + ex.Message, true);
                }
                finally
                {
                    if (candidate != null && pendingUpdate == null) UpdateService.DeleteStage(Path.GetDirectoryName(candidate));
                    if (!quoteClosed) {
                        check.Text = "检查更新…"; check.Enabled = true;
                        updateButton.IsEnabled = true; RefreshUpdateBadge();
                        Part<System.Windows.Controls.TextBlock>("SaveLabel").Text = saveFailed ? "保存失败" : "本机自动保存";
                    }
                    checkingUpdate = false;
                }
            };
            check.Click += async delegate { await checkForUpdates(); };
            updateButton.Click += async delegate { await checkForUpdates(); };
            Closed += delegate {
                if (pendingUpdate == null) return;
                try { UpdateInstaller.Start(pendingUpdate); }
                catch (Exception ex) { DailyDialog.Info(null, "更新失败", "无法启动更新，请重新打开 Daily。\n" + ex.Message); }
            };
        }

        internal bool SaveBeforeUpdate()
        {
            if (saveFailed) return false;
            try
            {
                // One fixed backup, no task-data access from the replacement helper.
                File.Copy(store.FilePath, store.FilePath + ".update.bak", true);
                return true;
            }
            catch (Exception ex)
            {
                if (!(ex is IOException || ex is UnauthorizedAccessException)) throw;
                return false;
            }
        }
    }
}
