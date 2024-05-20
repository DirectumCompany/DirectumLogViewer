using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace LogReader
{
  public class RemoteLogWatcher : LogWatcher
  {
    public new delegate void FileReCreatedHandler();
    public new event FileReCreatedHandler FileReCreated;
    private SftpClient client;
    private SftpFileStream fileStream;
    private StreamReader streamReader;

    /// <summary>
    /// ctor с созданием наблюдателем за файлом по указанному пути. Тригер на изменение файла.
    /// </summary>
    /// <param name="filePath">Путь до файла.</param>
    public RemoteLogWatcher(string filePath, ConnectionInfo info) : base(filePath)
    {
      client = new SftpClient(info);
      client.Connect();
      fileStream = client.OpenRead(filePath);
      streamReader = new StreamReader(fileStream);
      fileLength = streamReader.BaseStream.Length;
    }


    /// <summary>
    /// Чтение файла. При повторном срабатывании читает файл с прошлого места окончания чтения.
    /// </summary>
    public override void ReadToEndLine()
    {
      long current_length = streamReader.BaseStream.Length;
      if (current_length < fileLength)
      {
        streamReader.DiscardBufferedData();
        streamReader.BaseStream.Seek(0, SeekOrigin.Begin);
        FileReCreated?.Invoke();
      }

      fileLength = current_length;
      string line;
      var lines = new List<string>();
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

    public override void Dispose()
    {
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

      client.Dispose();
      base.Dispose();
    }
  }
}
