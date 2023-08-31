using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace LogReader
{
    /// <summary>
    /// Наблюдатель за изменениями в лог файле.
    /// </summary>
    public class LogWatcher : IDisposable
    {
        public bool IsWatching { get; private set; }

        public delegate void BlockNewLinesHandler(List<string> lines, double progress);
        public event BlockNewLinesHandler BlockNewLines;

        public delegate void FileReCreatedHandler();
        public event FileReCreatedHandler FileReCreated;

        private FileSystemWatcher fileSystemWatcher;
        private readonly object readLock = new object();
        private long fileLength;
        private long position;
        private string filePath;
        private const int GridUpdateTimer = 1000;
        private DateTime lastRead = DateTime.MinValue;

        /// <summary>
        /// Кол-во max строк для блока записи.
        /// </summary>
        private const int LineBlockSize = 500;

        /// <summary>
        /// ctor с созданием наблюдателем за файлом по указанному пути. Тригер на изменение файла.
        /// </summary>
        /// <param name="filePath">Путь до файла.</param>
        public LogWatcher(string filePath)
        {
            this.filePath = filePath;
            fileSystemWatcher = new FileSystemWatcher(Path.GetDirectoryName(this.filePath))
            {
                NotifyFilter = NotifyFilters.Attributes
                                | NotifyFilters.CreationTime
                                | NotifyFilters.DirectoryName
                                | NotifyFilters.FileName
                                | NotifyFilters.LastAccess
                                | NotifyFilters.LastWrite
                                | NotifyFilters.Security
                                | NotifyFilters.Size
            };
            fileSystemWatcher.Filter = Path.GetFileName(this.filePath);
            fileSystemWatcher.IncludeSubdirectories = true;
            fileSystemWatcher.EnableRaisingEvents = true;
            fileSystemWatcher.Changed += OnChange;
            fileLength = 0;
        }

        /// <summary>
        /// При возможном сбое повторного просмотра файла - вылетает ошибка.
        /// </summary>
        /// <exception cref="Exception">Log file already watching</exception>
        public void StartFileSystemWatcher()
        {
            if (IsWatching)
                throw new Exception("Log file already watching");

            IsWatching = true;
        }

        /// <summary>
        /// Чтение файла. При повторном срабатывании читает файл с прошлого места окончания чтения.
        /// </summary>
        public void ReadToEndLine()
        {
            lock (readLock)
            {
                using var fileStream = new FileStream(this.filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var streamReader = new StreamReader(fileStream);
                long current_length = streamReader.BaseStream.Length;
                if (current_length < fileLength)
                {
                    streamReader.DiscardBufferedData();
                    streamReader.BaseStream.Seek(0, SeekOrigin.Begin);
                    this.position = 0;
                    FileReCreated?.Invoke();
                }


                string line;
                List<string> lines = new List<string>();
                streamReader.BaseStream.Position = this.position;
                fileLength = current_length;
                while (streamReader != null && (line = streamReader.ReadLine()) != null)
                {
                    if (!String.IsNullOrEmpty(line))
                    {
                        lines.Add(line);

                        if (lines.Count >= LineBlockSize)
                        {
                            InvokeBlockNewLinesEvent(lines, streamReader);
                            lines.Clear();
                        }
                    }
                }

                if (lines.Count > 0)
                    InvokeBlockNewLinesEvent(lines, streamReader);
                this.position = streamReader.BaseStream.Position;

            }
        }

        /// <summary>
        /// Event на изменение файла.
        /// </summary>
        private void OnChange(object sender, FileSystemEventArgs e)
        {
            DateTime lastWriteTime = File.GetLastWriteTime(this.filePath);
            if (lastWriteTime != lastRead)
            {
                Thread.Sleep(GridUpdateTimer);
                ReadToEndLine();
                lastRead = lastWriteTime;

            }
        }

        /// <summary>
        /// Заполнение прогресса.
        /// Вызов делегата BlockNewLines.
        /// </summary>
        /// <param name="lines">Новые прочитанные строки.</param>
        /// <param name="streamReader">Экземпляр reader для работы в обработчике.</param>
        private void InvokeBlockNewLinesEvent(List<string> lines, StreamReader streamReader)
        {
            var progress = fileLength == 0 ? 100 : 100 * streamReader.BaseStream.Position / fileLength;
            BlockNewLines?.Invoke(lines, progress);
        }

        /// <summary>
        /// Обработчик закрытияй файлов.
        /// </summary>
        public void Dispose()
        {
            if (fileSystemWatcher != null)
            {
                fileSystemWatcher.Dispose();
                fileSystemWatcher = null;
            }
        }

    }
}
