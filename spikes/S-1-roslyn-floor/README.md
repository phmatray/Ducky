# S-1: Roslyn floor (week-1 prototype, SPEC §23)

Question: which `Microsoft.CodeAnalysis.CSharp` ships with SDK 10.0.100, the generator floor (R-PKG-3, ADR-0003)?

Answer: `5.0.0-2.25523.111` (`sdk/10.0.100/Roslyn/bincore/csc.deps.json`), so the pin is `5.0.0` in
`Directory.Packages.props`, and `.github/renovate.json` never updates it.

Check: `global.json` here pins exactly 10.0.100 (`rollForward: disable`). `Generator` references the pin;
`Consumer` loads it as an analyzer and uses the generated type.

    cd spikes/S-1-roslyn-floor && dotnet run --project Consumer   # prints "S-1 generator ran: yes"

With `VersionOverride="5.3.0"` on the generator's reference the same command fails with
`CS9057: Analyzer assembly '…/Generator.dll' cannot be used because it references version '5.3.0.0' of the
compiler, which is newer than the currently running version '5.0.0.0'`, which is why the pin must never move.

Both results were observed on 2026-09-30 with SDK 10.0.100 installed side by side, not system-wide
(`dotnet-install.sh --version 10.0.100 --install-dir <dir>`, then `DOTNET_ROOT=<dir> PATH=<dir>:$PATH` for the
command only).

Outside every solution and gate (SPEC §18).
