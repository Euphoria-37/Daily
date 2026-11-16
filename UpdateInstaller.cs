using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows;

namespace DailyTodo
{
    public class UpdateJob
    {
        public string Target { get; set; }
        public string Tag { get; set; }
        public ReleaseAsset Asset { get; set; }
        public int SourcePid { get; set; }
        public long SourceStarted { get; set; }
    }

    public static class UpdateInstaller
    {
        public static string Prepare(string candidate, ReleaseInfo release)
        {
            string stage = Path.GetDirectoryName(candidate);
            string target = Assembly.GetExecutingAssembly().Location;
            var asset = UpdateService.ValidateRelease(release);
            UpdateService.VerifyFile(candidate, asset, UpdateService.ParseVersion(release.tag_name));
            // Check write access before asking the running application to exit.
            string probe = Path.Combine(Path.GetDirectoryName(target), ".Daily-" + Guid.NewGuid().ToString("N") + ".tmp");
            using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { }
            File.Copy(target, Path.Combine(stage, "helper.exe"));
            var current = Process.GetCurrentProcess();
            var job = new UpdateJob { Target = target, Tag = release.tag_name, Asset = asset,
                SourcePid = current.Id, SourceStarted = current.StartTime.ToUniversalTime().Ticks };
            string file = Path.Combine(stage, "job.json");
            File.WriteAllText(file, new JavaScriptSerializer().Serialize(job));
            return file;
        }

        public static void Start(string job)
        {
            Process.Start(new ProcessStartInfo(Path.Combine(Path.GetDirectoryName(job), "helper.exe"), "--apply-update \"" + job + "\"")
            { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
        }

        public static void ReplaceAndRestart(string target, string candidate, ReleaseAsset asset, Version version, Action<string> launch)
        {
            target = Path.GetFullPath(target);
            if (!string.Equals(Path.GetExtension(target), ".exe", StringComparison.OrdinalIgnoreCase)
                || AssemblyName.GetAssemblyName(target).Name != "DailyTodo") throw new InvalidDataException("原程序路径无效。");
            UpdateService.VerifyFile(candidate, asset, version);
            string replacement = Path.Combine(Path.GetDirectoryName(target), ".Daily-" + Guid.NewGuid().ToString("N") + ".new");
            string previous = target + ".previous";
            bool replaced = false;
            try
            {
                File.Copy(candidate, replacement, false);
                UpdateService.VerifyFile(replacement, asset, version);
                File.Replace(replacement, target, previous);
                replaced = true;
                launch(target);
            }
            catch
            {
                if (replaced) File.Replace(previous, target, null);
                throw;
            }
            finally { if (File.Exists(replacement)) File.Delete(replacement); }
        }

        public static int Run(string jobPath)
        {
            string target = null;
            try
            {
                string stage = Path.GetDirectoryName(Path.GetFullPath(jobPath));
                Guid id;
                if (Path.GetFileName(jobPath) != "job.json" || !Guid.TryParseExact(Path.GetFileName(stage), "N", out id)
                    || !string.Equals(Path.GetDirectoryName(stage), Path.GetFullPath(UpdateService.StagingRoot), StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(Path.Combine(stage, "helper.exe"), Assembly.GetExecutingAssembly().Location, StringComparison.OrdinalIgnoreCase)
                    || new FileInfo(jobPath).Length > 8192) throw new InvalidDataException("更新任务无效。");
                var job = new JavaScriptSerializer().Deserialize<UpdateJob>(File.ReadAllText(jobPath));
                if (job == null || job.SourcePid <= 0 || job.SourcePid == Process.GetCurrentProcess().Id || job.Asset == null)
                    throw new InvalidDataException("更新任务信息无效。");
                UpdateService.ValidateRelease(new ReleaseInfo { tag_name = job.Tag, assets = new[] { job.Asset } });
                target = Path.GetFullPath(job.Target);
                if (AssemblyName.GetAssemblyName(target).Name != "DailyTodo") throw new InvalidDataException("原程序无效。");
                Process source = null;
                try { source = Process.GetProcessById(job.SourcePid); } catch (ArgumentException) { }
                if (source != null)
                {
                    using (source)
                    {
                        try
                        {
                            if (source.StartTime.ToUniversalTime().Ticks == job.SourceStarted && !source.WaitForExit(30000))
                                throw new IOException("Daily 尚未退出，更新已取消。");
                        }
                        catch (InvalidOperationException) { if (!source.HasExited) throw; }
                    }
                }
                ReplaceAndRestart(target, Path.Combine(stage, "candidate.exe"), job.Asset, UpdateService.ParseVersion(job.Tag), path => {
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(path) });
                });
                return 0;
            }
            catch (Exception ex)
            {
                DailyDialog.Info(null, "更新失败", "更新未完成，待办数据未被修改。\n" + ex.Message + "\n请重新打开原来的 Daily。");
                return 1;
            }
        }
    }
}
