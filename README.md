# TravelBlog

ASP.NET Core MVC travel blog with PostgreSQL, Identity authentication, post
ownership, and administrator management.

## Local setup

Install the .NET 8 SDK and PostgreSQL, then restore the solution:

```powershell
dotnet restore TravelBlog.sln
```

Configure the PostgreSQL connection string and initial administrator with
.NET user secrets:

```powershell
dotnet user-secrets set --project TravelBlog.Web "ConnectionStrings:BlogDatabase" "Host=localhost;Port=5432;Database=travelblog;Username=postgres;Password=your-postgres-password"
dotnet user-secrets set --project TravelBlog.Web "BootstrapAdmin:Email" "admin@example.com"
dotnet user-secrets set --project TravelBlog.Web "BootstrapAdmin:Password" "choose-a-strong-password"
dotnet user-secrets set --project TravelBlog.Web "BootstrapAdmin:DisplayName" "Site Administrator"
```

Run the application:

```powershell
dotnet run --project TravelBlog.Web
```

At normal application startup, Entity Framework applies pending migrations.
The bootstrap initializer then configures the placeholder administrator created
by the ownership migration, creates the `Admin` role if needed, and assigns it.
Once the account has a password, later startups update its configured email and
display name but do not replace that established password.

## Production configuration

Set configuration with environment variables using double underscores for
nested keys:

```text
ConnectionStrings__BlogDatabase
BootstrapAdmin__Email
BootstrapAdmin__Password
BootstrapAdmin__DisplayName
```

Use a PostgreSQL connection string such as:

```text
Host=db.example.com;Port=5432;Database=travelblog;Username=travelblog;Password=replace-me;SSL Mode=Require
```

The production identity that starts the app must be allowed to apply the
included migrations. Supply bootstrap values through the deployment platform's
secret store rather than committing them.

## Build and test

```powershell
dotnet build TravelBlog.sln
dotnet test TravelBlog.sln
```

Integration tests use `WebApplicationFactory` with an open SQLite in-memory
database. The `Testing` environment replaces PostgreSQL and deliberately skips
the production migration and bootstrap startup path.
