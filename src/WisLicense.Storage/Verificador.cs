using System.Security.Cryptography;
using WisLicense.Core;

namespace WisLicense.Storage;

/// <summary>Abre un .lic cargando las claves del directorio. Libera el appSecret al terminar.</summary>
public static class Verificador
{
    /// <summary>Firma y descifrado, sin vigencia (inspect).</summary>
    public static LicenciaPayload Abrir(byte[] lic, string dirClaves, string contrasena, Guid installationId) =>
        Con(lic, dirClaves, contrasena, (secreto, pub) => LectorLicencia.Abrir(lic, secreto, pub, installationId));

    /// <summary>Firma, descifrado y vigencia (verify y la app).</summary>
    public static LicenciaPayload Verificar(byte[] lic, string dirClaves, string contrasena, Guid installationId, DateTime ahora) =>
        Con(lic, dirClaves, contrasena, (secreto, pub) => LectorLicencia.Verificar(lic, secreto, pub, installationId, ahora));

    private static LicenciaPayload Con(byte[] lic, string dirClaves, string contrasena, Func<byte[], ECDsa, LicenciaPayload> operacion)
    {
        using var pub = AlmacenClaves.CargarPublica(dirClaves);
        var secreto = AlmacenClaves.CargarAppSecret(dirClaves, contrasena);
        try
        {
            return operacion(secreto, pub);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secreto);
        }
    }
}
