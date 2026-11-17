using System.Text;

namespace WisLicense.Core;

/// <summary>Contrato binario del .lic (plan §4). Cambiar cualquier valor exige subir VersionFormato en la app.</summary>
public static class Formato
{
    public const string Magic = "WISLIC1";
    public const byte VersionFormato = 0x01;

    public const int MagicLen = 7;
    public const int NonceLen = 12;
    public const int LongitudLen = 4;
    public const int TagLen = 16;
    public const int FirmaLen = 64;          // P-256 IEEE P1363: r || s

    /// <summary>Cabecera = magic + versión + nonce. Es el AAD de GCM y el prefijo del archivo.</summary>
    public const int CabeceraLen = MagicLen + 1 + NonceLen;   // 20

    public const int OffsetLongitud = CabeceraLen;            // 20
    public const int OffsetCiphertext = OffsetLongitud + LongitudLen; // 24

    public const int AesKeyLen = 32;
    public const int AppSecretLen = 32;

    public static readonly byte[] MagicBytes = Encoding.ASCII.GetBytes(Magic);
    public static readonly byte[] InfoHkdf = Encoding.ASCII.GetBytes("WISLIC1-aes");

    /// <summary>
    /// Orden de bytes del GUID usado como salt de HKDF: RFC 4122 (big-endian en todos los campos).
    /// Guid.ToByteArray() de .NET es little-endian en los 3 primeros campos: no usarlo para esto.
    /// Cubierto por los test vectors.
    /// </summary>
    public const bool GuidBigEndian = true;
}
