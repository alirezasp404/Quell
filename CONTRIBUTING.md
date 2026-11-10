# Contributing to Quell

Thank you for your interest in contributing to **Quell**! Quell is a modern, cross-platform, high-performance database management tool and SQL IDE built with **Avalonia UI** and **.NET 10**.

Whether you are fixing a bug, implementing a new database dialect, adding UI enhancements, or improving documentation, your contributions are warmly welcomed.

---

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Prerequisites & Development Environment](#prerequisites--development-environment)
- [Getting Started](#getting-started)
- [Development Workflow](#development-workflow)
- [Verification & Automated Test Suite](#verification--automated-test-suite)
- [Coding Guidelines & Architectural Principles](#coding-guidelines--architectural-principles)
  - [1. MVVM & Separation of Concerns](#1-mvvm--separation-of-concerns)
  - [2. Semantic Theming & Dynamic Tokens](#2-semantic-theming--dynamic-tokens)
  - [3. Vector Graphics Only (No Emojis)](#3-vector-graphics-only-no-emojis)
  - [4. Responsive Layout & Window Controls](#4-responsive-layout--window-controls)
  - [5. Hardware Graphics & Motion Pipeline](#5-hardware-graphics--motion-pipeline)
  - [6. Thread Affinity & UI Dispatching](#6-thread-affinity--ui-dispatching)
- [Commit Conventions](#commit-conventions)
- [Submitting a Pull Request](#submitting-a-pull-request)
- [Reporting Issues](#reporting-issues)

---

## Code of Conduct

We are committed to providing a welcoming, friendly, and harassment-free experience for everyone. Please be respectful, constructive, and collaborative in all issues, pull requests, and discussions.

---

## Prerequisites & Development Environment

To build, debug, and run Quell locally, make sure you have installed:

- **[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)** (v10.0.x or later)
- **Git**
- An IDE or editor of your choice:
  - **JetBrains Rider** (Recommended for cross-platform Avalonia UI development)
  - **Visual Studio 2022** (v17.12+ with .NET 10 workload)
  - **Visual Studio Code** with the *C# Dev Kit* and *Avalonia for VS Code* extensions

---

## Getting Started

1. **Fork the Repository** on GitHub:
   Navigate to [https://github.com/cipherunits/Quell](https://github.com/cipherunits/Quell) and click **Fork**.

2. **Clone your fork locally**:
   ```bash
   git clone https://github.com/<your-username>/Quell.git
   cd Quell
   ```

3. **Check out the `dev` branch**:
   Active development and pull requests target the `dev` branch.
   ```bash
   git checkout dev
   ```

4. **Restore dependencies**:
   ```bash
   dotnet restore Quell.csproj
   ```

5. **Build the project**:
   ```bash
   dotnet build Quell.csproj --configuration Release
   ```

6. **Run Quell**:
   ```bash
   dotnet run --configuration Release
   ```
   *(To bypass the splash screen during rapid UI debugging, pass `--no-splash`: `dotnet run -- --no-splash`)*

---

## Development Workflow

1. **Create a topic branch**:
   Always branch off from `dev`:
   ```bash
   git checkout dev
   git pull origin dev
   git checkout -b feat/your-feature-name
   # or
   git checkout -b fix/issue-description
   ```

2. **Make your changes**:
   Keep your edits focused and modular. If you add or modify UI components, refer to [`COMPONENTS.md`](COMPONENTS.md) to adhere to the existing component taxonomy.

3. **Verify builds with zero warnings**:
   Ensure your code compiles cleanly without any compiler warnings or errors:
   ```bash
   dotnet build Quell.csproj --configuration Release
   ```

4. **Run the Automated Verification Suite**:
   Before committing, always run the full automated verification test suite:
   ```bash
   dotnet run --configuration Release --no-build -- --verify
   ```

---

## Verification & Automated Test Suite

Quell features a built-in headless layout and logic verification engine in `Program.cs`. It validates:

- Window chrome and layout dynamics (Sidebar toggles, Footer collapse, Star column expansion).
- Focus visual suppression, modal lifecycles, and dialog states.
- Hardware graphics adapter detection and DirectX 11 / ANGLE GPU pipelines.
- Multi-dialect SQLite migrations, table provisioning, and persistence.
- Results table batch cell editing, multi-row deletion, and column dropping.
- Foreign key detection and arrow navigation.
- Terminal session lifecycle and database CLI resolution (`psql`, `mysql`, `sqlcmd`, `sqlite3`).
- SemVer comparison, GitHub release detection, and status bar update triggers.

**Running the test suite:**
```bash
dotnet run --configuration Release --no-build -- --verify
```
All pull requests must achieve a **100% pass rate** on the verification suite.

---

## Coding Guidelines & Architectural Principles

### 1. MVVM & Separation of Concerns
- Follow the Model-View-ViewModel pattern using **CommunityToolkit.Mvvm** (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`).
- Keep Views/UserControls (`.axaml` and `.axaml.cs`) focused on visual presentation and user gestures.
- Encapsulate business logic, database queries, and async task orchestration in dedicated Services (`/Services`) or ViewModels (`/ViewModels`).

### 2. Semantic Theming & Dynamic Tokens
- **Never hardcode hex color values** (e.g., `#1E293B`, `#FFFFFF`) directly in UserControls or styles.
- Always use semantic resource bindings:
  ```xml
  Background="{DynamicResource AppBackgroundBrush}"
  Foreground="{DynamicResource AppTextPrimaryBrush}"
  BorderBrush="{DynamicResource AppBorderBrush}"
  ```
- All global theme tokens are defined in `Styles/ThemeTokens.axaml`, with specific values declared in `Styles/LightTheme.axaml` and `Styles/DarkTheme.axaml`.

### 3. Vector Graphics Only (No Emojis)
- Quell exclusively uses clean, professional vector icons. **Do not use Unicode emoji characters** (e.g., 💾, 🚀, ❌) in UI buttons, menus, or labels.
- Use `FluentAvalonia.UI.Controls.FASymbolIcon` (e.g., `<ui:FASymbolIcon Symbol="Play" />`) or native Avalonia `PathIcon` geometry.

### 4. Responsive Layout & Window Controls
- Maintain flexible Grid column definitions (`Auto, *, Auto, Auto`).
- **Never push native window controls off-screen**: Minimize, Maximize/Restore, and Close buttons must always remain pinned to the far right.
- Use adaptive breakpoints (`responsive-medium`, `responsive-compact`, `responsive-minimal`) to hide text labels and compact pills on narrower viewport widths while preserving icon actions and tooltips.

### 5. Hardware Graphics & Motion Pipeline
- Quell integrates hardware graphics detection via `HardwareGraphicsService`.
- Visual transitions should check or utilize `:is(TopLevel).gpu-mode` / `UserControl.gpu-mode` selectors so animations remain fluid and lightweight across both GPU-accelerated and CPU fallback rendering environments.

### 6. Thread Affinity & UI Dispatching
- Background operations (I/O, database queries, GitHub API calls) should execute on background threads via `ConfigureAwait(false)`.
- When updating observable properties or interacting with visual controls from asynchronous tasks, dispatch safely to the Avalonia UI thread:
  ```csharp
  Avalonia.Threading.Dispatcher.UIThread.Post(() =>
  {
      MyProperty = newValue;
  });
  ```

---

## Commit Conventions

We follow the [Conventional Commits](https://www.conventionalcommits.org/) specification:

- `feat:` A new feature (e.g., `feat(editor): add multi-cursor support`)
- `fix:` A bug fix (e.g., `fix(navbar): resolve responsive overflow on compact widths`)
- `docs:` Documentation changes (e.g., `docs: update COMPONENTS.md`)
- `style:` Formatting, whitespace, or visual styling updates
- `refactor:` Code restructuring without behavior changes
- `perf:` Performance improvements
- `test:` Adding or updating tests and verification routines
- `chore:` Maintenance, package updates, or CI/CD workflow updates

---

## Submitting a Pull Request

1. **Push your branch to your GitHub fork**:
   ```bash
   git push -u origin feat/your-feature-name
   ```
2. **Open a Pull Request**:
   - Target the **`dev`** branch as the base repository branch.
   - Provide a clear, descriptive title following Conventional Commits.
   - Describe the problem you are solving and summarize the changes made.
   - Attach screenshots or GIFs if your changes involve UI modifications.
3. **CI Checks**:
   - Ensure GitHub Actions CI/CD passes (`Run Verification & Integrity Suite` on Windows, Linux, and macOS).
   - Address any reviewer feedback promptly.

---

## Reporting Issues

If you encounter a bug or have a suggestion:

1. **Search existing issues** to prevent duplicates: [Quell Issues](https://github.com/cipherunits/Quell/issues).
2. **Provide detailed information**:
   - Operating System and architecture (e.g., Windows 11 x64, Ubuntu 24.04, macOS Sequoia M-series).
   - .NET SDK version (`dotnet --version`).
   - Clear steps to reproduce the issue.
   - Expected vs. actual behavior.
   - Relevant error logs, stack traces, or console outputs.

---

*Thank you for contributing to make Quell faster, more reliable, and a joy to use for database engineers everywhere!*
