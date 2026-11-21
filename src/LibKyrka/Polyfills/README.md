# Polyfills

Small stand-ins for APIs and compiler support types that exist in modern
.NET but not in .NET Standard 2.0. This folder is only compiled for the
`netstandard2.0` target (see `LibKyrka.csproj`), so the rest of the code
can be written once against the modern API surface.

Everything here is `internal`. When .NET Standard support is dropped,
delete this folder.
