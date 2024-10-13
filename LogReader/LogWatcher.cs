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

    public delegate void BlockNewLinesHandler(List<string> lines, double progress);
    public event BlockNewLinesHandler BlockNewLines;

    public delegate void FileReCreatedHandler();
    public virtual event FileReCreatedHandler FileReCreated;

    protected readonly object readLock = new object();
    protected long fileLength;
    protected long position;
    protected readonly string filePath;
    protected Timer timer;

    /// <summary>
    /// Кол-во max строк для блока записи.
    /// </summary>
    protected const int LineBlockSize = 500;

    /// <summary>
    /// ctor с созданием наблюдателем за файлом по указанному пути. Тригер на изменение файла.
    /// </summary>
    /// <param name="filePath">Путь до файла.</param>
    public LogWatcher(string filePath)
    {
      this.filePath = filePath;
      fileLength = 0;
    }

    public string GetLogFilePath()
    {
      return filePath;
    }

    /// <summary>
    /// При возможном сбое повторного просмотра файла - вылетает ошибка.
    /// </summary>
    /// <exception cref="Exception">Log file already watching</exception>
    public void StartWatch(int period)
    {
      if (IsWatching)
        throw new Exception("Log file already watching");

      IsWatching = true;

      timer = new Timer
      {
        AutoReset = false,
        Interval = period
      };
      timer.Elapsed += OnTimedEvent;
      timer.Start();
    }

    /// <summary>
    /// Чтение файла. При повторном срабатывании читает файл с прошлого места окончания чтения.
    /// </summary>
    public virtual void ReadToEndLine()
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

    protected void OnTimedEvent(Object source, ElapsedEventArgs e)
    {
      ReadToEndLine();
      timer?.Start();
    }

    /// <summary>
    /// Заполнение прогресса.
    /// Вызов делегата BlockNewLines.
    /// </summary>
    /// <param name="lines">Новые прочитанные строки.</param>
    /// <param name="streamReader">Экземпляр reader для работы в обработчике.</param>
    protected void InvokeBlockNewLinesEvent(List<string> lines, StreamReader streamReader)
    {
      var progress = fileLength == 0 ? 100 : 100 * streamReader.BaseStream.Position / fileLength;
      BlockNewLines?.Invoke(lines, progress);
    }

    /// <summary>
    /// Обработчик закрытия файлов.
    /// </summary>
    public virtual void Dispose()
    {
      if (timer != null)
      {
        timer.Dispose();
        timer = null;
      }
    }

  }
}
