using System.Security.Cryptography;
using WisLicense.Core;

namespace WisLicense.Tests;

/// <summary>Claves efímeras por test: nunca se usan claves reales ni fijas aquí.</summary>
public sealed class Ayudas : IDisposable
{
    public ECDsa ClavePrivada { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    public byte[] AppSecret { get; } = RandomNumberGenerator.GetBytes(Formato.AppSecretLen);
    public Guid InstallationId { get; } = Guid.NewGuid();

    public static readonly DateTime Desde = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

    public LicenciaPayload Mensual(DateTime? validTo = null) => new()
    {
        LicenseId = Guid.NewGuid(),
        Serial = 1,
        Cliente = "Cliente Prueba SL",
        InstallationId = InstallationId,
        Plan = Planes.Mensual,
        Edicion = "pro",
        MaxEmpresas = 5,
        MaxUsuarios = 25,
        Emitida = Desde,
        ValidFrom = Desde,
        ValidTo = validTo ?? Desde.AddMonths(1),
        GraceDays = 7,
        MaxVersionMayor = null,
    };

    public LicenciaPayload Lifetime() => new()
    {
        LicenseId = Guid.NewGuid(),
        Serial = 1,
        Cliente = "Cliente Lifetime SL",
        InstallationId = InstallationId,
        Plan = Planes.Lifetime,
        Edicion = "enterprise",
        MaxEmpresas = 50,
        MaxUsuarios = 500,
        Emitida = Desde,
        ValidFrom = Desde,
        ValidTo = null,
        GraceDays = 0,
        MaxVersionMayor = 1,
    };

    public byte[] Emitir(LicenciaPayload p) => Empaquetador.Empaquetar(p, AppSecret, ClavePrivada);

    public ECDsa ClavePublica()
    {
        var pub = ECDsa.Create();
        pub.ImportSubjectPublicKeyInfo(ClavePrivada.ExportSubjectPublicKeyInfo(), out _);
        return pub;
    }

    public void Dispose()
    {
        ClavePrivada.Dispose();
        CryptographicOperations.ZeroMemory(AppSecret);
    }
}
