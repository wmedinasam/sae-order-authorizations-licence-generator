# wis-license

Emisor de licencias `.lic` para SAE Order Authorizations. Repo privado: contiene el flujo para firmar licencias con la clave privada. Diseño completo en `PLAN-CLI-LICENCIAS.md`.

## Estructura

| Proyecto | Función |
|---|---|
| `src/WisLicense.Core` | Formato `.lic`, cripto y reglas. Sin I/O. Lo comparte la app. |
| `src/WisLicense.Storage` | Claves en disco, ledger JSONL, emisión y verificación. |
| `src/WisLicense.Cli` | Comandos `keygen`, `issue`, `inspect`, `verify`, `list`. |
| `src/WisLicense.Web` | Interfaz Blazor en `http://127.0.0.1:5190`. Solo loopback. |
| `tests/WisLicense.Tests` | Pruebas xUnit y vectores de compatibilidad. |
| `testvectors/` | Claves y licencias de **prueba** (`TEST ONLY`) que usa la suite de la app. |

## Requisitos

- .NET 10 SDK.

## Comandos

```
dotnet build WisLicense.slnx
dotnet test
dotnet test --filter "FullyQualifiedName~LicenciaTests.Roundtrip"   # una prueba
```

## Flujo operativo

1. **Crear claves (una vez).** Respalda `private.pem` y su contraseña fuera de este equipo. Sin ellos no se pueden emitir licencias compatibles con las ya entregadas.
   ```
   wis-license keygen --out keys/
   ```
   `keygen` se niega a sobrescribir claves existentes.
2. **Recibir el `installationId`** del cliente desde su página Licencia.
3. **Emitir.** Desde la CLI o desde la web (`Emitir`):
   ```
   wis-license issue --cliente "Razón social" --installation-id <guid> --plan mensual \
     --edicion pro --max-empresas 5 --max-usuarios 25 --meses 1 \
     --keys keys/ --out licencia.lic
   ```
   Cada emisión añade una línea a `licenses-issued.jsonl`. Renovar = mismo `--license-id`, serial + 1.
4. **Entregar** el `.lic`. El cliente lo sube en su página Licencia.
5. **Comprobar** con `wis-license verify licencia.lic --keys keys/ --installation-id <guid>` o desde `Verificar`.

La contraseña se pide por consola sin eco, o se toma de `WIS_LICENSE_KEY_PASSWORD` (solo para CI).

## Configuración de la web

`src/WisLicense.Web/appsettings.json`:

- `Licencias:DirectorioClaves`: directorio de `private.pem`, `public.pem` y `appsecret.b64`.
- `Licencias:Ledger`: ruta de `licenses-issued.jsonl`.

Ambas se pueden sobrescribir con variables de entorno `Licencias__DirectorioClaves` y `Licencias__Ledger`.

La contraseña nunca se guarda: se pide en cada operación y se borra del formulario al terminar.

## Seguridad

- `.gitignore` excluye `*.pem`, `appsecret*`, `*.lic`, `keys/` y el ledger. `testvectors/` es la única excepción, a propósito.
- La web escucha solo en `127.0.0.1`. No tiene autenticación: no la expongas en red.
- Una licencia no se puede falsificar sin `private.pem`. El `appSecret` solo sirve para descifrar; el archivo se protege con la firma (ver plan §7.5).

## Vectores de compatibilidad

`testvectors/` contiene `public.pem`, `appsecret.b64`, tres `.lic` (dos válidos y uno alterado) y `expected.json`. La prueba `VectoresTests` los verifica. La suite de la app debe usar los mismos archivos. Si cambia el formato, hay que regenerarlos y subir `versionFormato` en ambos lados.
