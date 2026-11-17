# Plan: `wis-license` — CLI de emisión de licencias (repo privado)

> Idioma: español. Documento autocontenido: no requiere acceso al repo de la app.

## 1. Contexto
La app **SAE Order Authorizations** (Blazor .NET 10, on-prem, una instancia por cliente) se vende por suscripción mensual, anual o lifetime. No hay servidor de licencias: el cliente sube un archivo `.lic` en su app. Este CLI **emite** esos archivos. La app solo **verifica**; nunca puede generar licencias porque solo tiene la clave pública.

Este repo es **privado**. Contiene (o manipula) la clave privada: nunca se publica ni se sube a la app.

## 2. Alcance
Dentro: generar par de claves, emitir `.lic`, inspeccionar/verificar un `.lic`, registro local de emisiones, vectores de prueba compartidos con la app.
Fuera: servidor/API, cobro, UI web, revocación en línea, base de datos. (Posible fase 2, mismo formato.)

## 3. Stack
- .NET 10 consola (`net10.0`), C#, **sin paquetes NuGet de cripto**: solo `System.Security.Cryptography` (`ECDsa`, `AesGcm`, `HKDF`, `RandomNumberGenerator`).
- CLI parsing: `System.CommandLine` (única dependencia permitida) o manual con `args`.
- Pruebas: xUnit.
- Estructura: `src/WisLicense.Core` (formato + cripto, **sin I/O**), `src/WisLicense.Cli`, `tests/WisLicense.Tests`.
- Razón de separar `Core`: la misma lógica de verificación/descifrado se copia o referencia como paquete en la app para garantizar compatibilidad.

## 4. Contrato del archivo `.lic` (CRÍTICO: debe coincidir byte a byte con la app)
Binario, big-endian en enteros:

```
offset  tamaño  campo
0       7       magic = ASCII "WISLIC1"
7       1       versionFormato = 0x01
8       12      nonce (aleatorio, único por licencia)
20      4       longCifrado (uint32) = N
24      N       ciphertext   (AES-256-GCM del payload JSON UTF-8)
24+N    16      tag GCM
40+N    64      firma ECDSA P-256/SHA-256, formato IEEE P1363 (r||s), sobre los bytes [0 .. 40+N)
```
- **AAD** de GCM = bytes `[0 .. 20)` (magic + versión + nonce). Evita reutilizar el cifrado con otro encabezado.
- **Firma sobre todo lo anterior** (incluye ciphertext y tag): verificar primero la firma, **luego** descifrar.
- Archivo opaco: sin texto legible. Extensión `.lic`.

### Derivación de la clave AES
`aesKey = HKDF-SHA256(ikm = appSecret (32 bytes), salt = installationId (16 bytes, GUID en `ToByteArray()` orden RFC4122 big-endian), info = ASCII "WISLIC1-aes", longitud 32)`
- `appSecret`: 32 bytes aleatorios generados una sola vez por `keygen`. Se guarda en el archivo de claves del emisor y se **inyecta en la compilación de la app** (variable de entorno de CI). No va en texto plano en ningún repo.
- Consecuencia: un `.lic` solo descifra en la instalación cuyo `installationId` se usó al emitir.
- Fijar el orden de bytes del GUID en una constante documentada y cubrirlo con el test vector (evita el error clásico de endianness de `Guid.ToByteArray()` de .NET).

## 5. Payload JSON (antes de cifrar)
```json
{
  "formato": 1,
  "licenseId": "5b0d…(GUID)",
  "serial": 3,
  "cliente": "Razón social",
  "installationId": "GUID de la instalación",
  "plan": "mensual | anual | lifetime",
  "edicion": "basica | pro | enterprise",
  "maxEmpresas": 5,
  "maxUsuarios": 25,
  "emitida": "2026-11-17T00:00:00Z",
  "validFrom": "2026-11-17T00:00:00Z",
  "validTo": "2026-12-17T00:00:00Z",
  "graceDays": 7,
  "maxVersionMayor": 1
}
```
Reglas:
- `plan=lifetime` ⇒ `validTo = null` y `graceDays = 0`; `maxVersionMayor` obligatorio (la licencia cubre hasta esa versión mayor, p. ej. 1.x).
- `mensual`/`anual` ⇒ `validTo` obligatorio y > `validFrom`; `maxVersionMayor` opcional (null = sin tope).
- Fechas en UTC ISO-8601. Serializar con `System.Text.Json`, propiedades en **orden fijo**, sin indentar, para que la emisión sea determinista salvo nonce.
- `serial`: entero creciente por `licenseId`. La app rechaza un `.lic` con `serial` menor al ya instalado (evita volver a una licencia vieja). Renovar = mismo `licenseId`, `serial + 1`.

## 6. Comandos del CLI
```
wis-license keygen  --out <dir>
wis-license issue   --cliente "X" --installation-id <guid> --plan mensual|anual|lifetime
                    --edicion basica|pro|enterprise --max-empresas N --max-usuarios N
                    [--meses N | --anios N] [--desde YYYY-MM-DD] [--grace-days 7]
                    [--max-version-mayor 1] [--license-id <guid>]   # --license-id = renovar
                    --keys <dir> --out <archivo.lic>
wis-license inspect <archivo.lic> --keys <dir> --installation-id <guid>   # descifra y muestra JSON
wis-license verify  <archivo.lic> --keys <dir> --installation-id <guid>   # solo valida firma + vigencia
wis-license list    --ledger <archivo>                                    # emisiones registradas
```
- `keygen`: crea `private.pem` (PKCS#8 cifrado con contraseña, `ExportEncryptedPkcs8PrivateKey`, PBKDF2 ≥ 600 000 iteraciones), `public.pem` (se copia a la app), `appsecret.b64` (cifrado igual o en el mismo archivo protegido). Pide contraseña por consola (sin eco) o variable `WIS_LICENSE_KEY_PASSWORD`. **Se niega a sobrescribir** claves existentes.
- `issue`: valida reglas de §5, arma payload, cifra, firma, escribe `.lic` y **añade una línea al ledger** (`licenses-issued.jsonl`: licenseId, serial, cliente, plan, validTo, fecha emisión; **sin** la clave ni el contenido cifrado).
- Códigos de salida: 0 ok, 1 validación de entrada, 2 error de cripto/archivo.

## 7. Seguridad (obligatorio)
1. `.gitignore`: `*.pem`, `appsecret*`, `*.lic`, `keys/`, `ledger` si tiene datos de clientes. Agregarlo **antes** del primer commit.
2. La clave privada nunca se loguea ni se imprime; usar `CryptographicOperations.ZeroMemory` sobre buffers de claves.
3. No hay claves de ejemplo reales en tests: los tests generan pares efímeros; los vectores fijos usan claves de prueba marcadas `TEST ONLY`.
4. Documentar backup de `private.pem` + contraseña (si se pierde, no se pueden emitir licencias compatibles: habría que rotar clave pública en la app y reemitir todas).
5. Honestidad: el cifrado oculta el contenido; lo que impide falsificar es la firma. Un atacante con acceso al binario de la app podría extraer `appSecret`, pero **no** puede firmar licencias nuevas.

## 8. Pasos de implementación (cada uno con verificación)
1. [ ] Crear solución (`Core`, `Cli`, `Tests`), `.gitignore` seguro. Verif: `dotnet build` verde.
2. [ ] `Core`: modelo `LicenciaPayload`, validador de reglas §5. Verif: tests de reglas (lifetime sin `validTo`, mensual con `validTo`<`validFrom`, etc.).
3. [ ] `Core`: derivación HKDF + cifrado AES-GCM + empaquetado binario §4. Verif: roundtrip descifra igual.
4. [ ] `Core`: firma ECDSA P-256 y verificación; `Verificar(bytes, publicKey, installationId)` devuelve payload o error tipado (`FirmaInvalida`, `FormatoInvalido`, `InstalacionDistinta`, `NoVigente`). Verif: test de un byte alterado en cada región (encabezado, ciphertext, tag, firma) ⇒ siempre falla.
5. [ ] CLI `keygen` con clave privada protegida por contraseña. Verif: no sobrescribe; `public.pem` importa en `ECDsa.ImportFromPem`.
6. [ ] CLI `issue` + ledger. Verif: genera `.lic`, `verify` lo acepta; `--license-id` incrementa `serial`.
7. [ ] CLI `inspect`, `verify`, `list`.
8. [ ] **Vectores de prueba**: carpeta `testvectors/` con `public.pem` de prueba, `appsecret` de prueba, `installationId` fijo y 3 `.lic` (válida mensual, válida lifetime, alterada) + `expected.json`. Se **copian al repo de la app** para su suite xUnit; garantizan compatibilidad entre emisor y verificador.
9. [ ] `README.md`: flujo operativo (cliente envía `installationId` → `issue` → entregas `.lic` → cliente lo sube en página "Licencia").
10. [ ] Verif final: `dotnet test` verde; emitir licencia real de prueba y cargarla en la app de desarrollo.

## 9. Criterios de aceptación
- `.lic` emitido: ilegible con un editor de texto/hex (sin cadenas reconocibles aparte del magic), modificar cualquier byte ⇒ `verify` falla.
- Mismo `.lic` con otro `installationId` ⇒ `InstalacionDistinta`/fallo de descifrado.
- Lifetime nunca expira; mensual/anual expiran en `validTo`, con `graceDays` respetados.
- Sin dependencias de cripto externas; compila en Linux y Windows.

## 10. Compatibilidad con la app (referencia)
La app (`Services/LicenciaService.cs`, repo `sae_order_authorizations`) implementa el **mismo** §4/§5: verifica firma con `public.pem` embebida, descifra con el `appSecret` inyectado en build y el `installationId` guardado en BD, aplica vigencia/gracia/solo lectura y límites por edición. Cualquier cambio de formato incrementa `versionFormato` en ambos lados.

## 11. Preguntas abiertas (decidir con el dueño)
- Precios y límites exactos por edición.
- `graceDays` por defecto (propuesto 7).
- Si se quiere revocación en línea (requiere API; fuera de alcance aquí).
