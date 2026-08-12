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
dotnet user-secrets set --project TravelBlog.Web "ObjectStorage:Endpoint" "https://s3.example-provider.com"
dotnet user-secrets set --project TravelBlog.Web "ObjectStorage:Region" "region-name"
dotnet user-secrets set --project TravelBlog.Web "ObjectStorage:ForcePathStyle" "false"
dotnet user-secrets set --project TravelBlog.Web "ObjectStorage:Bucket" "travelblog-images"
dotnet user-secrets set --project TravelBlog.Web "ObjectStorage:AccessKey" "your-access-key"
dotnet user-secrets set --project TravelBlog.Web "ObjectStorage:SecretKey" "your-secret-key"
dotnet user-secrets set --project TravelBlog.Web "ObjectStorage:PublicBaseUrl" "https://images.example.com"
dotnet user-secrets set --project TravelBlog.Web "ObjectStorage:KeyPrefix" "featured-images"
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
ObjectStorage__Endpoint
ObjectStorage__Region
ObjectStorage__ForcePathStyle
ObjectStorage__Bucket
ObjectStorage__AccessKey
ObjectStorage__SecretKey
ObjectStorage__PublicBaseUrl
ObjectStorage__KeyPrefix
```

Use a PostgreSQL connection string such as:

```text
Host=db.example.com;Port=5432;Database=travelblog;Username=travelblog;Password=replace-me;SSL Mode=Require
```

The production identity that starts the app must be allowed to apply the
included migrations. Supply bootstrap values through the deployment platform's
secret store rather than committing them.

## Featured image storage

Featured images use an S3-compatible object store. `Endpoint` is the provider's
S3 API URL, while `PublicBaseUrl` is the externally accessible HTTP origin used
by browsers to display uploaded objects. The bucket or a CDN/custom domain in
front of it must permit public reads; an internal S3 API endpoint or private
bucket URL is not a usable `PublicBaseUrl`.

- AWS S3: use the regional S3 endpoint and region. `ForcePathStyle` is normally
  `false`; `PublicBaseUrl` can be a public bucket URL or CloudFront domain.
- Cloudflare R2: use the account-specific S3 API endpoint, region `auto`, and
  an R2 public bucket/custom domain for `PublicBaseUrl`.
- MinIO: use its S3 API endpoint and set `ForcePathStyle` to `true` when
  required. `PublicBaseUrl` must resolve externally in production, not merely
  to a container-internal hostname.

Uploads accept JPEG, PNG, WebP, and GIF files up to 5 MB. Validation checks both
the declared content type and file signature. Replacing or removing a featured
image deletes the previous managed object only after the post update is saved;
deleting a post also deletes its managed object. Existing legacy/local image
paths have no object key and are preserved during ordinary edits rather than
being deleted from object storage.

## Build and test

```powershell
dotnet build TravelBlog.sln
dotnet test TravelBlog.sln
```

Integration tests use `WebApplicationFactory` with an open SQLite in-memory
database. The `Testing` environment replaces PostgreSQL and deliberately skips
the production migration and bootstrap startup path.
