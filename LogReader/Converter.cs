using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace LogReader
{
  public static class Converter
  {
    /// <summary>
    /// Словарь постфиксов для элементов строки лога.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> logLineElementPostfix = new Dictionary<string, string>
    {
      {"lg", " |"}
    };

    /// <summary>
    /// Значения ширины по умолчанию для элементов строки лога.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, int> defaultLogLineElementWidth = new Dictionary<string, int>
    {
      {"pid", 10},
      {"l", 5},
      {"lg", 30},
      {"tr", 20}
    };

    /// <summary>
    /// Конвертировать json строку лога в обьект.
    /// </summary>
    /// <param name="jsonLine">JSON строка лога.</param>
    /// <returns>Обьект строки лога.</returns>
    public static LogLine ConvertToObject(string jsonLine)
    {
      try
      {
        var log = new LogLine();
        var parsedLine = ParseLogLine(jsonLine);

        if (parsedLine.ContainsKey("pid"))
        {
          log.Pid = parsedLine["pid"];
          parsedLine.Remove("pid");
        }

        if (parsedLine.ContainsKey("tr"))
        {
          log.Trace = parsedLine["tr"];
          parsedLine.Remove("tr");
        }

        if (parsedLine.ContainsKey("t"))
        {
          log.Time = DateTime.Parse(parsedLine["t"]);
          parsedLine.Remove("t");
        }

        if (parsedLine.ContainsKey("l"))
        {
          log.Level = parsedLine["l"];
          parsedLine.Remove("l");
        }

        if (parsedLine.ContainsKey("lg"))
        {
          log.Logger = parsedLine["lg"];
          parsedLine.Remove("lg");
        }

        if (parsedLine.ContainsKey("un"))
        {
          log.UserName = parsedLine["un"];
          parsedLine.Remove("un");
        }

        if (parsedLine.ContainsKey("tn"))
        {
          log.Tenant = parsedLine["tn"];
          parsedLine.Remove("tn");
        }

        if (parsedLine.ContainsKey("v"))
        {
          log.Version = parsedLine["v"];
          parsedLine.Remove("v");
        }

        log.FullMessage = TsvFormat(parsedLine);

        string firstLine = log.FullMessage.Split(new[] { '\r', '\n' }).FirstOrDefault();
        log.Message = firstLine;

        return log;
      }
      catch (Exception)
      {
        return new LogLine { Message = jsonLine, FullMessage = jsonLine };
      }
    }

    public static LogLine[] ConvertLinesToObjects(List<string> lines)
    {
      var logLines = new LogLine[lines.Count];

      Parallel.ForEach(lines, (line, state, index) =>
      {
        logLines[index] = ConvertToObject(line);
      });

      return logLines;
    }

    public static Dictionary<string, string> ConvertObjectToDict(LogLine logLine)
    {
      Dictionary<string, string> result = new Dictionary<string, string>();
      result.Add("t", logLine.Time.ToString("yyyy-MM-dd HH:mm:ss.fff"));
      result.Add("l", logLine.Level);
      result.Add("lg", logLine.Logger);
      result.Add("mt", logLine.FullMessage);

      if (!string.IsNullOrEmpty(logLine.Tenant))
        result.Add("tn", logLine.Tenant);

      if (!string.IsNullOrEmpty(logLine.Version))
        result.Add("v", logLine.Version);

      if (!string.IsNullOrEmpty(logLine.UserName))
        result.Add("un", logLine.UserName);

      if (!string.IsNullOrEmpty(logLine.Pid))
        result.Add("pid", logLine.Pid);

      if (!string.IsNullOrEmpty(logLine.Trace))
        result.Add("tr", logLine.Trace);

      return result;
    }

    public static string TsvFormat(IReadOnlyDictionary<string, string> logLineElements)
    {
      var sb = new StringBuilder();
      var firstElement = true;
      var onNewLine = string.Empty;
      foreach (var element in logLineElements)
      {
        var value = element.Value;

        if (value.Length > 0 && value[0] == '\n')
        {
          onNewLine += value;
          continue;
        }

        if (defaultLogLineElementWidth.TryGetValue(element.Key, out var width))
        {
          value = value.Length > width
            ? value.Substring(value.Length - width, width)
            : value.PadLeft(width);
        }

        if (firstElement)
          firstElement = false;
        else
          sb.Append(' ');
        sb.Append(value);

        if (logLineElementPostfix.TryGetValue(element.Key, out var postfix))
          sb.Append(postfix);
      }

      if (!string.IsNullOrEmpty(onNewLine))
        sb.Append(onNewLine);

      return sb.ToString();
    }

    private static Dictionary<string, string> ParseLogLine(string jsonLine)
    {
      try
      {
        var logLineElements = new Dictionary<string, string>();
        var jsonDict = GetJsonValues(jsonLine);

        foreach (var jsonPair in jsonDict)
        {
          string value;
          switch (jsonPair.Key)
          {
            case "st":
            case "ex":
              value = ConvertException(jsonPair.Value);
              break;
            case "args":
              value = ConvertArguments(jsonPair.Value);
              break;
            case "cust":
              value = ConvertCustomProperties(jsonPair.Value);
              break;
            case "span":
              value = ConvertSpan(jsonPair.Value);
              break;
            default:
              value = Convert(jsonPair.Value);
              break;
          }

          if (!string.IsNullOrEmpty(value))
            logLineElements[jsonPair.Key] = value;
        }

        return logLineElements;
      }
      catch (Exception)
      {
        return new Dictionary<string, string> { { string.Empty, jsonLine } };
      }
    }

    /// <summary>
    /// Получить словарь ключей и значений верхнего уровня из json.
    /// </summary>
    /// <param name="json">Строка-json.</param>
    /// <returns>Словарь значений.</returns>
    private static IDictionary<string, IJEnumerable<JToken>> GetJsonValues(string json)
    {
      return JObject.Parse(json).Properties().ToDictionary(kv => kv.Name, kv => kv.Values());
    }

    /// <summary>
    /// Конвертация свойства в строку.
    /// </summary>
    /// <param name="jTokens">Набор токенов.</param>
    /// <param name="prefix">Префикс строки.</param>
    /// <param name="postfix">Постфикс строки.</param>
    /// <returns>Свойство в виде строки.</returns>
    private static string Convert(IEnumerable<JToken> jTokens, string prefix = null, string postfix = null)
    {
      var result = new StringBuilder();
      if (!string.IsNullOrEmpty(prefix))
        result.Append(prefix);
      result.AppendJoin(", ", jTokens.Select(jt => jt.ToString().Replace("\n", string.Empty).Replace("\r", string.Empty)));
      if (!string.IsNullOrEmpty(postfix))
        result.Append(postfix);
      return result.ToString();
    }

    /// <summary>
    /// Конвертировать аргументы.
    /// </summary>
    /// <param name="jTokens">Набор токенов.</param>
    /// <returns>Аргумент в виде строки.</returns>
    private static string ConvertArguments(IEnumerable<JToken> jTokens)
    {
      return Convert(jTokens, "(", ")");
    }

    /// <summary>
    /// Конвертировать свойства.
    /// </summary>
    /// <param name="jTokens">Набор токенов.</param>
    /// <returns>Свойства в виде строки.</returns>
    private static string ConvertCustomProperties(IEnumerable<JToken> jTokens)
    {
      return Convert(jTokens, "[", "]");
    }

    /// <summary>
    /// Конвертировать спан.
    /// </summary>
    /// <param name="jTokens">Набор токенов.</param>
    /// <returns></returns>
    private static string ConvertSpan(IEnumerable<JToken> jTokens)
    {
      return Convert(jTokens, "Span(", ")");
    }

    /// <summary>
    /// Конвертировать исключение.
    /// </summary>
    /// <param name="jTokens">Набор токенов.</param>
    /// <returns>Отформатированное исключение в виде строки.</returns>
    private static string ConvertException(IJEnumerable<JToken> jTokens)
    {
      var result = new StringBuilder("\n");
      var type = jTokens.OfType<JProperty>().FirstOrDefault(property => property.Name == "type")?.Value.ToString();
      var message = jTokens.OfType<JProperty>().FirstOrDefault(property => property.Name == "m")?.Value.ToString();
      var stack = jTokens.OfType<JProperty>().FirstOrDefault(property => property.Name == "stack")?.Value.ToString();

      if (!string.IsNullOrEmpty(type))
        result.Append(type);
      else
        result.AppendJoin('\n', jTokens.Select(jt => jt.ToString()));

      if (!string.IsNullOrEmpty(message))
      {
        result.Append(": ");
        result.Append(message);
      }

      if (!string.IsNullOrEmpty(stack))
      {
        result.Append("\n   ");
        result.Append(stack.Replace("\r\n", "\n"));
      }

      return result.ToString();
    }
  }
}
