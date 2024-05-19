using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Renci.SshNet;

namespace LogReader
{
  public class RemoteLogWatcher : LogWatcher
  {
    public new delegate void FileReCreatedHandler();
    public new event FileReCreatedHandler FileReCreated;
    private ConnectionInfo info;

    /// <summary>
    /// ctor с созданием наблюдателем за файлом по указанному пути. Тригер на изменение файла.
    /// </summary>
    /// <param name="filePath">Путь до файла.</param>
    public RemoteLogWatcher(string filePath, ConnectionInfo info) : base(filePath) => this.info = info;

    /// <summary>
    /// Чтение файла. При повторном срабатывании читает файл с прошлого места окончания чтения.
    /// </summary>
    public override void ReadToEndLine()
    {
      lock (readLock)
      {
        using (var client = new SftpClient(info))
        {
          client.Connect();
          using var fileStream = client.OpenRead(this.filePath);
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
          client.Disconnect();
        }
      }
    }
  }
}
