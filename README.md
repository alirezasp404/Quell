# Fdb Studio

Modern, high-performance, cross-platform database management tool and SQL IDE built with **Avalonia UI** and **.NET 10**.

---

## Features

- **Cross-Platform**: Runs natively on Windows (`win-x64`), Linux (`linux-x64`), and macOS (`osx-x64` & `osx-arm64`).
- **Modern UI & Theming**: Light, Dark, and System Default themes with fully decoupled semantic design tokens (`ThemeTokens.axaml`, `LightTheme.axaml`, `DarkTheme.axaml`).
- **Dynamic Syntax Coloring**: User-configurable SQL keyword syntax colors (Blue, Purple, Emerald Green, Amber Gold, Rose Red, Cyan) with automatic WCAG contrast preservation.
- **Embedded SQLite Local Engine**: Powered by **Dapper** and `Microsoft.Data.Sqlite` for local state persistence, query history, saved connections, and dynamic multi-dialect keywords.
- **Unified Splash Screen Pre-loading**: Frameless Splash Screen executing real SQLite table provisioning, schema migrations, and batch pre-caching of themes, colors, panel/table customizations, shortcuts, saved connections, and dialect keywords for instant application launch.
- **IntelliSense & Autocomplete**: Floating contextual overlay (`SqlIntelliSenseOverlayComponent`) for dialect-aware keyword matching and template code snippets.
- **Excel-Grade Grid**: `QueryResultTableComponent` featuring native virtualization, sortable columns, row metrics, search filtering, and pagination.
- **Zoom & Shortcuts**: Native `Ctrl + Plus`, `Ctrl + Minus`, and `Ctrl + 0` font scaling inside the text editor.
- **Vector Graphics Only**: Exclusively uses Fluent UI vector icons (`FASymbolIcon` and `PathIcon`), completely emoji-free.

---

## Project Structure & Components

The application is decomposed into isolated, testable Avalonia components located in `/Components`:

- `MainMenuBarComponent`: Application menu bar with server connection status indicator.
- `MainToolbarComponent`: SQL execution actions, query management, and connection buttons.
- `EditorComponent`: AvaloniaEdit hosting with custom line numbers, zoom handlers, and overlay.
- `EditorTabStripComponent`: Tab headers with close buttons, context menus, and new tab controls.
- `SqlIntelliSenseOverlayComponent`: Dialect keyword completion popup.
- `QueryResultTableComponent`: Result grid composed with dedicated toolbar and pagination components.
- `TableToolbarComponent`: Search query filter, refresh, execution timing badge, and CSV export.
- `TablePaginationComponent`: Pagination navigation and row selection summary.
- `SidebarComponent`: Database object explorer with empty states and schemas.
- `SplashScreenCardComponent`: Startup card with artwork and live SQLite initialization progress.
- `SettingsModalComponent`: Appearance and editor settings dialog with theme switcher and color pickers.
- `ThemeSelectionCardsComponent`: Interactive theme selection cards.
- `SyntaxColorSelectorComponent`: Keyword syntax palette swatches with live previews.
- `LiveSyntaxPreviewComponent`: Real-time mini code snippet preview.

For detailed component documentation, refer to [COMPONENTS.md](COMPONENTS.md).

---

## Building and Running

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Run Application
```bash
dotnet run
```

### Run Full Verification Suite
```bash
dotnet run -- --verify
```

---

## Cross-Platform Packaging & CI/CD

Fdb Studio includes an automated GitHub Actions workflow (`.github/workflows/ci-cd.yml`) that builds, packages, and attaches native installers whenever code is pushed to branch `main`, on pull requests to `main`, or via manual workflow dispatch.

### Supported Installer Formats
| Platform | Target Runtime | Installer Format | Portable Package | Packaging Tool |
| :--- | :--- | :--- | :--- | :--- |
| **Windows** | `win-x64` | `Fdb-Studio-Setup-win-x64.exe` | `.zip` | Inno Setup (`packaging/windows/installer.iss`) |
| **Linux** | `linux-x64` | `fdb-studio_1.0.0_amd64.deb` | `.tar.gz` | `dpkg-deb` (`packaging/linux/build-deb.sh`) |
| **macOS** | `osx-x64` | `Fdb-Studio-1.0.0-osx-x64.dmg` | `.zip` | `hdiutil` (`packaging/macos/build-app.sh`) |
| **macOS** | `osx-arm64` | `Fdb-Studio-1.0.0-osx-arm64.dmg` | `.zip` | `hdiutil` (`packaging/macos/build-app.sh`) |
