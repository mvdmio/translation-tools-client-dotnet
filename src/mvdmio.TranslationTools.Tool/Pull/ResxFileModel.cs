namespace mvdmio.TranslationTools.Tool.Pull;

internal sealed class ResxFileModel
{
   public required string FilePath { get; init; }
   public required IReadOnlyCollection<ResxDataEntryModel> Entries { get; init; }
}

internal sealed class ResxDataEntryModel
{
   public required string Key { get; init; }
   public string? Value { get; init; }
   public string? Comment { get; init; }
}
