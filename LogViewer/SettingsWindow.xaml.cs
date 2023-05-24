using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.Windows;

namespace LogViewer
{
  /// <summary>
  /// Interaction logic for SettingsWindow.xaml
  /// </summary>
  public partial class SettingsWindow : Window
  {
    public static string LogsPath { get; set; }
    public static string WhitelistLogs { get; set; }
    public static bool AssociateLogFile { get; set; }
    public static bool AssociateLogFileChanged { get { return associateLogFileOldValue != AssociateLogFile; }  }
    private static bool associateLogFileOldValue { get; set; }
    public static bool UseBackgroundNotification { get; set; }
    public static bool CloseToTray { get; set; }

    private const string DefaultLogPath = @"D:\Projects\master\Logs";
    private const string RegKey = @"SOFTWARE\JsonLogViewerSettings";
    private const string LogsPathKey = "LogsPath";
    private const string WhitelistKey = "WhiteList";
    private const string AssociateLogFileKey = "AssociateLogFile";
    private const string UseBackgroundNotificationKey = "UseBackgroundNotification";
    private const string CloseToTrayKey = "CloseToTray";
    private static readonly List<string> DefaultListLogs = new List<string>
    {
      "${machinename}.WebServer.${shortdate}",
      "${machinename}.Worker.${shortdate}",
      "${machinename}.WorkerNetFx.${shortdate}",
      "${machinename}.WorkflowBlockService.${shortdate}",
      "${machinename}.WorkflowProcessService.${shortdate}",
      "${machinename}.IntegrationService.${shortdate}",
      "${machinename}.StorageService.${shortdate}",
      "${machinename}.WcfServer.${shortdate}"
    };

    public SettingsWindow()
    {
      InitializeComponent();
    }

    public static bool IsFirstRun()
    {
      using RegistryKey key = Registry.CurrentUser.CreateSubKey(RegKey);
      var isEmpty = (string)key.GetValue(LogsPathKey, null) == null;
      key.Close();
      return isEmpty;
    }

    public static bool? ShowSettingsDialog()
    {
      Load();

      var dialog = new SettingsWindow();
      var result = dialog.ShowDialog();

      if (result == true)
      {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RegKey);
        key.SetValue(LogsPathKey, dialog.LogsPathTextBox.Text);
        key.SetValue(WhitelistKey, dialog.WhiteListTextBox.Text);
        key.SetValue(AssociateLogFileKey, dialog.AssociateLogFileCheckBox.IsChecked);
        key.SetValue(UseBackgroundNotificationKey, dialog.UseBackgroundNotificationCheckBox.IsChecked);
        key.SetValue(CloseToTrayKey, dialog.CloseToTrayCheckBox.IsChecked);
        key.Close();
      }

      return result;
    }

    public static void Load()
    {
      using RegistryKey key = Registry.CurrentUser.CreateSubKey(RegKey);

      LogsPath = (string)key.GetValue(LogsPathKey, DefaultLogPath);

      WhitelistLogs = (string)key.GetValue(WhitelistKey, string.Join("\n", DefaultListLogs));

      AssociateLogFile = Convert.ToBoolean(key.GetValue(AssociateLogFileKey, false));
      associateLogFileOldValue = AssociateLogFile;

      UseBackgroundNotification = Convert.ToBoolean(key.GetValue(UseBackgroundNotificationKey, false));

      key.Close();
    }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
      if (!System.IO.Directory.Exists(LogsPathTextBox.Text))
      {
        MessageBox.Show($"Directory '{LogsPathTextBox.Text}' not exist", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        return;
      }

      if (String.IsNullOrEmpty(WhiteListTextBox.Text.Trim()))
      {
        MessageBox.Show("Whitelist is empty", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        return;
      }

      this.DialogResult = true;
    }

    private void SelectLogPath_Click(object sender, RoutedEventArgs e)
    {
      var dialog = new CommonOpenFileDialog
      {
        IsFolderPicker = true
      };

      if (CommonFileDialogResult.Ok == dialog.ShowDialog())
      {
        LogsPathTextBox.Text = dialog.FileName;
        this.Focus();
      }
    }
  }
}
