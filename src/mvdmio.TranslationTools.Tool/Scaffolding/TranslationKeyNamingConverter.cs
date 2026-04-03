using System.Text;

namespace mvdmio.TranslationTools.Tool.Scaffolding;

internal static class TranslationKeyNamingConverter
{
   public static string Convert(string propertyName, int keyNaming)
   {
      return keyNaming switch
      {
         0 => propertyName,
         1 => ToLowerSnakeCase(propertyName),
         2 => propertyName.Replace('_', '.'),
         _ => ToLowerSnakeCase(propertyName)
      };
   }

   private static string ToLowerSnakeCase(string value)
   {
      var builder = new StringBuilder(value.Length * 2);

      for (var i = 0; i < value.Length; i++)
      {
         var current = value[i];

         if (current == '_')
         {
            if (builder.Length > 0 && builder[builder.Length - 1] != '_')
               builder.Append('_');

            continue;
         }

         if (char.IsUpper(current))
         {
            var hasPrevious = i > 0;
            var previous = hasPrevious ? value[i - 1] : '\0';
            var hasNext = i < value.Length - 1;
            var next = hasNext ? value[i + 1] : '\0';

            if (
               builder.Length > 0
               && builder[builder.Length - 1] != '_'
               && (char.IsLower(previous) || char.IsDigit(previous) || (hasNext && char.IsLower(next)))
            )
            {
               builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(current));
            continue;
         }

         builder.Append(char.ToLowerInvariant(current));
      }

      return builder.ToString();
   }
}
