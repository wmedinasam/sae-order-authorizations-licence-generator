using System.Security.Cryptography;
using WisLicense.Core;

namespace WisLicense.Storage;

/// <summary>Datos de una emisión tal como los captura el operador (CLI o web).</summary>
public sealed record SolicitudEmision(
    string Cliente,
    Guid InstallationId,
    string Plan,
    string Edicion,
    int MaxEmpresas,
    int MaxUsuarios,
    DateTime Desde,
    int? Meses = null,
    int? Anios = null,
    int? GraceDays = null,
    int? MaxVersionMayor = null,
    Guid? LicenseId = null);

/// <summary>Emisión de licencias: arma el payload (serial, renovación, vigencia) y firma el .lic.</summary>
public static class Emisor
{
    public const int GraceDaysPorDefecto = 7;

    public static (byte[] Lic, LicenciaPayload Payload) Emitir(SolicitudEmision s, string dirClaves, string contrasena, string rutaLedger)
    {
        var payload = ConstruirPayload(s, Ledger.Leer(rutaLedger), AhoraUtc());

        using var clavePrivada = AlmacenClaves.CargarPrivada(dirClaves, contrasena);
        var appSecret = AlmacenClaves.CargarAppSecret(dirClaves, contrasena);
        try
        {
            return (Empaquetador.Empaquetar(payload, appSecret, clavePrivada), payload);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(appSecret);
        }
    }

    /// <summary>Se llama después de escribir el .lic: el ledger nunca registra una licencia que no llegó a existir.</summary>
    public static void Registrar(LicenciaPayload p, string rutaLedger) =>
        Ledger.Agregar(rutaLedger, new EntradaLedger(p.LicenseId, p.Serial, p.Cliente, p.Plan, p.ValidTo, p.Emitida));

    public static LicenciaPayload ConstruirPayload(SolicitudEmision s, IReadOnlyList<EntradaLedger> ledger, DateTime ahora)
    {
        // Renovar: mismo licenseId, serial = último emitido + 1.
        var licenseId = s.LicenseId ?? Guid.NewGuid();
        var historial = ledger.Where(e => e.LicenseId == licenseId).ToList();
        if (s.LicenseId is not null && historial.Count == 0)
            throw new LicenciaException(CodigoError.Validacion, $"licenseId {licenseId} no existe en el ledger; no hay nada que renovar.");
        var serial = historial.Count == 0 ? 1 : historial.Max(e => e.Serial) + 1;

        var lifetime = s.Plan == Planes.Lifetime;
        DateTime? validTo = lifetime ? null
            : s.Anios is { } anios ? s.Desde.AddYears(anios)
            : s.Meses is { } meses ? s.Desde.AddMonths(meses)
            : throw new LicenciaException(CodigoError.Validacion, "mensual/anual requiere meses o años.");

        return new LicenciaPayload
        {
            LicenseId = licenseId,
            Serial = serial,
            Cliente = s.Cliente,
            InstallationId = s.InstallationId,
            Plan = s.Plan,
            Edicion = s.Edicion,
            MaxEmpresas = s.MaxEmpresas,
            MaxUsuarios = s.MaxUsuarios,
            Emitida = ahora,
            ValidFrom = s.Desde,
            ValidTo = validTo,
            GraceDays = lifetime ? 0 : s.GraceDays ?? GraceDaysPorDefecto,
            MaxVersionMayor = s.MaxVersionMayor,
        };
    }

    /// <summary>Las fechas se escriben a segundos, como en el ejemplo del plan §5.</summary>
    public static DateTime AhoraUtc()
    {
        var ahora = DateTime.UtcNow;
        return new DateTime(ahora.Ticks - ahora.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
    }
}
