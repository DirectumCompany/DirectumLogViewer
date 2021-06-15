using System;
using System.IO;
using System.Text;
using LogReader;
using Microsoft.Toolkit.Uwp.Notifications;

namespace LogViewer
{
  /// <summary>
  /// Обработчик лог файла.
  /// </summary>
  class LogHandler
  {
    public static readonly string ConvertedFilePrefix = "Converted_";

    public static readonly string LogLevelError = "Error";

    private readonly string filePath;
    private readonly Uri icon;
    private readonly bool backgroundConvert;

    private readonly string directoryName;
    private readonly string fileName;

    private StreamWriter writer;
    private readonly LogWatcher watcher;
    private readonly int watchPeriod = 3000;

    public LogHandler(string filePath, Uri icon, bool backgroundConvert)
    {
      this.filePath = filePath;
      this.icon = icon;
      this.backgroundConvert = backgroundConvert;

      directoryName = Path.GetDirectoryName(filePath);
      fileName = Path.GetFileName(filePath);

      if (this.backgroundConvert)
      {
        try
        {
          writer = new StreamWriter(Path.Combine(directoryName, ConvertedFilePrefix + fileName), false, new UTF8Encoding());
        }
        catch
        {
          // TODO Сообщить пользователю о проблеме, фоновая конвертация выключена по умолчанию.
        }
      }

      watcher = new LogWatcher(filePath);
      watcher.NewLine += OnNewLine;
      watcher.FileReCreated += OnFileReCreated;
      watcher.ReadToEndLine();
      watcher.StartWatch(watchPeriod);
    }

    private void OnNewLine(string line, bool isEndLine, double process)
    {
      // Write to converted log
      if (backgroundConvert && writer != null)
      {
        var converted = Converter.ConvertToTsv(line);

        writer.Write(converted);
        writer.Write('\n');
        if (isEndLine)
          writer.Flush();
      }

      // Show notify
      if (watcher.IsStartedWatching)
      {
        var logLine = Converter.ConvertToObject(line);

        if (logLine.Level == LogLevelError)
        {
          try
          {
            new ToastContentBuilder()
                .AddArgument(MainWindow.NotificationTypeKey, MainWindow.NotificationError)
                .AddArgument(MainWindow.NotificationFilePathKey, filePath)
                .AddArgument(MainWindow.NotificationTimeKey, logLine.Time.Ticks.ToString())
                .AddAppLogoOverride(icon, ToastGenericAppLogoCrop.Circle)
                .AddText(fileName)
                .AddText(logLine.Message)
                .Show();
          }
          catch
          {
            // TODO не всегда приходят уведомлялки
          }
        }
      }
    }

    private void OnFileReCreated()
    {
      if (writer != null)
      {
        writer.Close();
        writer = new StreamWriter(Path.Combine(directoryName, ConvertedFilePrefix + fileName), false, new UTF8Encoding());
      }
    }
  }
}
