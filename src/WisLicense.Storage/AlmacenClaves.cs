using System.Security.Cryptography;
using WisLicense.Core;

namespace WisLicense.Storage;

/// <summary>Claves del emisor en disco. Solo I/O: la criptografía de formato vive en Core.</summary>
public sealed record EstadoClaves(bool Privada, bool Publica, bool AppSecret)
{
    public bool Completo => Privada && Publica && AppSecret;
    public bool Ninguna => !Privada && !Publica && !AppSecret;
}

public static class AlmacenClaves
{
    public const string ArchivoPrivada = "private.pem";
    public const string ArchivoPublica = "public.pem";
    public const string ArchivoAppSecret = "appsecret.b64";

    // Plan §6: PBKDF2 ≥ 600 000 iteraciones.
    public const int IteracionesPbkdf2 = 600_000;

    private static readonly PbeParameters Pbe = new(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, IteracionesPbkdf2);

    /// <summary>Genera par ECDSA P-256 y appSecret. Se niega a sobrescribir cualquier archivo existente.</summary>
    public static void Generar(string directorio, string contrasena)
    {
        var rutas = new[] { ArchivoPrivada, ArchivoPublica, ArchivoAppSecret }.Select(n => Path.Combine(directorio, n)).ToArray();
        if (rutas.Any(File.Exists))
            throw new LicenciaException(CodigoError.Validacion, $"Ya existen claves en '{directorio}'. No se sobrescriben.");

        Directory.CreateDirectory(directorio);

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        File.WriteAllText(rutas[0], ecdsa.ExportEncryptedPkcs8PrivateKeyPem(contrasena, Pbe));
        File.WriteAllText(rutas[1], ecdsa.ExportSubjectPublicKeyInfoPem());

        var appSecret = RandomNumberGenerator.GetBytes(Formato.AppSecretLen);
        try
        {
            File.WriteAllText(rutas[2], Convert.ToBase64String(CifrarAppSecret(appSecret, contrasena)));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(appSecret);
        }
    }

    public static EstadoClaves Estado(string directorio) => new(
        File.Exists(Path.Combine(directorio, ArchivoPrivada)),
        File.Exists(Path.Combine(directorio, ArchivoPublica)),
        File.Exists(Path.Combine(directorio, ArchivoAppSecret)));

    public static ECDsa CargarPrivada(string directorio, string contrasena)
    {
        var ecdsa = ECDsa.Create();
        try
        {
            ecdsa.ImportFromEncryptedPem(File.ReadAllText(Path.Combine(directorio, ArchivoPrivada)), contrasena);
            return ecdsa;
        }
        catch
        {
            ecdsa.Dispose();
            throw;
        }
    }

    public static ECDsa CargarPublica(string directorio)
    {
        var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(File.ReadAllText(Path.Combine(directorio, ArchivoPublica)));
        return ecdsa;
    }

    /// <summary>Devuelve los 32 bytes del appSecret. El llamador debe limpiarlos con ZeroMemory al terminar.</summary>
    public static byte[] CargarAppSecret(string directorio, string contrasena)
    {
        var bytes = Convert.FromBase64String(File.ReadAllText(Path.Combine(directorio, ArchivoAppSecret)).Trim());
        return DescifrarAppSecret(bytes, contrasena);
    }

    /// <summary>appSecret en base64 para la app (dotnet user-secrets "Licencia:AppSecret"). Limpia el buffer en claro.</summary>
    public static string ExportarAppSecretBase64(string directorio, string contrasena)
    {
        var secreto = CargarAppSecret(directorio, contrasena);
        try
        {
            return Convert.ToBase64String(secreto);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secreto);
        }
    }

    // Formato del archivo: salt(16) | nonce(12) | tag(16) | ciphertext(32), todo en base64.
    private static byte[] CifrarAppSecret(byte[] appSecret, string contrasena)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var nonce = RandomNumberGenerator.GetBytes(Formato.NonceLen);
        var clave = Rfc2898DeriveBytes.Pbkdf2(contrasena, salt, IteracionesPbkdf2, HashAlgorithmName.SHA256, 32);
        var cifrado = new byte[appSecret.Length];
        var tag = new byte[Formato.TagLen];
        try
        {
            using var gcm = new AesGcm(clave, Formato.TagLen);
            gcm.Encrypt(nonce, appSecret, cifrado, tag);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clave);
        }
        return [.. salt, .. nonce, .. tag, .. cifrado];
    }

    private static byte[] DescifrarAppSecret(byte[] datos, string contrasena)
    {
        if (datos.Length != 16 + Formato.NonceLen + Formato.TagLen + Formato.AppSecretLen)
            throw new LicenciaException(CodigoError.FormatoInvalido, "appsecret con longitud inesperada.");

        var salt = datos[..16];
        var nonce = datos[16..(16 + Formato.NonceLen)];
        var tag = datos[(16 + Formato.NonceLen)..(16 + Formato.NonceLen + Formato.TagLen)];
        var cifrado = datos[(16 + Formato.NonceLen + Formato.TagLen)..];

        var clave = Rfc2898DeriveBytes.Pbkdf2(contrasena, salt, IteracionesPbkdf2, HashAlgorithmName.SHA256, 32);
        var plano = new byte[cifrado.Length];
        try
        {
            using var gcm = new AesGcm(clave, Formato.TagLen);
            gcm.Decrypt(nonce, cifrado, tag, plano);
            return plano;
        }
        catch (AuthenticationTagMismatchException)
        {
            throw new LicenciaException(CodigoError.FormatoInvalido, "Contraseña incorrecta o appsecret dañado.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clave);
        }
    }
}
