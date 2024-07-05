namespace LogViewer
{
  class LogFileOpener
  {
    // Тип открытия лог файла
    public LogFileOpenerType Type { get; set; }

    // Имя лог файла
    public string Name { get; set; }

    // Путь к лог файлу
    public string PathToFile { get; set; }

    public LogFileOpener(string name, LogFileOpenerType type)
    {
      this.Name = name;
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
