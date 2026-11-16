using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace DailyTodo
{
    public class TodoItem
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public bool Done { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? ArchivedAt { get; set; }
        public bool Archived { get; set; }
    }

    public class TodoData
    {
        public List<TodoItem> Items { get; set; }
        public bool Pinned { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string QuoteText { get; set; }
        public string QuoteDate { get; set; }
        public string UpdateCheckDate { get; set; }
        public string UpdateCheckBuild { get; set; }
        public string LatestReleaseTag { get; set; }
        public TodoData()
        {
            Items = new List<TodoItem>();
            Pinned = true;
            Left = double.NaN;
            Top = double.NaN;
            Width = 390;
            Height = 540;
        }
    }

    public class TodoStore
    {
        public string FilePath { get; private set; }
        public TodoData Data { get; private set; }
        public TodoStore(string path)
        {
            FilePath = path;
            Data = new TodoData();
            if (!File.Exists(path)) return;
            try
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                using (var reader = XmlReader.Create(path, settings))
                    Data = (TodoData)new XmlSerializer(typeof(TodoData)).Deserialize(reader);
                if (Data == null || Data.Items == null || Data.Items.Any(t => t == null || string.IsNullOrWhiteSpace(t.Id) || t.Text == null)
                    || Data.Items.Select(t => t.Id).Distinct().Count() != Data.Items.Count)
                    throw new InvalidDataException("Invalid task data");
            }
            catch (Exception ex)
            {
                if (!(ex is InvalidOperationException || ex is XmlException || ex is IOException || ex is UnauthorizedAccessException)) throw;
                throw new InvalidDataException("无法读取待办数据，原文件已保留：" + path, ex);
            }
        }

        public bool Add(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Trim().Length > 200) return false;
            Data.Items.Add(new TodoItem { Id = Guid.NewGuid().ToString("N"), Text = text.Trim(), Done = false, CreatedAt = DateTime.UtcNow });
            return true;
        }
        public void Toggle(string id)
        {
            var item = Data.Items.Find(t => t.Id == id);
            if (item != null && !item.Archived)
            {
                item.Done = !item.Done;
                item.CompletedAt = item.Done ? (DateTime?)DateTime.UtcNow : null;
            }
        }
        public bool Edit(string id, string text)
        {
            var item = Data.Items.Find(t => t.Id == id);
            if (item == null || item.Archived || string.IsNullOrWhiteSpace(text) || text.Trim().Length > 200) return false;
            item.Text = text.Trim();
            return true;
        }
        public void Delete(string id) { Data.Items.RemoveAll(t => t.Id == id); }

        public bool Move(string id, string targetId, bool after)
        {
            int from = Data.Items.FindIndex(t => t.Id == id && !t.Archived);
            int target = Data.Items.FindIndex(t => t.Id == targetId && !t.Archived);
            if (from < 0 || target < 0 || from == target) return false;
            int destination = target + (after ? 1 : 0);
            if (from < destination) destination--;
            if (from == destination) return false;
            var item = Data.Items[from];
            Data.Items.RemoveAt(from);
            Data.Items.Insert(destination, item);
            return true;
        }

        public bool Archive(string id)
        {
            var item = Data.Items.Find(t => t.Id == id);
            if (item == null || item.Archived) return false;
            item.Archived = true;
            item.ArchivedAt = DateTime.UtcNow;
            return true;
        }

        public bool Restore(string id)
        {
            var item = Data.Items.Find(t => t.Id == id);
            if (item == null || !item.Archived) return false;
            item.Archived = false;
            item.ArchivedAt = null;
            return true;
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            string temp = FilePath + ".tmp";
            try
            {
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    new XmlSerializer(typeof(TodoData)).Serialize(stream, Data);
                    stream.Flush(true);
                }
                if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak");
                else File.Move(temp, FilePath);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
