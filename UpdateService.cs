using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace DailyTodo
{
    // These DTOs mirror GitHub's public release API fields.
    public class ReleaseAsset
    {
        public string name { get; set; }
        public string browser_download_url { get; set; }
        public string digest { get; set; }
        public long size { get; set; }
        public string state { get; set; }
    }
    public class ReleaseInfo
    {
        public string tag_name { get; set; }
        public string body { get; set; }
        public bool draft { get; set; }
        public bool prerelease { get; set; }
        public ReleaseAsset[] assets { get; set; }
    }

    public static class UpdateService
    {
        public const long MaxDownloadSize = 64 * 1024 * 1024;
        public static readonly string StagingRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DailyTodo", "Updates");

        public static string ReleaseNotes(ReleaseInfo release)
        {
            return "\n\n更新日志 · " + release.tag_name + "\n"
                + (string.IsNullOrWhiteSpace(release.body) ? "此版本暂未提供更新日志。" : release.body.Trim());
        }

        public static Version ParseVersion(string tag)
        {
            Version version;
            if (tag == null || !Regex.IsMatch(tag, @"^v?\d+\.\d+\.\d+$") || !Version.TryParse(tag.TrimStart('v'), out version))
                throw new InvalidDataException("发布版本号无效。");
            return new Version(version.Major, version.Minor, version.Build, 0);
        }

        public static ReleaseAsset ValidateRelease(ReleaseInfo release)
        {
            if (release == null || release.draft || release.prerelease) throw new InvalidDataException("未找到正式发布版。");
            ParseVersion(release.tag_name);
            var matches = (release.assets ?? new ReleaseAsset[0]).Where(a => a != null && a.name == "Daily.exe").ToArray();
            if (matches.Length != 1) throw new InvalidDataException("发布版缺少 Daily.exe。");
            var asset = matches[0];
            string expected = "https://github.com/march7th-26/Daily/releases/download/" + release.tag_name + "/Daily.exe";
            if (asset.browser_download_url != expected || asset.state != "uploaded" || asset.size < 1024 || asset.size > MaxDownloadSize
                || asset.digest == null || !Regex.IsMatch(asset.digest, @"^sha256:[a-fA-F0-9]{64}$"))
                throw new InvalidDataException("更新附件地址、大小或校验信息无效。");
            return asset;
        }

        static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20), MaxResponseContentBufferSize = 1024 * 1024 };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Daily/" + BuildVersion.Value);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return client;
        }

        public static async Task<ReleaseInfo> GetLatestAsync()
        {
            using (var client = CreateClient())
            {
                string json = await client.GetStringAsync("https://api.github.com/repos/march7th-26/Daily/releases/latest");
                var release = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 }.Deserialize<ReleaseInfo>(json);
                if (release == null || release.draft || release.prerelease) throw new InvalidDataException("未找到正式发布版。");
                ParseVersion(release.tag_name);
                return release;
            }
        }

        public static void VerifyFile(string file, ReleaseAsset asset, Version expectedVersion)
        {
            if (new FileInfo(file).Length != asset.size) throw new InvalidDataException("下载文件不完整。");
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(file))
            {
                string digest = "sha256:" + BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                if (!string.Equals(digest, asset.digest, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("下载文件校验失败。");
            }
            var assembly = AssemblyName.GetAssemblyName(file);
            if (assembly.Name != "DailyTodo" || assembly.Version != expectedVersion) throw new InvalidDataException("附件程序版本与发布版不一致。");
        }

        public static async Task<string> DownloadAsync(ReleaseInfo release)
        {
            var asset = ValidateRelease(release);
            Directory.CreateDirectory(StagingRoot);
            string stage = Path.Combine(StagingRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            string candidate = Path.Combine(stage, "candidate.exe");
            try
            {
                using (var client = CreateClient())
                using (var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2)))
                using (var response = await client.GetAsync(asset.browser_download_url, HttpCompletionOption.ResponseHeadersRead, timeout.Token))
                {
                    response.EnsureSuccessStatusCode();
                    var finalUri = response.RequestMessage.RequestUri;
                    if (finalUri.Scheme != "https" || !(finalUri.Host == "github.com" || finalUri.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidDataException("下载重定向地址无效。");
                    if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value != asset.size)
                        throw new InvalidDataException("下载大小与发布信息不一致。");
                    using (var input = await response.Content.ReadAsStreamAsync())
                    using (var output = new FileStream(candidate, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                    {
                        var buffer = new byte[81920];
                        long total = 0;
                        int read;
                        while ((read = await input.ReadAsync(buffer, 0, buffer.Length, timeout.Token)) > 0)
                        {
                            total += read;
                            if (total > asset.size) throw new InvalidDataException("下载文件超过预期大小。");
                            await output.WriteAsync(buffer, 0, read, timeout.Token);
                        }
                        output.Flush(true);
                    }
                }
                VerifyFile(candidate, asset, ParseVersion(release.tag_name));
                return candidate;
            }
            catch { DeleteStage(stage); throw; }
        }

        public static void DeleteStage(string stage)
        {
            string full = Path.GetFullPath(stage);
            Guid id;
            if (!string.Equals(Path.GetDirectoryName(full), Path.GetFullPath(StagingRoot), StringComparison.OrdinalIgnoreCase)
                || !Guid.TryParseExact(Path.GetFileName(full), "N", out id)) throw new InvalidDataException("更新缓存目录无效。");
            try
            {
                if (Directory.Exists(full) && (File.GetAttributes(full) & FileAttributes.ReparsePoint) == 0) Directory.Delete(full, true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        public static void CleanupStaging()
        {
            if (!Directory.Exists(StagingRoot)) return;
            string executingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            foreach (string dir in Directory.GetDirectories(StagingRoot))
            {
                Guid id;
                if (Guid.TryParseExact(Path.GetFileName(dir), "N", out id) && !string.Equals(dir, executingDirectory, StringComparison.OrdinalIgnoreCase)
                    && Directory.GetLastWriteTimeUtc(dir) < DateTime.UtcNow.AddDays(-1)) DeleteStage(dir);
            }
        }
    }
}
