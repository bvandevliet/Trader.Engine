# TraderEngine — Claude Code notes

## .NET solution / build
- Solution is `.slnx` (not `.sln`); use `dotnet sln <file>.slnx` / VS 2022 17.x+.
- Central Package Management is in effect: `Directory.Packages.props` (versions) + `Directory.Build.props` (shared properties: TargetFramework, Nullable, ImplicitUsings, RestorePackagesWithLockFile, and `IsTestProject`/`IsPackable` auto-set for any project named `*.Tests`).
- After changing `Directory.Packages.props` or any `.csproj`, regenerate lock files with `dotnet restore --force-evaluate`; verify with `dotnet restore --locked-mode`.
- Before assuming a `PackageReference` is redundant because a `ProjectReference`d project already has it, verify empirically (`dotnet restore` + `dotnet list package --include-transitive`) rather than reasoning from the reference graph alone — analyzer/source-generator packages (e.g. Riok.Mapperly) and anything under `PrivateAssets=all` never flow transitively, even without explicit blocking.
- Code style: explicit constructors, not C# primary constructors (verified: 13 vs 0 across the codebase) — don't apply IDE "use primary constructor" suggestions.

## Docker Compose / Visual Studio Container Tools
- `docker-compose.dcproj` (`Microsoft.Docker.Sdk`) can be added to `TraderEngine.slnx` safely — `dotnet build`/`test`/`restore` silently skip it (SDK only resolves inside VS), so it won't break CI or CLI-driven builds.
- VS Fast Mode debugging only builds a Dockerfile's *first* stage, then bind-mounts the host build output over it — a single-stage Dockerfile that `COPY`s a prebuilt `bin/Release/.../publish` folder will NOT work for one-click F5 (needs a manual `dotnet publish` first). Use a self-contained multi-stage Dockerfile (base/build/publish/final, SDK builds from source) for real one-click debugging; keep the prod/CI Dockerfile separate/untouched if CI depends on its current shape.
- `--env-file` is a root-level Compose CLI flag only (`docker compose --env-file X up`, not `up --env-file X`) — can't be smuggled in via `DockerComposeUpArguments`/`DockerComposeBuildArguments` (those append after the subcommand).
- `DockerComposeEnvFilePath` (singular, documented) takes exactly one file. `DockerComposeEnvFilePaths` (plural, semicolon-delimited) is real and shipped but undocumented on Microsoft Learn — confirmed by decompiling `...MSBuild\Sdks\Microsoft.Docker.Sdk\tools\Microsoft.Docker*.dll` (`strings` + grep) and validating live via `MSBuild.exe docker-compose.dcproj -t:DockerGetComposeEnvFilePaths -getTargetResult:DockerGetComposeEnvFilePaths`. Use it to layer `.env.example;.env` (later file wins) instead of pre-merging files.
- A service's `env_file:` YAML key injects raw vars into the *container* and never participates in `${VAR}` interpolation when Compose parses the YAML — the two mechanisms don't interact, don't conflate them when `environment:` blocks rename/compose vars (e.g. `CMC_API_KEY` → `CoinMarketCap__API_KEY`).
- MSBuild.exe is at `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe`; in Git Bash use `-t:Target` not `/t:Target` (the `/` gets mangled into a path).
- VS live-syncs `.slnx`/`.dcproj`/`Directory.Packages.props`/`.csproj` while open in the IDE — expect concurrent external edits mid-session; re-read a file before editing it rather than assuming your last-known copy is current.

## PowerShell 5.1 (this machine's default)
- `Set-Content -Encoding utf8NoBOM` doesn't exist (PS 6+ only). Use `[System.IO.File]::WriteAllLines($path, $lines, (New-Object System.Text.UTF8Encoding $false))` instead.
- `Get-Content` without `-Encoding UTF8` reads via the system codepage, mangling non-ASCII characters (e.g. em dashes become `â€”`) — always pass `-Encoding UTF8` explicitly when round-tripping repo files.
