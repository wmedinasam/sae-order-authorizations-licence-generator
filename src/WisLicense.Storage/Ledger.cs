using System.Text.Json;
using System.Text.Json.Serialization;

namespace WisLicense.Storage;

/// <summary>Línea del ledger (plan §6): sin clave ni contenido cifrado.</summary>
public sealed record EntradaLedger(
    [property: JsonPropertyName("licenseId")] Guid LicenseId,
    [property: JsonPropertyName("serial")] int Serial,
    [property: JsonPropertyName("cliente")] string Cliente,
    [property: JsonPropertyName("plan")] string Plan,
    [property: JsonPropertyName("validTo")] DateTime? ValidTo,
    [property: JsonPropertyName("emitida")] DateTime Emitida);

/// <summary>Registro append-only de emisiones en JSON Lines.</summary>
public static class Ledger
{
    public static void Agregar(string ruta, EntradaLedger entrada)
    {
        var linea = JsonSerializer.Serialize(entrada);
        File.AppendAllText(ruta, linea + Environment.NewLine);
    }

    public static IReadOnlyList<EntradaLedger> Leer(string ruta)
    {
        if (!File.Exists(ruta)) return [];

        return File.ReadLines(ruta)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => JsonSerializer.Deserialize<EntradaLedger>(l)!)
            .ToList();
    }
}
