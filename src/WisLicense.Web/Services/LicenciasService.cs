using WisLicense.Core;
using WisLicense.Storage;

namespace WisLicense.Web.Services;

/// <summary>Fachada que usan las páginas. Rutas desde appsettings; la contraseña nunca se guarda.</summary>
public sealed class LicenciasService(IConfiguration config)
{
    public string DirectorioClaves => config["Licencias:DirectorioClaves"] ?? "keys";
    public string RutaLedger => config["Licencias:Ledger"] ?? "licenses-issued.jsonl";

    public (byte[] Lic, LicenciaPayload Payload) Emitir(SolicitudEmision solicitud, string contrasena)
    {
        var resultado = Emisor.Emitir(solicitud, DirectorioClaves, contrasena, RutaLedger);
        Emisor.Registrar(resultado.Payload, RutaLedger);
        return resultado;
    }

    public (LicenciaPayload Payload, EstadoVigencia Estado) Verificar(byte[] lic, Guid installationId, string contrasena)
    {
        var ahora = Emisor.AhoraUtc();
        var payload = Verificador.Abrir(lic, DirectorioClaves, contrasena, installationId);
        return (payload, Vigencia.Evaluar(payload, ahora));
    }

    public IReadOnlyList<EntradaLedger> Registro() => Ledger.Leer(RutaLedger);

    public EstadoClaves Estado() => AlmacenClaves.Estado(DirectorioClaves);

    public void GenerarClaves(string contrasena) => AlmacenClaves.Generar(DirectorioClaves, contrasena);
}
