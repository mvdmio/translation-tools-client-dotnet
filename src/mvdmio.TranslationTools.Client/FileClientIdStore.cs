using System;
using System.IO;

namespace mvdmio.TranslationTools.Client;

/// <summary>
/// Default <see cref="IClientIdStore"/> that persists the client GUID as a text
/// file under the local application data folder. If the file cannot be read or
/// written for any reason, a fresh in-memory GUID is returned without throwing.
/// </summary>
public sealed class FileClientIdStore : IClientIdStore
{
   private readonly string _filePath;
   private readonly object _lock = new();

   private Guid? _cachedClientId;

   /// <summary>
   /// Creates a store using the default location:
   /// <c>{LocalApplicationData}/mvdmio/TranslationTools/client-id</c>.
   /// </summary>
   public FileClientIdStore()
      : this(DefaultDirectory())
   {
   }

   /// <summary>
   /// Creates a store that persists the client id inside the given directory.
   /// Primarily intended for tests.
   /// </summary>
   /// <param name="directory">Directory in which the <c>client-id</c> file is stored.</param>
   public FileClientIdStore(string directory)
   {
      if (string.IsNullOrWhiteSpace(directory))
         throw new ArgumentException("Directory must be provided.", nameof(directory));

      _filePath = Path.Combine(directory, "client-id");
   }

   /// <inheritdoc />
   public Guid GetOrCreateClientId()
   {
      lock (_lock)
      {
         if (_cachedClientId is { } cached)
            return cached;

         var resolved = ResolveClientId();
         _cachedClientId = resolved;
         return resolved;
      }
   }

   private Guid ResolveClientId()
   {
      try
      {
         if (File.Exists(_filePath))
         {
            var contents = File.ReadAllText(_filePath).Trim();
            if (Guid.TryParse(contents, out var existing))
               return existing;
         }

         var generated = Guid.NewGuid();

         var directory = Path.GetDirectoryName(_filePath);
         if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

         File.WriteAllText(_filePath, generated.ToString());
         return generated;
      }
      catch (Exception)
      {
         // Persistence unavailable (IO error, access denied, etc.).
         // Fall back to an in-memory GUID; never throw.
         return Guid.NewGuid();
      }
   }

   private static string DefaultDirectory()
   {
      return Path.Combine(
         Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
         "mvdmio",
         "TranslationTools"
      );
   }
}
