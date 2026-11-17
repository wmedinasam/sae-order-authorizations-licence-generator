using System.Security.Cryptography;

namespace WisLicense.Core;

public static class ClaveAes
{
    /// <summary>aesKey = HKDF-SHA256(appSecret, salt = installationId en RFC 4122, info = "WISLIC1-aes", 32 bytes).</summary>
    public static byte[] Derivar(ReadOnlySpan<byte> appSecret, Guid installationId)
    {
        if (appSecret.Length != Formato.AppSecretLen)
            throw new LicenciaException(CodigoError.FormatoInvalido, "appSecret debe medir 32 bytes.");

        Span<byte> salt = stackalloc byte[16];
        installationId.TryWriteBytes(salt, Formato.GuidBigEndian, out _);

        var ikm = appSecret.ToArray();
        try
        {
            return HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, Formato.AesKeyLen, salt.ToArray(), Formato.InfoHkdf);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ikm);
        }
    }
}
