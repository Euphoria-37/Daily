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
    }

    public class TodoData
    {
        public List<TodoItem> Items { get; set; }
        public bool Pinned { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
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
            Data.Items.Add(new TodoItem { Id = Guid.NewGuid().ToString("N"), Text = text.Trim(), Done = false });
            return true;
        }
        public void Toggle(string id)
        {
            var item = Data.Items.Find(t => t.Id == id);
            if (item != null) item.Done = !item.Done;
        }
        public void Delete(string id) { Data.Items.RemoveAll(t => t.Id == id); }

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
