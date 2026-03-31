using System.Text.Json;

namespace mvdmio.TranslationTools.Tool.Pull;

internal sealed class TranslationSnapshotFileWriter
{
   private static readonly JsonSerializerOptions JsonSerializerOptions = new() {
      PropertyNamingPolicy = null,
      WriteIndented = true
   };

   public string Write(TranslationSnapshotFile snapshot)
   {
      return JsonSerializer.Serialize(snapshot, JsonSerializerOptions) + Environment.NewLine;
   }
}
