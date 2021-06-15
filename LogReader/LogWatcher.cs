using System;
using System.IO;
using System.Threading;

namespace LogReader
{
  /// <summary>
  /// Наблюдатель за изменениями в лог файле.
  /// </summary>
  public class LogWatcher : IDisposable
  {
    public bool IsStartedWatching { get => timer != null; }

    public delegate void NewLineHandler(string line, bool isEndLine, double process);
    public event NewLineHandler NewLine;

    public delegate void FileReCreatedHandler();
    public event FileReCreatedHandler FileReCreated;

    private FileStream fileStream;
    private StreamReader streamReader;
    private Timer timer;
    private readonly object readLock = new object();
    private long fileLength;

    public LogWatcher(string filePath)
    {
      fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
      streamReader = new StreamReader(fileStream);
      fileLength = streamReader.BaseStream.Length;
    }

    public void StartWatch(int period)
    {
      if (timer != null)
        throw new Exception("Already started watching");

      var timerState = new TimerState { Counter = 0 };

      timer = new Timer(
          callback: new TimerCallback(TimerTask),
          state: timerState,
          dueTime: period / 2,
          period: period);
    }

    private void TimerTask(object timerState)
    {
      ReadToEndLine();
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
        while (streamReader != null && (line = streamReader.ReadLine()) != null)
        {
          if (!String.IsNullOrEmpty(line))
          {
            var process = 100 * (streamReader.BaseStream.Position + 1) / (fileLength + 1);
            NewLine?.Invoke(line, streamReader.Peek() == -1, process);
          }
        }
      }
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

    class TimerState
    {
      public int Counter;
    }
  }
}
