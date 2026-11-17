using System.Text.Json.Serialization;

namespace WisLicense.Core;

/// <summary>Payload JSON cifrado dentro del .lic (plan §5). El orden de declaración es el orden de serialización.</summary>
public sealed record LicenciaPayload
{
    [JsonPropertyName("formato")] public int Formato { get; init; } = 1;
    [JsonPropertyName("licenseId")] public Guid LicenseId { get; init; }
    [JsonPropertyName("serial")] public int Serial { get; init; }
    [JsonPropertyName("cliente")] public string Cliente { get; init; } = "";
    [JsonPropertyName("installationId")] public Guid InstallationId { get; init; }
    [JsonPropertyName("plan")] public string Plan { get; init; } = "";
    [JsonPropertyName("edicion")] public string Edicion { get; init; } = "";
    [JsonPropertyName("maxEmpresas")] public int MaxEmpresas { get; init; }
    [JsonPropertyName("maxUsuarios")] public int MaxUsuarios { get; init; }
    [JsonPropertyName("emitida")] public DateTime Emitida { get; init; }
    [JsonPropertyName("validFrom")] public DateTime ValidFrom { get; init; }
    [JsonPropertyName("validTo")] public DateTime? ValidTo { get; init; }
    [JsonPropertyName("graceDays")] public int GraceDays { get; init; }
    [JsonPropertyName("maxVersionMayor")] public int? MaxVersionMayor { get; init; }
}

public static class Planes
{
    public const string Mensual = "mensual";
    public const string Anual = "anual";
    public const string Lifetime = "lifetime";
    public static readonly string[] Todos = [Mensual, Anual, Lifetime];
}

public static class Ediciones
{
    public static readonly string[] Todas = ["basica", "pro", "enterprise"];
}
