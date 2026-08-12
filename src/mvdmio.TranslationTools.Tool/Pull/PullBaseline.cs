using System.Text.Json;

namespace mvdmio.TranslationTools.Tool.Pull;

internal sealed class PullBaseline
{
   public const string FileName = ".mvdmio-translations.pull.json";

   public required PullBaselineItem[] Items { get; init; }
}

internal sealed class PullBaselineItem
{
   public required string Origin { get; init; }
   public required string Locale { get; init; }
   public required string Key { get; init; }
   public string? Value { get; init; }
}

internal sealed class PullBaselineLookup
{
   private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
   {
      WriteIndented = true,
      PropertyNameCaseInsensitive = true
   };

   private readonly Dictionary<(string Origin, string Locale, string Key), string?> _values;

   private PullBaselineLookup(Dictionary<(string Origin, string Locale, string Key), string?> values)
   {
      _values = values;
   }

   public static string GetPath(string projectDirectory)
   {
      return Path.Combine(projectDirectory, PullBaseline.FileName);
   }

   public static async Task WriteAsync(
      IPullFileSystem fileSystem,
      string projectDirectory,
      IEnumerable<PullBaselineItem> items,
      CancellationToken cancellationToken
   )
   {
      var baseline = new PullBaseline
      {
         Items = items.ToArray()
      };
      var json = JsonSerializer.Serialize(baseline, SerializerOptions);
      await fileSystem.WriteAllTextAsync(GetPath(projectDirectory), json, cancellationToken);
   }

   public static async Task<PullBaselineLookup?> TryLoadAsync(string projectDirectory, CancellationToken cancellationToken)
   {
      var path = GetPath(projectDirectory);
      if (!File.Exists(path))
         return null;

      var json = await File.ReadAllTextAsync(path, cancellationToken);
      var baseline = JsonSerializer.Deserialize<PullBaseline>(json, SerializerOptions);
      if (baseline?.Items is null)
         return null;

      var values = new Dictionary<(string Origin, string Locale, string Key), string?>(new BaselineKeyComparer());
      foreach (var item in baseline.Items)
      {
         values[(NormalizeOrigin(item.Origin), item.Locale, item.Key)] = NormalizeValue(item.Value);
      }

      return new PullBaselineLookup(values);
   }

   public bool TryGetValue(string origin, string locale, string key, out string? value)
   {
      return _values.TryGetValue((NormalizeOrigin(origin), locale, key), out value);
   }

   public static string? ResolvePushValue(
      PullBaselineLookup? baseline,
      string origin,
      string locale,
      string key,
      string? localValue,
      string defaultLocale
   )
   {
      if (baseline is null)
         return localValue;

      if (!baseline.TryGetValue(origin, locale, key, out var pulledValue))
         return localValue;

      if (string.Equals(locale, defaultLocale, StringComparison.OrdinalIgnoreCase))
         return null;

      return string.Equals(NormalizeValue(localValue), pulledValue, StringComparison.Ordinal)
         ? null
         : localValue;
   }

   private static string NormalizeOrigin(string origin)
   {
      return origin.Trim().Replace('\\', '/');
   }

   private static string? NormalizeValue(string? value)
   {
      return value == string.Empty ? null : value;
   }

   private sealed class BaselineKeyComparer : IEqualityComparer<(string Origin, string Locale, string Key)>
   {
      public bool Equals((string Origin, string Locale, string Key) x, (string Origin, string Locale, string Key) y)
      {
         return string.Equals(x.Origin, y.Origin, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Locale, y.Locale, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Key, y.Key, StringComparison.OrdinalIgnoreCase);
      }

      public int GetHashCode((string Origin, string Locale, string Key) obj)
      {
         return HashCode.Combine(
            obj.Origin.ToLowerInvariant(),
            obj.Locale.ToLowerInvariant(),
            obj.Key.ToLowerInvariant()
         );
      }
   }
}
