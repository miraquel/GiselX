# GiselX

A modern ASP.NET Core 9.0 web application built with clean architecture principles, featuring Identity management, role-based authorization, and a responsive admin dashboard.

## 🏗️ Architecture

This solution follows a layered architecture pattern with clear separation of concerns:

```
GiselX/
├── GiselX.Web                    # Presentation Layer (ASP.NET Core MVC)
├── GiselX.Service                # Business Logic Layer
├── GiselX.Service.Interface      # Service Abstractions
├── GiselX.Service.Dto            # Data Transfer Objects
├── GiselX.Repository             # Data Access Layer
├── GiselX.Repository.Interface   # Repository Abstractions
├── GiselX.Domain                 # Domain Models & Entities
├── GiselX.Mapper                 # Object Mapping (Mapperly)
└── GiselX.Common                 # Shared Utilities & Constants
```

## 🎯 Key Features

- **ASP.NET Core 9.0** - Built on the latest .NET framework
- **ASP.NET Core Identity** - Complete user authentication and authorization
- **Role-Based Access Control (RBAC)** - Fine-grained permission system
- **Entity Framework Core** - SQL Server & SQLite support
- **Clean Architecture** - Separation of concerns with repository and service patterns
- **Mapperly** - High-performance object mapping
- **Excel Export** - ClosedXML integration for data export
- **Modern UI** - Bootstrap 5 admin dashboard (Materio template)
- **Gulp & Webpack** - Frontend build pipeline

## 📋 Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js](https://nodejs.org/) (for frontend assets)
- SQL Server or SQLite (configured via connection string)
- IDE: Visual Studio 2022, JetBrains Rider, or VS Code

## 🚀 Getting Started

### 1. Clone the Repository

```bash
git clone <repository-url>
cd GiselX
```

### 2. Restore Dependencies

```bash
# Restore .NET packages
dotnet restore

# Restore Node.js packages
cd GiselX.Web
npm install
```

### 3. Configure Database

Update the connection string in `GiselX.Web/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=GiselXDb;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```

Or use SQLite (default):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "DataSource=app.db;Cache=Shared"
  }
}
```

### 4. Apply Migrations

```bash
cd GiselX.Web
dotnet ef database update
```

### 5. Build Frontend Assets

```bash
cd GiselX.Web
npm run build
```

### 6. Run the Application

```bash
dotnet run --project GiselX.Web
```

Navigate to `https://localhost:5001` or `http://localhost:5000`

## 📦 Project Details

### GiselX.Web
The main web application containing:
- MVC Controllers and Views
- ASP.NET Core Identity configuration
- Authentication & Authorization setup
- Frontend assets (src/, wwwroot/)
- Database context

**Key Technologies:**
- ASP.NET Core MVC 9.0
- ASP.NET Core Identity
- Entity Framework Core
- Bootstrap 5 (Materio template)

### GiselX.Service
Business logic layer containing service implementations for:
- Company management
- Service level management
- Business rules and validations

### GiselX.Repository
Data access layer implementing the repository pattern:
- `CompanyRepository`
- `ServiceLevelRepository`
- Database operations and queries

### GiselX.Domain
Domain entities and models:
- `AppIdentityUser` / `AppIdentityRole` - Identity entities
- `Company` - Company entity
- `ServiceLevel` - Service level entity
- `Period` - Period entity
- `TransDist` - Transaction distribution
- `ServelDeliveryEx` / `ServelReceiptEx` - Service delivery/receipt extensions
- Common models: `ListRequest`, `PagedList`, `PagedListRequest`

### GiselX.Service.Dto
Data Transfer Objects for:
- `CompanyDto`
- `ServiceLevelDto`
- `PeriodDto`
- `UserDto`
- `RoleDto`

### GiselX.Common
Shared utilities and helpers:
- `PermissionConstants` - Application permissions
- `ExcelHelper` - Excel export functionality
- `QueryableExtensions` - LINQ extensions

### GiselX.Mapper
Object mapping using Mapperly for high-performance mapping between entities and DTOs.

## 🔐 Authentication & Authorization

The application uses ASP.NET Core Identity with:
- User registration and login
- Email confirmation requirement
- Role-based authorization
- Custom permission-based policies
- Bearer token authentication support
- Session management (30-minute sliding expiration)

## 🗃️ Database

Supports both SQL Server and SQLite:
- **Development**: SQLite (app.db)
- **Production**: SQL Server (configure in appsettings.json)

Key tables:
- AspNetUsers / AspNetRoles (Identity)
- Companies
- ServiceLevels
- Periods
- TransDist
- ServelDeliveryEx / ServelReceiptEx

## 🛠️ Development

### Build Solution

```bash
dotnet build
```

### Run Tests (if available)

```bash
dotnet test
```

### Frontend Development

```bash
cd GiselX.Web
npm run build  # Build assets
npx gulp watch # Watch for changes
```

### Entity Framework Migrations

```bash
# Add new migration
dotnet ef migrations add <MigrationName> --project GiselX.Web

# Update database
dotnet ef database update --project GiselX.Web

# Remove last migration
dotnet ef migrations remove --project GiselX.Web
```

## 📝 Configuration

### Application Settings

Configure in `appsettings.json`:
- Connection strings
- Logging levels
- Identity options (in Program.cs)
- Cookie settings

### Identity Configuration

Customize in `Program.cs`:
- Password requirements
- Lockout settings
- Session timeout
- Email confirmation

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📄 License

This project uses the Materio Bootstrap template (MIT License) for its frontend design.

## 📧 Contact

For questions or support, please open an issue in the repository.

---

**Built with ❤️ using ASP.NET Core 9.0**

