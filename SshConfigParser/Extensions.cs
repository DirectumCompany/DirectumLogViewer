using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// based on https://github.com/JeremySkinner/Ssh-Config-Parser

namespace SshConfigParser
{
  internal static class Extensions
  {
    public static void ForEach<T>(this IEnumerable<T> collection, Action<T> action)
    {
      foreach (var element in collection)
      {
        action(element);
      }
    }
  }
}
