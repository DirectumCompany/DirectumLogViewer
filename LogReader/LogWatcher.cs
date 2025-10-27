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
    public event FileReCreatedHandler FileReCreated;

    private readonly object readLock = new object();
    private long fileLength;
    private long position;
    private readonly string filePath;
    private Timer timer;

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
    public void ReadToEndLine()
    {
      lock (readLock)
      {
        try
        {
          if (StopIfFileMissing())
            return;

          using (var streamReader = OpenStreamReader())
          {
            long currentLength = streamReader.BaseStream.Length;

            if (DetectAndHandleRecreation(streamReader, currentLength))
            {
              // После пересоздания читаем с начала
              currentLength = streamReader.BaseStream.Length;
            }

            ReadNewLines(streamReader, currentLength);
          }
        }
        catch (FileNotFoundException)
        {
          ResetState();
        }
        catch (IOException)
        {
          // Транзитные ошибки доступа/шары — пропускаем тик
        }
        catch (UnauthorizedAccessException)
        {
          // Нет временного доступа — пропускаем тик
        }
      }
    }

    private void OnTimedEvent(Object source, ElapsedEventArgs e)
    {
      try
      {
        ReadToEndLine();
      }
      finally
      {
        if (IsWatching && timer != null)
          timer.Start();
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
    /// Обработчик закрытия файлов.
    /// </summary>
    public void Dispose()
    {
      if (timer != null)
      {
        timer.Dispose();
        timer = null;
      }
      IsWatching = false;
    }

    /// <summary>
    /// Проверяет наличие файла. Если файл отсутствует, сбрасывает состояние
    /// (позицию и длину) и сообщает, что чтение следует прервать.
    /// </summary>
    /// <returns>true, если файл отсутствует и дальнейшее чтение не требуется; иначе false.</returns>
    private bool StopIfFileMissing()
    {
      if (!File.Exists(this.filePath))
      {
        ResetState();
        return true;
      }
      return false;
    }

    /// <summary>
    /// Открывает поток чтения для лог-файла с корректным режимом совместного доступа
    /// (ReadWrite | Delete), чтобы не блокировать ротацию/замену файла по SMB.
    /// </summary>
    /// <returns>Экземпляр StreamReader для чтения файла.</returns>
    private StreamReader OpenStreamReader()
    {
      var fileStream = new FileStream(this.filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
      return new StreamReader(fileStream);
    }

    /// <summary>
    /// Определяет пересоздание или обнуление файла (уменьшение длины или позиция за пределами конца)
    /// и, при необходимости, сбрасывает позицию, обновляет внутреннее состояние и вызывает событие FileReCreated.
    /// </summary>
    /// <param name="streamReader">Поток чтения текущего файла.</param>
    /// <param name="currentLength">Текущая длина файла в байтах.</param>
    /// <returns>true, если обнаружено пересоздание/обнуление; иначе false.</returns>
    private bool DetectAndHandleRecreation(StreamReader streamReader, long currentLength)
    {
      if (currentLength < fileLength || this.position > currentLength)
      {
        streamReader.DiscardBufferedData();
        streamReader.BaseStream.Seek(0, SeekOrigin.Begin);
        this.position = 0;
        fileLength = 0;
        FileReCreated?.Invoke();
        return true;
      }
      return false;
    }

    /// <summary>
    /// Читает новые строки из файла, накапливает их блоками и публикует через событие BlockNewLines.
    /// Обновляет текущую позицию чтения и показатель прогресса.
    /// </summary>
    /// <param name="streamReader">Поток чтения текущего файла.</param>
    /// <param name="currentLength">Текущая длина файла в байтах.</param>
    private void ReadNewLines(StreamReader streamReader, long currentLength)
    {
      string line;
      List<string> lines = new List<string>();

      streamReader.BaseStream.Position = this.position;
      fileLength = currentLength;

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

    /// <summary>
    /// Сбрасывает внутренние счётчики состояния чтения (позиция и длина файла).
    /// </summary>
    private void ResetState()
    {
      fileLength = 0;
      this.position = 0;
    }

  }
}
