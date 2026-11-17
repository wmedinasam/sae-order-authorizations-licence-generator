using System.Security.Cryptography;
using System.Text.Json;

namespace WisLicense.Core;

/// <summary>Emite el .lic completo: cifra el payload, arma el binario y firma todo lo anterior (plan §4).</summary>
public static class Empaquetador
{
    public static byte[] Empaquetar(LicenciaPayload payload, ReadOnlySpan<byte> appSecret, ECDsa clavePrivada)
    {
        var errores = ReglasLicencia.Validar(payload);
        if (errores.Count > 0)
            throw new LicenciaException(CodigoError.Validacion, string.Join(" ", errores));

        var json = JsonSerializer.SerializeToUtf8Bytes(payload);
        var aesKey = ClaveAes.Derivar(appSecret, payload.InstallationId);
        var nonce = RandomNumberGenerator.GetBytes(Formato.NonceLen);

        var cabecera = new byte[Formato.CabeceraLen];
        Formato.MagicBytes.CopyTo(cabecera, 0);
        cabecera[Formato.MagicLen] = Formato.VersionFormato;
        nonce.CopyTo(cabecera, Formato.MagicLen + 1);

        var ciphertext = new byte[json.Length];
        var tag = new byte[Formato.TagLen];
        using (var gcm = new AesGcm(aesKey, Formato.TagLen))
            gcm.Encrypt(nonce, json, ciphertext, tag, cabecera);

        var cuerpo = new byte[Formato.OffsetCiphertext + ciphertext.Length + Formato.TagLen];
        cabecera.CopyTo(cuerpo, 0);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(cuerpo.AsSpan(Formato.OffsetLongitud), (uint)ciphertext.Length);
        ciphertext.CopyTo(cuerpo, Formato.OffsetCiphertext);
        tag.CopyTo(cuerpo, Formato.OffsetCiphertext + ciphertext.Length);

        var firma = clavePrivada.SignData(cuerpo, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        CryptographicOperations.ZeroMemory(aesKey);
        CryptographicOperations.ZeroMemory(json);

        return [.. cuerpo, .. firma];
    }
}
