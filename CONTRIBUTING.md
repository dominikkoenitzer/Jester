# Contributing to Jester

Thanks for your interest in improving Jester! This guide gets you from clone to pull request.

## Getting started

**Prerequisites:** [.NET 9 SDK](https://dotnet.microsoft.com/download) on Windows 10/11.

```sh
git clone https://github.com/dominikkoenitzer/Jester.git
cd Jester
dotnet build          # compile
dotnet run            # launch the app
```

## Project layout

See the [Project structure](README.md#-project-structure) table in the README. In short:

- UI lives in the `*.xaml` files; logic in their `*.xaml.cs` partners.
- The look is centralized in **`Theme.xaml`** — change colors/styles there, not per-control.
- Custom window chrome is in **`ThemedWindow.cs`**; commands in **`JesterCommands.cs`**.

## Coding style

- Formatting is enforced by [`.editorconfig`](.editorconfig). Run `dotnet format` before committing.
- Match the surrounding code: 4-space indentation, `PascalCase` for members, `_camelCase` for private fields.
- Keep methods small and intention-revealing; prefer clarity over cleverness.
- The build is warning-clean — please keep it that way.

## Making a change

1. **Fork** the repo and create a branch: `git checkout -b feature/short-description`.
2. Make your change, keeping commits focused. Write clear commit messages.
3. Verify it builds and runs: `dotnet build -c Release` and a quick manual smoke test.
4. Update [`CHANGELOG.md`](CHANGELOG.md) under **Unreleased** if it's user-facing.
5. Open a **pull request** against `main` and fill in the template.

## Reporting bugs & ideas

Use the [issue templates](../../issues/new/choose). For bugs, include your Windows version,
steps to reproduce, and what you expected versus what happened.

## Be respectful

Please keep all interactions — issues, pull requests, and discussions — kind, respectful, and constructive. Be welcoming to newcomers and assume good faith.

## License

By contributing, you agree that your contributions will be licensed under the
[GNU GPL v3.0](LICENSE), the same license as the project.
