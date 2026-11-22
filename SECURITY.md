# Security policy

## Reporting a vulnerability

Please do **not** open a public issue for a security problem. Report it
privately through GitHub instead: **Security > Report a vulnerability** on
this repository. You will get an answer within a week.

If the problem is in the sanctum protocol or the native libkyrka code
itself rather than in these .NET bindings, please also report it upstream
to [libkyrka](https://github.com/jorisvink/libkyrka).

## Supported versions

Only the latest released version receives security fixes.

## What the bindings do to stay safe

- Native packet buffers that held plaintext are wiped after every call.
  Key files are read into stack memory and wiped after loading.
- Spans over native memory are bounds-checked and valid only during the
  handler call.
- Handlers cannot call back into libkyrka, which is not re-entrant.
- The native library is only loaded from the application's own folders,
  never from the current directory or `PATH`.
- The native binaries are built with stack protection, FORTIFY_SOURCE,
  full RELRO, non-executable stacks, ASLR and (Linux) stack clash and
  control-flow protection. They export only libkyrka's public functions.
- libsodium is built from a pinned release verified by its SHA-256 hash.
- Releases are built from source in CI, published with nuget.org trusted
  publishing (no long-lived API key), and carry a signed build provenance
  attestation: `gh attestation verify <file.nupkg> --repo <owner/repo>`.
