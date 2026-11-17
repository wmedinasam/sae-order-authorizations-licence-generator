using WisLicense.Core;
using WisLicense.Storage;
using Xunit;

namespace WisLicense.Tests;

public class AppSecretExportTests
{
    private const string Contrasena = "contraseña-de-prueba-larga";

    [Fact]
    public void ExportaLosMismos32BytesQueUsaElEmisor()
    {
        var dir = NuevoDirectorio();
        try
        {
            AlmacenClaves.Generar(dir, Contrasena);

            var exportado = AlmacenClaves.ExportarAppSecretBase64(dir, Contrasena);
            var bytes = Convert.FromBase64String(exportado);

            Assert.Equal(Formato.AppSecretLen, bytes.Length);
            Assert.Equal(AlmacenClaves.CargarAppSecret(dir, Contrasena), bytes);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ContrasenaIncorrectaNoExporta()
    {
        var dir = NuevoDirectorio();
        try
        {
            AlmacenClaves.Generar(dir, Contrasena);

            Assert.Throws<LicenciaException>(() => AlmacenClaves.ExportarAppSecretBase64(dir, "otra-contraseña-incorrecta"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string NuevoDirectorio() =>
        Path.Combine(Path.GetTempPath(), "wis-appsecret-" + Guid.NewGuid().ToString("N"));
}
