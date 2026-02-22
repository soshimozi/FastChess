# FastChess

FastChess is a performance-oriented C# chess engine core using bitboards and magic-bitboard sliding attacks.

## What is in this repo

- `FastChess.Library`: engine primitives and core logic (`BoardState`, `Move`, move generation, FEN, Zobrist, etc.)
- `FastChess.Console`: simple console app for quick local experimentation
- `FastChess.Magic`: utility project to regenerate magic attack tables
- `FastChess.Tests`: xUnit tests for core behavior

## Requirements

- .NET 8 SDK

## Build and test

```bash
dotnet build FastChess.sln
dotnet test FastChess.sln
```

## Unity plugin usage

`FastChess.Library` is multi-targeted and produces both:
- `net8.0`
- `netstandard2.1`

For Unity, use the `netstandard2.1` build output:

- `FastChess.Library/bin/Debug/netstandard2.1/FastChess.Library.dll`
- or `FastChess.Library/bin/Release/netstandard2.1/FastChess.Library.dll`

Do not copy the `net8.0` DLL into Unity `Assets/Plugins`; that can trigger startup load/type-cache errors.

## Run the console sample

```bash
dotnet run --project FastChess.Console/FastChess.Console.csproj
```

## Regenerate magic tables

Run the generator:

```bash
dotnet run --project FastChess.Magic/FastChess.Magic.csproj
```

This writes a `MagicData.cs` file in the current working directory. Replace `FastChess.Library/MagicData.cs` with the generated file when you want to update embedded magic tables.

## Notes

- The library is currently focused on correctness + speed-friendly structure (immutable board updates, bitboard APIs, legal move generation).
- `FastChess.Tests` should be kept green while refactoring core move logic.

## Roadmap

- Improve move-generation coverage with targeted tests (castling, en passant edge cases, pins/check evasions, promotions).
- Add benchmarking for move generation and make-move throughput.
- Introduce search components (iterative deepening, alpha-beta, move ordering, transposition table).
- Add UCI protocol support for engine integration with GUIs.
- Improve evaluation (material + piece-square tables, mobility, king safety, endgame tuning).

## Contributing

1. Create a branch from `main`.
2. Make focused changes with clear commit messages.
3. Run tests before opening a PR:
   - `dotnet test FastChess.sln`
4. If you modify magic generation logic:
   - run `dotnet run --project FastChess.Magic/FastChess.Magic.csproj`
   - update `FastChess.Library/MagicData.cs` with the generated output
   - re-run tests
5. Open a PR with:
   - summary of what changed
   - why it changed
   - test evidence (command output or brief summary)
