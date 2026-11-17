namespace WisLicense.Cli;

/// <summary>Parser mínimo: un comando, argumentos posicionales y pares --clave valor.</summary>
public sealed class Argumentos
{
    private readonly Dictionary<string, string> _opciones = new(StringComparer.OrdinalIgnoreCase);

    public string Comando { get; }
    public List<string> Posicionales { get; } = [];

    public Argumentos(string[] args)
    {
        Comando = args.Length > 0 ? args[0] : "";
        for (var i = 1; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--")) { Posicionales.Add(args[i]); continue; }

            var clave = args[i][2..];
            if (i + 1 >= args.Length || args[i + 1].StartsWith("--"))
                throw new ArgumentException($"La opción --{clave} requiere un valor.");
            _opciones[clave] = args[++i];
        }
    }

    public bool Tiene(string clave) => _opciones.ContainsKey(clave);

    public string? Opcion(string clave) => _opciones.GetValueOrDefault(clave);

    public string Requerida(string clave) =>
        Opcion(clave) ?? throw new ArgumentException($"Falta la opción --{clave}.");

    public int? Entero(string clave) =>
        Opcion(clave) is { } v
            ? int.TryParse(v, out var n) ? n : throw new ArgumentException($"--{clave} debe ser un entero.")
            : null;

    public Guid? Guid(string clave) =>
        Opcion(clave) is { } v
            ? System.Guid.TryParse(v, out var g) ? g : throw new ArgumentException($"--{clave} debe ser un GUID.")
            : null;
}
