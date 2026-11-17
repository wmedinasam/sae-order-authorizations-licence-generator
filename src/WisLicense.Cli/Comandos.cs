using WisLicense.Core;
using WisLicense.Storage;

namespace WisLicense.Cli;

public static class Comandos
{
    private const string VariableContrasena = "WIS_LICENSE_KEY_PASSWORD";

    public static int Keygen(Argumentos a)
    {
        var dir = a.Requerida("out");
        AlmacenClaves.Generar(dir, Contrasena(confirmar: true));
        Console.WriteLine($"Claves creadas en {Path.GetFullPath(dir)}. Respalda private.pem y la contraseña.");
        return 0;
    }

    public static int Issue(Argumentos a)
    {
        var rutaLic = a.Requerida("out");
        var rutaLedger = a.Opcion("ledger") ?? "licenses-issued.jsonl";
        var dirClaves = a.Requerida("keys");

        var solicitud = new SolicitudEmision(
            Cliente: a.Requerida("cliente"),
            InstallationId: a.Guid("installation-id") ?? throw new ArgumentException("Falta la opción --installation-id."),
            Plan: a.Requerida("plan"),
            Edicion: a.Requerida("edicion"),
            MaxEmpresas: a.Entero("max-empresas") ?? throw new ArgumentException("Falta la opción --max-empresas."),
            MaxUsuarios: a.Entero("max-usuarios") ?? throw new ArgumentException("Falta la opción --max-usuarios."),
            Desde: a.Opcion("desde") is { } d
                ? DateTime.SpecifyKind(DateOnly.Parse(d).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
                : DateTime.UtcNow.Date,
            Meses: a.Entero("meses"),
            Anios: a.Entero("anios"),
            GraceDays: a.Entero("grace-days"),
            MaxVersionMayor: a.Entero("max-version-mayor"),
            LicenseId: a.Guid("license-id"));

        var (lic, payload) = Emisor.Emitir(solicitud, dirClaves, Contrasena(), rutaLedger);
        File.WriteAllBytes(rutaLic, lic);
        Emisor.Registrar(payload, rutaLedger);

        Console.WriteLine($"Licencia {payload.LicenseId} serial {payload.Serial} emitida en {rutaLic}.");
        return 0;
    }

    public static int Inspect(Argumentos a)
    {
        var (payload, _) = Abrir(a, verificar: false);
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    public static int Verify(Argumentos a)
    {
        var (payload, estado) = Abrir(a, verificar: true);
        Console.WriteLine($"Válida: {payload.Cliente}, plan {payload.Plan}, estado {estado}.");
        return 0;
    }

    // Solo el base64 va a stdout, para encadenarlo con user-secrets sin dejar el secreto en un archivo.
    public static int AppSecretExport(Argumentos a)
    {
        var dir = a.Requerida("keys");
        Console.Out.Write(AlmacenClaves.ExportarAppSecretBase64(dir, Contrasena()));
        Console.Error.WriteLine("appSecret exportado a stdout. No lo guardes en archivos del repo.");
        return 0;
    }

    public static int List(Argumentos a)
    {
        var entradas = Ledger.Leer(a.Requerida("ledger"));
        if (entradas.Count == 0) { Console.WriteLine("Sin emisiones registradas."); return 0; }

        Console.WriteLine($"{"emitida",-17} {"serial",-6} {"plan",-9} {"validTo",-11} cliente");
        foreach (var e in entradas.OrderBy(e => e.Emitida))
        {
            var emitida = e.Emitida.ToString("yyyy-MM-dd HH:mm");
            var validTo = e.ValidTo?.ToString("yyyy-MM-dd") ?? "—";
            Console.WriteLine($"{emitida,-17} {e.Serial,-6} {e.Plan,-9} {validTo,-11} {e.Cliente}");
        }
        return 0;
    }

    private static (LicenciaPayload Payload, EstadoVigencia Estado) Abrir(Argumentos a, bool verificar)
    {
        if (a.Posicionales.Count != 1) throw new ArgumentException("Indica un único archivo .lic.");

        var installationId = a.Guid("installation-id") ?? throw new ArgumentException("Falta la opción --installation-id.");
        var lic = File.ReadAllBytes(a.Posicionales[0]);
        var dirClaves = a.Requerida("keys");
        var contrasena = Contrasena();

        if (!verificar)
            return (Verificador.Abrir(lic, dirClaves, contrasena, installationId), EstadoVigencia.Vigente);

        var ahora = Emisor.AhoraUtc();
        var payload = Verificador.Verificar(lic, dirClaves, contrasena, installationId, ahora);
        return (payload, Vigencia.Evaluar(payload, ahora));
    }

    // Contraseña: variable de entorno (CI) o prompt sin eco.
    private static string Contrasena(bool confirmar = false)
    {
        var env = Environment.GetEnvironmentVariable(VariableContrasena);
        if (!string.IsNullOrEmpty(env)) return env;

        var primera = LeerOculta("Contraseña de las claves: ");
        if (!confirmar) return primera;

        if (primera != LeerOculta("Repite la contraseña: "))
            throw new ArgumentException("Las contraseñas no coinciden.");
        return primera;
    }

    private static string LeerOculta(string prompt)
    {
        Console.Write(prompt);
        var buffer = new List<char>();
        while (true)
        {
            var k = Console.ReadKey(intercept: true);
            if (k.Key == ConsoleKey.Enter) break;
            if (k.Key == ConsoleKey.Backspace && buffer.Count > 0) buffer.RemoveAt(buffer.Count - 1);
            else if (!char.IsControl(k.KeyChar)) buffer.Add(k.KeyChar);
        }
        Console.WriteLine();
        return new string(buffer.ToArray());
    }
}
