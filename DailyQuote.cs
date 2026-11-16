using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;

namespace DailyTodo
{
    public static class DailyQuote
    {
        static readonly HttpClient Client = CreateClient();
        static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(6), MaxResponseContentBufferSize = 8192 };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Daily/1.1");
            return client;
        }

        public static Task<string> FetchAsync()
        {
            return Client.GetStringAsync("https://v1.hitokoto.cn/?c=d&c=k&encode=text&max_length=42");
        }

        public static string DateKey(DateTime now) { return now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture); }
        public static bool IsCurrent(TodoData data, DateTime now)
        {
            return data.QuoteDate == DateKey(now) && !string.IsNullOrWhiteSpace(data.QuoteText);
        }

        public static async Task<bool> RefreshAsync(TodoData data, DateTime now, Func<Task<string>> fetch, bool force = false)
        {
            if (!force && IsCurrent(data, now)) return false;
            string text = (await fetch()).Trim();
            if (text.Length == 0 || text.Length > 80 || text.IndexOf('<') >= 0 || text.IndexOf('>') >= 0)
                throw new FormatException("Invalid quote response");
            XmlConvert.VerifyXmlChars(text);
            data.QuoteText = text;
            data.QuoteDate = DateKey(now);
            return true;
        }
    }

    public partial class MainWindow
    {
        bool quoteBusy;
        bool quoteClosed;
        DateTime quoteRetryAfter;
        bool onlineQuotes;
        internal Func<Task<string>> quoteFetch = DailyQuote.FetchAsync;

        void InitializeQuote(bool enabled)
        {
            onlineQuotes = enabled;
            ShowQuote();
            var label = Part<System.Windows.Controls.TextBlock>("QuoteLabel");
            label.Cursor = System.Windows.Input.Cursors.Hand;
            label.Focusable = true;
            System.Windows.Automation.AutomationProperties.SetName(label, "每日一言，点击换一句");
            label.MouseLeftButtonUp += async delegate(object sender, System.Windows.Input.MouseButtonEventArgs e) {
                e.Handled = true; await RefreshQuoteAsync(true);
            };
            label.KeyDown += async delegate(object sender, System.Windows.Input.KeyEventArgs e) {
                if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Space)
                { e.Handled = true; await RefreshQuoteAsync(true); }
            };
            Loaded += async delegate { await RefreshQuoteAsync(); };
            Closed += delegate { quoteClosed = true; };
        }

        void ShowQuote()
        {
            var label = Part<System.Windows.Controls.TextBlock>("QuoteLabel");
            label.Text = string.IsNullOrWhiteSpace(store.Data.QuoteText) ? "把今天，过得有条不紊。" : store.Data.QuoteText;
            label.ToolTip = "点击换一句 · " + (string.IsNullOrWhiteSpace(store.Data.QuoteText) ? "联网后自动更新" : "一言 · " + store.Data.QuoteDate);
        }

        internal async Task RefreshQuoteAsync(bool force = false)
        {
            DateTime now = DateTime.Now;
            if (!onlineQuotes || quoteClosed || quoteBusy || (!force && (DailyQuote.IsCurrent(store.Data, now) || now < quoteRetryAfter))) return;
            quoteBusy = true;
            Part<System.Windows.Controls.TextBlock>("QuoteLabel").ToolTip = "正在换一句…";
            try
            {
                // Fetch without sending local state. Apply only while the window is alive.
                string text = await quoteFetch();
                if (quoteClosed) return;
                if (await DailyQuote.RefreshAsync(store.Data, now, () => Task.FromResult(text), force))
                {
                    quoteRetryAfter = DateTime.MinValue;
                    ShowQuote();
                    Save();
                }
            }
            catch (Exception ex)
            {
                if (!(ex is HttpRequestException || ex is TaskCanceledException || ex is FormatException || ex is XmlException)) throw;
                quoteRetryAfter = DateTime.Now.AddMinutes(30);
                Part<System.Windows.Controls.TextBlock>("QuoteLabel").ToolTip = "暂时无法更新，原句已保留；点击重试。";
            }
            finally { quoteBusy = false; }
        }
    }
}
