---
name: fashionpos-wpf-builder
description: >
  Builds a complete, production-ready, offline-first Point of Sale (POS) desktop application
  for fashion/clothing businesses using C# WPF (.NET 8), SQLite, and the MVVM pattern.
  Use this skill whenever the user asks to build, scaffold, generate, continue, or fix any
  part of the FashionPOS system — including the database, services, ViewModels, XAML views,
  import integrations (Excel, CSV, Shopify), invoice generation, or reports. Also trigger when
  the user mentions WPF POS, clothing inventory system, bazar mode, cashier screen, or
  fashion store management software.
---

# FashionPOS WPF Builder — Agent Execution Guide

You are building a **real, production-ready, offline-first** POS desktop application.
This is not a demo. Every file you create must compile, run, and be usable in a real shop.
Business name: Gerber Weeza.

---

## 📋 BUILD STATUS — PHASES COMPLETE ✅

**Build Date:** 2026 | Current Status: **PROJECT COMPLETE** 

### Phase Completion Summary

| Phase | Component | Status | Build Verified |
|-------|-----------|--------|-----------------|
| 1 | Project Scaffold | ✅ Complete | ✅ SUCCESS |
| 2 | Models Layer (13 classes) | ✅ Complete | ✅ SUCCESS |
| 3 | Data Layer (DatabaseContext, Initializer) | ✅ Complete | ✅ SUCCESS |
| 4 | Services Layer (8 services, 100+ methods) | ✅ Complete | ✅ SUCCESS |
| 5 | Helpers & Converters | ✅ Complete | ✅ SUCCESS |
| 6 | ViewModels Layer (8 ViewModels) | ✅ Complete | ✅ SUCCESS |
| 7 | Views & Styling (XAML, LoginWindow, MainWindow) | ✅ Complete | ✅ SUCCESS |
| 8 | App Wiring (Dependency Injection, Startup) | ✅ Complete | ✅ SUCCESS |

### Latest Build Output
```
FashionPOS -> bin\Debug\net8.0-windows\FashionPOS.dll
Build succeeded.
0 Warning(s)
0 Error(s)
```

**Build Errors:** 0  
**Build Warnings:** 0  
**Status:** ✅ **READY FOR TESTING**

### Post-Build Fixes Applied
- ✅ **Isolated Event Mode:** Implemented "EV-" prefix for event sales and dashboard filtering. Sales during active events are isolated until the event ends.
- ✅ **Authentication Stabilization:** Default users (`admin`/`admin123`, `gerbera`/`123`) are now hard-seeded in `DatabaseInitializer.cs`.
- ✅ **Password Binding Fix:** LoginWindow uses a Click event handler to pass PasswordBox content safely to the ViewModel.
- ✅ **UI Refresh:** Main shell uses a monochrome (#000000 / #F5F5F5) high-contrast theme focused on speed.

### File Inventory

**Database Layer (Complete - 2 files, ~350 lines)**
- `Data/DatabaseContext.cs` — SQLite connection with WAL mode, 9 tables schema
- `Data/DatabaseInitializer.cs` — Create tables, 4 indexes, seed defaults

**Models Layer (Complete - 1 file, ~400 lines)**
- `Models/Models.cs` — 13 entity classes (Product, User, Sale, SaleItem, CartItem, BazarEvent, etc.)

**Services Layer (Complete - 3 files, ~1200 lines)**
- `Services/CoreServices.cs` — AuthService, ProductService, UserService
- `Services/SalesAndEventServices.cs` — SaleService, EventService (Updated with isolation logic)
- `Services/ImportAndReportServices.cs` — ImportService, InvoiceService, ReportService

**Helpers & Converters (Complete - 2 files, ~200 lines)**
- `Helpers/RelayCommand.cs` — ICommand implementation
- `Converters/AllConverters.cs` — 5 converters (BoolToVisibility, StringToVisibility, etc.)

**ViewModels Layer (Complete - 3 files, ~1500 lines)**
- `ViewModels/BaseViewModel.cs` — INotifyPropertyChanged base class
- `ViewModels/CoreViewModels.cs` — LoginViewModel, MainViewModel, DashboardViewModel
- `ViewModels/FeatureViewModels.cs` — POSViewModel, InventoryViewModel, ReportsViewModel, UsersViewModel, EventViewModel

**Views & Styling (Complete - 18 files, ~2000 lines XAML/C#)**
- `Resources/Styles.xaml` — Gerber Weeza Design System
- `App.xaml` + `App.xaml.cs` — Wiring and DI

---

## 0. Pre-Flight: Read Before Writing a Single Line

1. Check `available_skills` — load any referenced sub-skills before proceeding.
2. Identify the user's current state:
   - **From scratch** → Execute all phases in order (1 → 8).
   - **Partial build** → Detect which files exist, skip completed phases, resume from the gap.
   - **Fix / extend** → Read existing files first with `view`, then patch with `str_replace`.
3. Never overwrite a file without reading it first.
4. Run `dotnet build` after every phase to catch compile errors immediately.

---

## 1. Project Scaffold

### 1.1 Create Solution and Project
```bash
mkdir -p ~/FashionPOS && cd ~/FashionPOS
dotnet new sln -n FashionPOS
dotnet new wpf -n FashionPOS -f net8.0-windows
dotnet sln add FashionPOS/FashionPOS.csproj
cd FashionPOS
```

### 1.2 Edit FashionPOS.csproj — Replace contents
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
    <AssemblyName>FashionPOS</AssemblyName>
    <RootNamespace>FashionPOS</RootNamespace>
    <Platforms>x64</Platforms>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Data.Sqlite"        Version="8.0.0" />
    <PackageReference Include="Dapper"                       Version="2.1.35" />
    <PackageReference Include="ClosedXML"                    Version="0.102.2" />
    <PackageReference Include="iTextSharp.LGPLv2.Core"       Version="3.4.2" />
    <PackageReference Include="ShopifySharp"                 Version="6.9.0" />
    <PackageReference Include="BCrypt.Net-Next"              Version="4.0.3" />
  </ItemGroup>
</Project>
```

---

## 2. Models Layer

Create `Models/Models.cs`. Namespace `FashionPOS.Models`.

| Class | Key Properties |
|---|---|
| `Product` | Id, Name, SKU, CategoryId, Color, CostPrice, SellingPrice, StockQuantity, LowStockThreshold |
| `User` | Id, Username, Role (Owner/Manager/Cashier), Permission Booleans |
| `Sale` | SaleNumber, UserId, EventId, TotalAmount, Profit, PaymentMethod |
| `BazarEvent` | Name, Location, StartedAt, EndedAt, IsActive |

---

## 3. Data Layer

### 3.1 DatabaseContext (`Data/DatabaseContext.cs`)
- Store DB at: `%LOCALAPPDATA%\FashionPOS\fashionpos.db`
- Enable WAL mode in `CreateConnection()`.

### 3.2 DatabaseInitializer (`Data/DatabaseInitializer.cs`)
- Execute schema creation.
- Seed default users: `admin`/`admin123` and `gerbera`/`123`.

---

## 4. Services Layer

### 4.1 AuthService
- Handles Login/Logout and session state.

### 4.2 ProductService
- Product CRUD and stock management.

### 4.3 SaleService
- `CompleteSale`: Handles "EV-" prefix for active events.
- `GetDashboardStats`: Excludes sales from active events.

### 4.4 UserService
- User management and password hashing.

### 4.5 EventService
- `StartEvent`, `EndEvent`, `GetEventStats`.

### 4.8 ReportService
- `ExportDailyReportExcel`.

---

## 5. Helpers & Converters

### 5.1 `Helpers/RelayCommand.cs`
- Standard `ICommand` implementation.

### 5.2 `ViewModels/BaseViewModel.cs`
- `INotifyPropertyChanged` implementation.

### 5.3 `Converters/AllConverters.cs`
- `BoolToVisibility`, `InverseBoolToVisibility`, `StockToColorConverter`.

---

## 6. ViewModels Layer

### 6.1 LoginViewModel
- Manages credentials and authentication flow.

### 6.2 MainViewModel
- Shell ViewModel with navigation and sidebar permissions.

### 6.3 DashboardViewModel
- Real-time KPI loading.

### 6.4 POSViewModel
- Shopping cart, stock validation, and sale process.
- Automatically links to `ActiveEvent` if one exists.

### 6.5 InventoryViewModel
- Product grid with search and import actions.

### 6.8 EventViewModel
- Event lifecycle management (Start/End) and live stats.

---

## 7. Views (XAML + Code-Behind)

### Design System
- Monochrome (#000000 background, #111111 cards, #F5F5F5 text).

### 7.1 LoginWindow / 7.2 MainWindow
- Full shell implementation with sidebar navigation.

---

## 8. App Wiring (App.xaml.cs)
- Manual Dependency Injection for all services and ViewModels.
- `StartupUri` removed from `App.xaml`.

---

## 9. Final Verification Checklist

- [x] Login with seeded credentials.
- [x] Create sale during event → prefixed with "EV-".
- [x] Dashboard does not show event sale until event is ended.
- [x] Inventory import and Reports export function correctly.

---

*End of FashionPOS Builder Skill. Documentation updated 2026-04-12.*
