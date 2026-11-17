using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace WisLicense.Core;

/// <summary>Verifica y abre un .lic. Orden fijo: formato → firma → descifrado → coincidencia de instalación.</summary>
public static class LectorLicencia
{
    /// <summary>Abre el payload sin evaluar vigencia (lo usa inspect).</summary>
    public static LicenciaPayload Abrir(ReadOnlySpan<byte> lic, ReadOnlySpan<byte> appSecret, ECDsa clavePublica, Guid installationId)
    {
        var minimo = Formato.OffsetCiphertext + Formato.TagLen + Formato.FirmaLen;
        if (lic.Length < minimo)
            throw new LicenciaException(CodigoError.FormatoInvalido, "El archivo es demasiado corto.");

        if (!lic[..Formato.MagicLen].SequenceEqual(Formato.MagicBytes) || lic[Formato.MagicLen] != Formato.VersionFormato)
            throw new LicenciaException(CodigoError.FormatoInvalido, "Cabecera o versión de formato no reconocida.");

        var n = BinaryPrimitives.ReadUInt32BigEndian(lic.Slice(Formato.OffsetLongitud, Formato.LongitudLen));
        if (lic.Length != minimo + n)
            throw new LicenciaException(CodigoError.FormatoInvalido, "La longitud del archivo no coincide con su cabecera.");

        var cuerpoLen = lic.Length - Formato.FirmaLen;
        var cuerpo = lic[..cuerpoLen];
        var firma = lic[cuerpoLen..];

        if (!clavePublica.VerifyData(cuerpo, firma, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
            throw new LicenciaException(CodigoError.FirmaInvalida, "La firma no coincide con la clave pública.");

        var nonce = lic.Slice(Formato.MagicLen + 1, Formato.NonceLen);
        var cabecera = lic[..Formato.CabeceraLen];
        var ciphertext = lic.Slice(Formato.OffsetCiphertext, (int)n);
        var tag = lic.Slice(Formato.OffsetCiphertext + (int)n, Formato.TagLen);

        var aesKey = ClaveAes.Derivar(appSecret, installationId);
        var plano = new byte[ciphertext.Length];
        try
        {
            using var gcm = new AesGcm(aesKey, Formato.TagLen);
            gcm.Decrypt(nonce, ciphertext, tag, plano, cabecera);
        }
        catch (AuthenticationTagMismatchException)
        {
            // Firma válida + tag inválido = la clave AES no es la de esta instalación.
            throw new LicenciaException(CodigoError.InstalacionDistinta, "La licencia no corresponde a esta instalación.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(aesKey);
        }

        var payload = JsonSerializer.Deserialize<LicenciaPayload>(plano)
            ?? throw new LicenciaException(CodigoError.FormatoInvalido, "Payload vacío.");
        CryptographicOperations.ZeroMemory(plano);

        if (payload.InstallationId != installationId)
            throw new LicenciaException(CodigoError.InstalacionDistinta, "La licencia no corresponde a esta instalación.");

        return payload;
    }

    /// <summary>Abre y además exige vigencia (incluida la gracia). Lo usa verify y la app.</summary>
    public static LicenciaPayload Verificar(ReadOnlySpan<byte> lic, ReadOnlySpan<byte> appSecret, ECDsa clavePublica, Guid installationId, DateTime ahoraUtc)
    {
        var payload = Abrir(lic, appSecret, clavePublica, installationId);
        if (Vigencia.Evaluar(payload, ahoraUtc) == EstadoVigencia.Expirada)
            throw new LicenciaException(CodigoError.NoVigente, "La licencia expiró y terminó su periodo de gracia.");
        return payload;
    }
}
