using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogViewer
{
  internal class Host
  {
    public string Name { get; }
    public string LogsFolder { get; }
    public bool IsRemote { get; }
    public Host(string name, string logsFolder = "", bool isRemote = false)
    {
      Name = name;
      LogsFolder = logsFolder;
      IsRemote = isRemote;
    }

    public override string ToString() => this.Name.ToString();
  }
}
