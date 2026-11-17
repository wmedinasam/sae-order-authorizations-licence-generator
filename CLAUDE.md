# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Current state

The .NET 10 solution `WisLicense.slnx` exists and implements the plan: `Core`, `Storage`, `Cli`, `Web` (Blazor, loopback only) and `Tests`. `PLAN-CLI-LICENCIAS.md` is still the design source of truth (§8 lists the steps). `README.md` has the operational flow.

## Before the first commit

Create `.gitignore` first (plan §7.1): `*.pem`, `appsecret*`, `*.lic`, `keys/`, and the ledger if it holds client data. This repo handles the private signing key, so nothing gets committed before that file exists.

## Commands

```
dotnet build WisLicense.slnx
dotnet test
dotnet test --filter "FullyQualifiedName~LicenciaTests.Roundtrip"   # single test
dotnet run --project src/WisLicense.Web      # http://127.0.0.1:5190, keys from appsettings
```

The web UI reads `Licencias:DirectorioClaves` and `Licencias:Ledger`. Override them with `Licencias__DirectorioClaves` / `Licencias__Ledger` when testing, so no real keys get touched.

## Architecture

- **Trust split.** The CLI holds the private key (ECDSA P-256) and `appSecret`. The licensed app (separate repo, `sae_order_authorizations`) holds only `public.pem` and `appSecret` injected at build time. The app verifies licenses and never issues them, so changes to the format must keep that split.
- **Layout.** `src/WisLicense.Core` holds format and crypto with no I/O. `src/WisLicense.Cli` holds the commands. `tests/WisLicense.Tests` is xUnit. Core is shared with the app so both sides run the same verification and decryption code.
- **`.lic` format (plan §4).** Magic `WISLIC1`, format version, 12-byte nonce, uint32 ciphertext length, AES-256-GCM ciphertext and tag, then an ECDSA signature over everything before it. The AAD is the first 20 bytes (magic, version, nonce). Verify the signature first, then decrypt. Any change to the layout bumps `versionFormato` on both sides.
- **Key derivation.** `aesKey = HKDF-SHA256(appSecret, salt = installationId, info = "WISLIC1-aes")`. A license only decrypts on the installation it was issued for. The GUID byte order is a documented constant covered by the test vectors (the classic `Guid.ToByteArray()` endianness trap). Do not change it.
- **Payload rules (plan §5).** `lifetime` has `validTo = null` and `graceDays = 0`, and requires `maxVersionMayor`. `mensual` and `anual` require `validTo > validFrom`. Serialize with fixed property order and no indentation so issuing is deterministic apart from the nonce.
- **Renewal.** Same `licenseId` with `serial + 1`. The app rejects any `.lic` whose serial is lower than the installed one.
- **Test vectors.** The `testvectors/` folder (plan step 8) is copied into the app's test suite and is the compatibility contract between issuer and verifier. Regenerating it changes the contract. Its keys are marked `TEST ONLY`. Other tests use ephemeral key pairs.
- **Exit codes.** 0 ok, 1 input validation, 2 crypto or file error.
- **Key handling.** Never log or print the private key. Zero key buffers with `CryptographicOperations.ZeroMemory`. The CLI refuses to overwrite existing keys.

## Conventions

- Keep the Spanish domain vocabulary (`cliente`, `licencia`, `plan`, `edicion`, `maxEmpresas`) in JSON fields and CLI flags. The app reads these names.
- The only allowed dependency is `System.CommandLine`, and only if it is used. No crypto NuGet packages.
