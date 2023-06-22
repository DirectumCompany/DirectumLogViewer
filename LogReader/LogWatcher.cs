using System;
using System.Collections.Generic;
using System.IO;
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

    private FileStream fileStream;
    private StreamReader streamReader;
    private Timer timer;
    private readonly object readLock = new object();
    private long fileLength;

    private const int LineBlockSize = 500;

    public LogWatcher(string filePath)
    {
      fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
      streamReader = new StreamReader(fileStream);
      fileLength = streamReader.BaseStream.Length;
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

    public void ReadToEndLine()
    {
      lock (readLock)
      {
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
              InvokeBlockNewLinesEvent(lines);
              lines.Clear();
            }
          }
        }

        if (lines.Count > 0)
          InvokeBlockNewLinesEvent(lines);
      }
    }

    private void OnTimedEvent(Object source, ElapsedEventArgs e)
    {
      ReadToEndLine();
      timer.Start();
    }

    private void InvokeBlockNewLinesEvent(List<string> lines)
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

      if (streamReader != null)
      {
        streamReader.Dispose();
        streamReader = null;
      }

      if (fileStream != null)
      {
        fileStream.Dispose();
        fileStream = null;
      }
    }

  }
}
