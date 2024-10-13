namespace LogViewer
{
  class LogFileOpener
  {
    // Тип открытия лог файла
    public LogFileOpenerType Type { get; }

    // Имя лог файла
    public string Name { get; }

    // Путь к лог файлу
    public string PathToFile { get; }

    public LogFileOpener(string name, LogFileOpenerType type)
    {
      this.Name = name;
      this.Type = type;
    }

    public LogFileOpener(string name, string pathToFile, LogFileOpenerType type)
    {
      this.Name = name;
      this.PathToFile = pathToFile;
      this.Type = type;
    }
  }

  // Перечисление, определяющее типы открытия лог файла
  enum LogFileOpenerType
  {
    FromFileDirect,      // Прямое открытие файла
    FromFileWithDialog,  // Открытие файла через диалоговое окно
    FromClipboard        // Открытие файла из буфера обмена
  }
}
