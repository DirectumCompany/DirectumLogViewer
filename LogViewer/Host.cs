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
    public Host(string name, string logsFolder = "")
    {
      Name = name;
      LogsFolder = logsFolder;
    }

    public override string ToString() => this.Name.ToString();
  }
}
