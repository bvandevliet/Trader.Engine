using System.Collections;
using System.Globalization;
using System.Reflection;

namespace TraderEngine.CLI.Helpers;

public static class WordPressDbSerializer
{
  [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
  public class WordPressObjectAttribute : Attribute
  {
    public string Name { get; }

    public WordPressObjectAttribute(string name)
    {
      Name = name;
    }
  }

  public static string Serialize(object? value)
  {
    if (value == null)
    {
      return $"N;";
    }
    else if (value is char charValue)
    {
      return $"s:1:\"{charValue}\";";
    }
    else if (value is string stringValue)
    {
      return $"s:{stringValue.Length}:\"{stringValue}\";";
    }
    else if (value is short or ushort or int or uint or long or ulong)
    {
      return $"i:{value};";
    }
    else if (value is float floatValue)
    {
      return $"d:{floatValue.ToString(CultureInfo.InvariantCulture)};";
    }
    else if (value is double doubleValue)
    {
      return $"d:{doubleValue.ToString(CultureInfo.InvariantCulture)};";
    }
    else if (value is decimal decimalValue)
    {
      return $"d:{decimalValue.ToString(CultureInfo.InvariantCulture)};";
    }
    else if (value is bool boolValue)
    {
      return $"b:{(boolValue ? 1 : 0)};";
    }
    else if (value is DateTime dateTimeValue)
    {
      string formattedDate = dateTimeValue.ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture);
      string tzKind = dateTimeValue.Kind.ToString();

      return $"O:8:\"DateTime\":3:{{s:4:\"date\";s:{formattedDate.Length}:\"{formattedDate}\";s:13:\"timezone_type\";i:3;s:8:\"timezone\";s:{tzKind.Length}:\"{tzKind}\";}}";
    }

    var type = value.GetType();

    if (type.IsArray)
    {
      var array = (Array)value;
      var elements = new List<string>();

      for (int index = 0; index < array.Length; index++)
      {
        elements.Add($"{Serialize(index)}{Serialize(array.GetValue(index)!)}");
      }

      return $"a:{elements.Count}:{{{string.Join("", elements)}}}";
    }
    else if (value is IEnumerable enumerable)
    {
      var elements = new List<string>();

      int index = 0;

      foreach (object? item in enumerable)
      {
        var itemType = item.GetType();

        if (itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
        {
          elements.Add(Serialize(item));
        }
        else
        {
          elements.Add($"{Serialize(index)}{Serialize(item)}");
        }

        index++;
      }

      return $"a:{elements.Count}:{{{string.Join("", elements)}}}";
    }
    else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
    {
      var keyProperty = type.GetProperty("Key")!;
      var valueProperty = type.GetProperty("Value")!;

      object key = keyProperty.GetValue(value)!;
      object val = valueProperty.GetValue(value)!;

      return $"{Serialize(key)}{Serialize(val)}";
    }
    else if (type.IsClass)
    {
      var properties = type.GetProperties();
      var elements = new List<string>();

      foreach (var propertyInfo in properties)
      {
        elements.Add($"s:{propertyInfo.Name.Length}:\"{propertyInfo.Name}\";{Serialize(propertyInfo.GetValue(value)!)}");
      }

      var objectName = type.GetCustomAttribute<WordPressObjectAttribute>()?.Name ?? type.Name;

      return $"O:{objectName.Length}:\"{objectName}\":{elements.Count}:{{{string.Join("", elements)}}}";
    }

    return "";
  }

  private static object? Deserialize(string value, Type type, out int endIndex)
  {
    if (value.StartsWith("N;"))
    {
      endIndex = 2;

      return null;
    }
    else if (value.StartsWith("s:"))
    {
      endIndex = value.IndexOf(';') + 1;

      return Convert.ChangeType(value[(value.IndexOf('"') + 1)..(endIndex - 2)], type);
    }
    else if (value.StartsWith("i:"))
    {
      endIndex = value.IndexOf(';') + 1;

      return Convert.ChangeType(value[2..(endIndex - 1)], type);
    }
    else if (value.StartsWith("d:"))
    {
      endIndex = value.IndexOf(';') + 1;

      return Convert.ChangeType(value[2..(endIndex - 1)], type);
    }
    else if (value.StartsWith("b:"))
    {
      endIndex = value.IndexOf(';') + 1;

      return value[2..(endIndex - 1)] == "1";
    }
    else if (value.StartsWith("a:"))
    {
      endIndex = value.IndexOf('{') + 1;

      int elementsCount = int.Parse(value[2..(endIndex - 2)]);

      var genArgs = type.GenericTypeArguments;

      bool isAssoc = genArgs.Length == 2;

      var keyType =
        genArgs.Length <= 1 ? typeof(int) : genArgs[0];
      var valType =
        type.HasElementType ? type.GetElementType()! :
        genArgs.Length == 1 ? genArgs[0] : genArgs[1];

      var dictionaryType = typeof(Dictionary<,>).MakeGenericType(keyType, valType);

      object instance = Activator.CreateInstance(dictionaryType)!;

      for (int index = 0; index < elementsCount; index++)
      {
        object key = Deserialize(value[endIndex..], keyType, out int keyEnd)!;

        endIndex += keyEnd;

        object? val = Deserialize(value[endIndex..], valType, out int valEnd);

        endIndex += valEnd;

        dictionaryType.GetMethod("Add")!.Invoke(instance, new[] { key, val });
      }

      // To account for closing '}'.
      endIndex++;

      if (isAssoc)
      {
        return instance;
      }

      object listValues = dictionaryType.GetProperty("Values")!.GetValue(instance)!;

      var toListMethod = type.IsArray
        ? typeof(Enumerable).GetMethod("ToArray")!.MakeGenericMethod(valType)
        : typeof(Enumerable).GetMethod("ToList")!.MakeGenericMethod(valType);

      return toListMethod.Invoke(null, new object[] { listValues })!;
    }
    // TODO: Add support for stdClass !!
    else if (value.StartsWith("O:"))
    {
      endIndex = value.IndexOf('{') + 1;

      string[] meta = value[..(endIndex - 1)].Split(':');

      if (meta[2].Trim('"') == "DateTime")
      {
        string[] parts = value[endIndex..].Split(';');

        // TODO: Handle timezone !!
        string formattedDate = parts[1].Split('"')[1];
        //string tzKind = parts[5].Split('"')[1];

        return DateTime.Parse(formattedDate, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
      }

      object instance = Activator.CreateInstance(type)!;

      int elementsCount = int.Parse(meta[3]);

      for (int index = 0; index < elementsCount; index++)
      {
        string keyName = (string)Deserialize(value[endIndex..], typeof(string), out int keyEnd)!;

        endIndex += keyEnd;

        var propertyInfo = type.GetProperty(keyName)!;

        object? val = Deserialize(value[endIndex..], propertyInfo.PropertyType, out int valEnd);

        endIndex += valEnd;

        propertyInfo.SetValue(instance, val);
      }

      // To account for closing '}'.
      endIndex++;

      return instance;
    }

    endIndex = 0; return null;
  }

  public static object? Deserialize(string value, Type type)
  {
    return Deserialize(value, type, out _);
  }

  public static T? Deserialize<T>(string value)
  {
    return (T?)Deserialize(value, typeof(T));
  }
}