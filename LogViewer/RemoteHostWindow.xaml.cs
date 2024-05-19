using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
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
    private const string RegKey = @"SOFTWARE\JsonLogViewerSettings\RemoteHost\";
    private const string HostKey = "Host";
    private const string NameKey = "Name";
    private const string UserKey = "User";
    private const string PasswordKey = "Password";
    private const string IdentityFilePathKey = "IdentityFile";
    private const string LogsPathKey = "LogsFolder";
    private const string PortKey = "Port";
    private readonly string InitialSshKeyDirectory = $"C:\\Users\\{Environment.UserName}\\.ssh";

    public RemoteHostWindow()
    {
      InitializeComponent();
    }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
     // var result = SshConfig.ParseFile(SshConfigPath);
      var host = new SshHost
      {
        Host = this.Host.Text,
        Name = this.Name.Text,
        User = this.Username.Text,
      };

      using var key = Registry.CurrentUser.CreateSubKey(RegKey + host.Name);
      key.SetValue(HostKey, host.Host);
      key.SetValue(UserKey, host.User);
      key.SetValue(NameKey, host.Name);
      key.SetValue(LogsPathKey, this.LogsPath.Text);
      key.SetValue(PortKey, this.Port.Text);
      var pass = this.Password.Password;
      if (string.IsNullOrWhiteSpace(pass))
      {
        host.IdentityFile = this.IdentityFile.Text;
        key.SetValue(IdentityFilePathKey, host.IdentityFile);
      }
      else
        key.SetValue(PasswordKey, pass);

      key.Close();
   //   result.Add(host);
    //  File.WriteAllTextAsync(SshConfigPath, result.ToString());
      this.DialogResult = true;
    }

    private void IdentityFileButton_Click(object sender, RoutedEventArgs e)
    {
      var dialog = new OpenFileDialog();
      dialog.Filter = "All files (*.*)|*.*";
      dialog.InitialDirectory = InitialSshKeyDirectory;

      if (dialog.ShowDialog() ?? false)
        IdentityFile.Text = dialog.FileName;

      this.Focus();
    }
  }
}
