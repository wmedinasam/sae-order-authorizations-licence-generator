using WisLicense.Core;

namespace WisLicense.Cli;

public static class Program
{
    private const int Ok = 0;
    private const int ErrorEntrada = 1;
    private const int ErrorCriptoArchivo = 2;

    public static int Main(string[] args)
    {
        try
        {
            var a = new Argumentos(args);
            return a.Comando switch
            {
                "keygen" => Comandos.Keygen(a),
                "issue" => Comandos.Issue(a),
                "inspect" => Comandos.Inspect(a),
                "verify" => Comandos.Verify(a),
                "list" => Comandos.List(a),
                "appsecret-export" => Comandos.AppSecretExport(a),
                _ => Uso(),
            };
        }
        catch (ArgumentException e)
        {
            Console.Error.WriteLine($"Error de uso: {e.Message}");
            return ErrorEntrada;
        }
        catch (LicenciaException e) when (e.Codigo == CodigoError.Validacion)
        {
            Console.Error.WriteLine($"Entrada inválida: {e.Message}");
            return ErrorEntrada;
        }
        catch (LicenciaException e)
        {
            Console.Error.WriteLine($"{e.Codigo}: {e.Message}");
            return ErrorCriptoArchivo;
        }
        catch (Exception e) when (e is IOException or System.Security.Cryptography.CryptographicException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Error de cripto o archivo: {e.Message}");
            return ErrorCriptoArchivo;
        }
    }

    private static int Uso()
    {
        Console.WriteLine("""
            wis-license keygen  --out <dir>
            wis-license issue   --cliente "X" --installation-id <guid> --plan mensual|anual|lifetime
                                --edicion basica|pro|enterprise --max-empresas N --max-usuarios N
                                [--meses N | --anios N] [--desde YYYY-MM-DD] [--grace-days 7]
                                [--max-version-mayor 1] [--license-id <guid>]
                                --keys <dir> --out <archivo.lic> [--ledger licenses-issued.jsonl]
            wis-license inspect <archivo.lic> --keys <dir> --installation-id <guid>
            wis-license verify  <archivo.lic> --keys <dir> --installation-id <guid>
            wis-license list    --ledger <archivo>
            wis-license appsecret-export --keys <dir>   # imprime el appSecret en base64 (stdout)
            """);
        return ErrorEntrada;
    }
}
