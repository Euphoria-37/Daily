using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace DailyTodo
{
    public static class UpdateStatus
    {
        public static bool NeedsCheck(TodoData data, DateTime now)
        { return data.UpdateCheckDate != DailyQuote.DateKey(now) || data.UpdateCheckBuild != BuildVersion.Value; }

        public static bool HasUpdate(TodoData data)
        {
            try { return UpdateService.ParseVersion(data.LatestReleaseTag) > UpdateService.ParseVersion(BuildVersion.Value); }
            catch (InvalidDataException) { return false; }
        }

        public static void Record(TodoData data, ReleaseInfo release, DateTime now)
        {
            if (release == null || release.draft || release.prerelease) throw new InvalidDataException("未找到正式发布版。");
            if (UpdateService.ParseVersion(release.tag_name) > UpdateService.ParseVersion(BuildVersion.Value)) UpdateService.ValidateRelease(release);
            data.LatestReleaseTag = release.tag_name;
            data.UpdateCheckBuild = BuildVersion.Value;
            data.UpdateCheckDate = DailyQuote.DateKey(now);
        }
    }

    public partial class MainWindow
    {
        bool autoUpdateEnabled;
        bool autoUpdateBusy;
        DateTime updateRetryAfter;
        Task<ReleaseInfo> updateRequest;

        async Task RefreshDailyContentAsync()
        { await Task.WhenAll(RefreshQuoteAsync(), AutoCheckUpdatesAsync()); }

        async Task<ReleaseInfo> FetchUpdateAsync()
        {
            if (updateRequest == null) updateRequest = UpdateService.GetLatestAsync();
            var request = updateRequest;
            try { return await request; }
            finally { if (updateRequest == request) updateRequest = null; }
        }

        internal void RefreshUpdateBadge()
        {
            bool available = UpdateStatus.HasUpdate(store.Data);
            Part<System.Windows.Shapes.Ellipse>("UpdateBadge").Visibility = available ? Visibility.Visible : Visibility.Collapsed;
            var button = Part<System.Windows.Controls.Button>("UpdateButton");
            button.ToolTip = available ? "发现新版本 " + store.Data.LatestReleaseTag + " · 点击更新" : "检查更新";
            System.Windows.Automation.AutomationProperties.SetName(button, available ? "检查更新，有新版本" : "检查更新");
        }

        async Task AutoCheckUpdatesAsync()
        {
            DateTime now = DateTime.Now;
            if (!autoUpdateEnabled || quoteClosed || autoUpdateBusy || checkingUpdate || now < updateRetryAfter || !UpdateStatus.NeedsCheck(store.Data, now)) return;
            autoUpdateBusy = true;
            try
            {
                var release = await FetchUpdateAsync();
                if (quoteClosed) return;
                UpdateStatus.Record(store.Data, release, now);
                RefreshUpdateBadge();
                Save();
            }
            catch (Exception ex)
            {
                if (!(ex is System.Net.Http.HttpRequestException || ex is TaskCanceledException || ex is IOException || ex is ArgumentException || ex is InvalidOperationException)) throw;
                updateRetryAfter = DateTime.Now.AddMinutes(30);
            }
            finally { autoUpdateBusy = false; }
        }
    }
}
