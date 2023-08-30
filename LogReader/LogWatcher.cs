using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Timers;

namespace LogReader
{
    /// <summary>
    /// Наблюдатель за изменениями в лог файле.
    /// </summary>
    public class LogWatcher : IDisposable
    {
        public bool IsWatching { get; private set; }

        public delegate void BlockNewLinesHandler(List<string> lines, bool isEndFile, double progress);
        public event BlockNewLinesHandler BlockNewLines;

        public delegate void FileReCreatedHandler();
        public event FileReCreatedHandler FileReCreated;

        //private FileStream fileStream;
        //private StreamReader streamReader;
        private Timer timer;
        private FileSystemWatcher fileSystemWatcher;
        private readonly object readLock = new object();
        private long fileLength;
        private string filePath;


        private const int LineBlockSize = 500;

        public LogWatcher(string filePath)
        {
            this.filePath = filePath;
            //fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            //streamReader = new StreamReader(fileStream);
            //fileLength = streamReader.BaseStream.Length;
            var qq = Path.GetDirectoryName(this.filePath);
            var tt = Path.GetFileName(this.filePath);
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

        public void StartWatch(int period)
        {
            if (IsWatching)
                throw new Exception("Log file already watching");

            IsWatching = true;

            timer = new Timer();
            timer.AutoReset = false;
            timer.Interval = period;
            timer.Elapsed += OnTimedEvent;
            timer.Start();
        }
        public void StartFileSystemWatcher()
        {
            if (IsWatching)
                throw new Exception("Log file already watching");

            IsWatching = true;
        }

        public void ReadToEndLine()
        {
            lock (readLock)
            {
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var streamReader = new StreamReader(fileStream);
                var current_length = streamReader.BaseStream.Length;

                if (current_length < fileLength)
                {
                    streamReader.DiscardBufferedData();
                    streamReader.BaseStream.Seek(0, SeekOrigin.Begin);
                    FileReCreated?.Invoke();
                }

                fileLength = current_length;
                string line;
                List<string> lines = new List<string>();

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
            }
        }
        public void ReadToEndLineWithoutLock()
        {
            lock (readLock)
            {
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var streamReader = new StreamReader(fileStream);
                var current_length = streamReader.BaseStream.Length;

                if (current_length < fileLength)
                {
                    streamReader.DiscardBufferedData();
                    streamReader.BaseStream.Seek(0, SeekOrigin.Begin);
                    FileReCreated?.Invoke();
                }


                string line;
                List<string> lines = new List<string>();
                streamReader.BaseStream.Seek(fileLength == 0 ? fileLength : fileLength -2, SeekOrigin.Begin);
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


            }
        }

        private void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            ReadToEndLineWithoutLock();
            timer.Start();
        }
        private void OnChange(object sender, FileSystemEventArgs e)
        {
            ReadToEndLineWithoutLock();
        }

        private void InvokeBlockNewLinesEvent(List<string> lines, StreamReader streamReader)
        {
            var progress = fileLength == 0 ? 100 : 100 * streamReader.BaseStream.Position / fileLength;
            BlockNewLines?.Invoke(lines, streamReader.Peek() == -1, progress);
        }

        public void Dispose()
        {
            if (timer != null)
            {
                timer.Dispose();
                timer = null;
            }
            if (fileSystemWatcher != null)
            {
                fileSystemWatcher.Dispose();
                fileSystemWatcher = null;
            }

            //if (streamReader != null)
            //{
            //    streamReader.Dispose();
            //    streamReader = null;
            //}

            //if (fileStream != null)
            //{
            //    fileStream.Dispose();
            //    fileStream = null;
            //}
        }

    }
}
