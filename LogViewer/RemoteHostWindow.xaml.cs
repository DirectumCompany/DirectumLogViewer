using SshConfigParser;
using System;
using System.IO;
using System.Windows;

namespace LogViewer
{
  /// <summary>
  /// Логика взаимодействия для RemoteHostWindow.xaml
  /// </summary>
  public partial class RemoteHostWindow : Window
  {
    private static string SshConfigPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh", "config");
    public RemoteHostWindow()
    {
      InitializeComponent();
    }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
      var result = SshConfig.ParseFile(SshConfigPath);
      var host = new SshHost
      {
        Host = this.HostName.Text,
        HostName = this.HostName.Text,
        User = this.Username.Text,
      };

      var pass = this.Password.Text;
      if (string.IsNullOrWhiteSpace(pass))
        host.IdentityFile = this.IdentityFile.Text;
      else
        host.Properties.Add("#PASS", pass);
       
      host.Properties.Add("#LOGSPATH", this.LogsPath.Text);
      result.Add(host);
      File.WriteAllTextAsync(SshConfigPath, result.ToString());

      this.DialogResult = true;
    }
  }
}
