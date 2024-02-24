namespace LogViewer
{

  /// <summary>
  /// Лог файл.
  /// </summary>
  class LogFile
  {
    public string FullPath { get; }
    public string Name { get; }

    public LogFile(string fullPath, string name)
    {
      this.FullPath = fullPath;
      this.Name = name;
    }

    public LogFile(string fullPath)
    {
      this.FullPath = fullPath;
      this.Name = System.IO.Path.GetFileName(fullPath);
    }
  }
}
