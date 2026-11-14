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

        public static async Task<bool> RefreshAsync(TodoData data, DateTime now, Func<Task<string>> fetch)
        {
            if (IsCurrent(data, now)) return false;
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

        void InitializeQuote(bool enabled)
        {
            onlineQuotes = enabled;
            ShowQuote();
            Loaded += async delegate { await RefreshQuoteAsync(); };
            Closed += delegate { quoteClosed = true; };
        }

        void ShowQuote()
        {
            var label = Part<System.Windows.Controls.TextBlock>("QuoteLabel");
            label.Text = string.IsNullOrWhiteSpace(store.Data.QuoteText) ? "把今天，过得有条不紊。" : store.Data.QuoteText;
            label.ToolTip = string.IsNullOrWhiteSpace(store.Data.QuoteText) ? "每日语句 · 联网后自动更新" : "每日语句 · 一言 · " + store.Data.QuoteDate;
        }

        async Task RefreshQuoteAsync()
        {
            DateTime now = DateTime.Now;
            if (!onlineQuotes || quoteClosed || quoteBusy || DailyQuote.IsCurrent(store.Data, now) || now < quoteRetryAfter) return;
            quoteBusy = true;
            try
            {
                // Fetch without sending local state. Apply only while the window is alive.
                string text = await DailyQuote.FetchAsync();
                if (quoteClosed) return;
                if (await DailyQuote.RefreshAsync(store.Data, now, () => Task.FromResult(text)))
                {
                    ShowQuote();
                    Save();
                }
            }
            catch (Exception ex)
            {
                if (!(ex is HttpRequestException || ex is TaskCanceledException || ex is FormatException || ex is XmlException)) throw;
                quoteRetryAfter = DateTime.Now.AddMinutes(30);
                Part<System.Windows.Controls.TextBlock>("QuoteLabel").ToolTip = "暂时无法更新，每日语句已保留；稍后自动重试。";
            }
            finally { quoteBusy = false; }
        }
    }
}
