using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Windows;

namespace LogViewer
{
  public class FileAssociations
  {
    // needed so that Explorer windows get refreshed after the registry is updated
    [System.Runtime.InteropServices.DllImport("Shell32.dll")]
    private static extern int SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);

    private const int SHCNE_ASSOCCHANGED = 0x8000000;
    private const int SHCNF_FLUSH = 0x1000;
    private const string Extension = ".log";
    private const string ProgId = "Directum_Log_Viewer_File";
    private const string FileTypeDescription = "Directum Log Viewer File";

    public static void SetAssociation()
    {
      try
      {
        var filePath = Process.GetCurrentProcess().MainModule.FileName;
        SetAssociation(Extension, ProgId, FileTypeDescription, filePath);
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero);
      }
      catch (Exception ex)
      {
        MessageBox.Show($"Error setting file association: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }

    public static void RemoveAssociation()
    {
      try
      {
        RemoveAssociation(Extension, ProgId);
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero);
      }
      catch (Exception ex)
      {
        MessageBox.Show($"Error remove file association: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }

    public static void SetAssociation(string extension, string progId, string fileTypeDescription, string applicationFilePath)
    {
      var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\" + extension, true);
      if (key != null)
      {
        key.DeleteSubKey("UserChoice", false);
        key.Close();
      }

      SetKeyDefaultValue(@"Software\Classes\" + extension, progId);
      SetKeyDefaultValue(@"Software\Classes\" + progId, fileTypeDescription);
      SetKeyDefaultValue($@"Software\Classes\{progId}\shell\open\command", "\"" + applicationFilePath + "\" \"%1\"");
    }

    public static void RemoveAssociation(string extension, string progId)
    {
      DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts", extension);
      SetKeyDefaultValue(@"Software\Classes\" + extension, string.Empty);
      DeleteSubKeyTree(@"Software\Classes", progId);
    }

    private static bool SetKeyDefaultValue(string keyPath, string value)
    {
      using (var key = Registry.CurrentUser.CreateSubKey(keyPath))
      {
        if (key.GetValue(null) as string != value)
        {
          key.SetValue(null, value);
          return true;
        }
      }

      return false;
    }

    private static void DeleteSubKeyTree(string key, string subkey)
    {
      using (var keyroot = Registry.CurrentUser.OpenSubKey(key, RegistryKeyPermissionCheck.ReadWriteSubTree))
      {
        if (keyroot == null)
          return;

        keyroot.GetAccessControl(System.Security.AccessControl.AccessControlSections.All);

        keyroot.DeleteSubKeyTree(subkey, false);
      }
    }
  }
}
