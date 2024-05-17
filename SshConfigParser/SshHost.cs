using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Renci.SshNet;

namespace SshConfigParser
{
  public class SshHost
  {

    public SftpClient? sftpClient;

    /// <summary>
    /// Identity file for the host.
    /// </summary>
    public string IdentityFile
    {
      get => this[nameof(IdentityFile)]?.ToString();
      set => this[nameof(IdentityFile)] = value;
    }

    /// <summary>
    /// Password for the user.
    /// </summary>
    public string Password
    {
      get => this[nameof(Password)]?.ToString();
      set => this[nameof(Password)] = value;
    }

    /// <summary>
    /// The host alias
    /// </summary>
    public string Host
    {
      get => this[nameof(Host)]?.ToString();
      set => this[nameof(Host)] = value;
    }

    /// <summary>
    /// Full host name
    /// </summary>
    public string Name
    {
      get => this[nameof(Name)]?.ToString();
      set => this[nameof(Name)] = value;
    }

    /// <summary>
    /// User
    /// </summary>
    public string User
    {
      get => this[nameof(User)]?.ToString();
      set => this[nameof(User)] = value;
    }

    /// <summary>
    /// Port
    /// </summary>
    public string Port
    {
      get => this[nameof(Port)]?.ToString() ?? "22";
      set => this[nameof(Port)] = value;
    }

    /// <summary>
    /// Logs folder
    /// </summary>
    public string LogsFolder
    {
      get => this[nameof(LogsFolder)]?.ToString();
      set => this[nameof(LogsFolder)] = value;
    }

    /// <summary>
    /// Logs folder
    /// </summary>
    public bool IsRemote
    {
      get => (bool)(this[nameof(IsRemote)] ?? true);
      set => this[nameof(IsRemote)] = value;
    }

    /// <summary>
    /// A collection of all the properties for this host, including those explicitly defined.
    /// </summary>
    public Dictionary<string, object> Properties { get; set;  } = new Dictionary<string, object>();

    public object this[string key]
    {
      get
      {
        if (Properties.TryGetValue(key, out var value))
          return value;

        return null;
      }
      set { Properties[key] = value; }
    }

    /// <summary>
    /// Keys of all items in the SSH host.
    /// </summary>
    public IEnumerable<string> Keys => Properties.Keys;

    public override string ToString() => this.Name;
  }
}
