namespace LogViewer
{

  /// <summary>
  /// Описание лог файла.
  /// </summary>
  public class LogFileDescription
  {
    public string FullPath { get; }
    public string Name { get; }
    public LogFileDescriptionActionType ActionType { get; }

    public LogFileDescription(string fullPath, LogFileDescriptionActionType actionType)
    {
      this.FullPath = fullPath;
      this.Name = System.IO.Path.GetFileName(fullPath);
      this.ActionType = actionType;
    }
  }

  public enum LogFileDescriptionActionType
  {
    OpenFile,
    OpenFileFromDialog
  }
}
