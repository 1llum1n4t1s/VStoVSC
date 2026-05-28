using System.Text.Json.Serialization;

namespace VStoVSC.Util;

/// <summary>
/// JSON Source Generator 用のコンテキストクラス
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified)]
[JsonSerializable(typeof(Settings))]
internal partial class AppJsonContext : JsonSerializerContext
{
}
