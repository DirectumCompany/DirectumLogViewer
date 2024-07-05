using System;

namespace LogReader
{
  /// <summary>
  /// Объект строки лога.
  /// </summary>
  public class LogLine
  {
    public DateTime Time { get; set; }
    public string Level { get; set; }
    public string Logger { get; set; }
    public string Message { get; set; }
    public string FullMessage { get; set; }
    public string UserName { get; set; }
    public string Tenant { get; set; }
    public string Version { get; set; }
    public string Pid { get; set; }
    public string Trace { get; set; }
    public int NumLine { get; set; }

  }
}
