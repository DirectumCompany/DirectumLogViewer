using System.Windows;
using System.Windows.Input;

namespace LogViewer
{
  /// <summary>
  /// Interaction logic for SearchWindow.xaml
  /// </summary>
  public partial class SearchWindow : Window
  {
    public SearchWindow()
    {
      InitializeComponent();
    }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
      this.DialogResult = true;
    }

    private void SearchText_KeyDown(object sender, KeyEventArgs e)
    {
      if (e.Key == Key.Return)
      {
        this.Accept_Click(null, null);
      }
    }
  }
}
