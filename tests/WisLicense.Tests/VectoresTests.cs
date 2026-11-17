using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using WisLicense.Core;

namespace WisLicense.Tests;

/// <summary>Compatibilidad emisor/verificador: los mismos vectores los usa la suite de la app.</summary>
public class VectoresTests
{
    private static readonly string Dir = Path.Combine(AppContext.BaseDirectory, "testvectors");

    [Fact]
    public void Casos_de_expected_json_se_cumplen()
    {
        var esperado = JsonSerializer.Deserialize<Esperado>(File.ReadAllText(Path.Combine(Dir, "expected.json")))!;
        var iid = Guid.Parse(esperado.InstallationId);
        var ahora = DateTime.Parse(esperado.EvaluadoEn, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);

        var secreto = Convert.FromBase64String(File.ReadAllText(Path.Combine(Dir, "appsecret.b64")).Trim());
        using var pub = ECDsa.Create();
        pub.ImportFromPem(File.ReadAllText(Path.Combine(Dir, "public.pem")));

        foreach (var caso in esperado.Casos)
        {
            var lic = File.ReadAllBytes(Path.Combine(Dir, caso.Archivo));

            if (caso.Resultado == "Vigente")
            {
                var p = LectorLicencia.Verificar(lic, secreto, pub, iid, ahora);
                Assert.Equal(caso.Cliente, p.Cliente);
                Assert.Equal(caso.Plan, p.Plan);
                Assert.Equal(caso.Serial, p.Serial);
                Assert.Equal(caso.ValidTo is null ? null : DateTime.Parse(caso.ValidTo, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal), p.ValidTo);
            }
            else
            {
                var ex = Assert.Throws<LicenciaException>(() => LectorLicencia.Verificar(lic, secreto, pub, iid, ahora));
                Assert.Equal(Enum.Parse<CodigoError>(caso.Resultado), ex.Codigo);
            }
        }
    }

    private sealed record Esperado(
        [property: JsonPropertyName("installationId")] string InstallationId,
        [property: JsonPropertyName("evaluadoEn")] string EvaluadoEn,
        [property: JsonPropertyName("casos")] List<Caso> Casos);

    private sealed record Caso(
        [property: JsonPropertyName("archivo")] string Archivo,
        [property: JsonPropertyName("resultado")] string Resultado,
        [property: JsonPropertyName("cliente")] string? Cliente,
        [property: JsonPropertyName("plan")] string? Plan,
        [property: JsonPropertyName("serial")] int Serial,
        [property: JsonPropertyName("validTo")] string? ValidTo);
}
