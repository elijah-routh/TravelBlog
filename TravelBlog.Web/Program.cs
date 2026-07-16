var builder = WebApplication.CreateBuilder(args);

// Add MVC controllers and Razor views.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure production error handling.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Allows ASP.NET Core to serve files from wwwroot.
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// Default route:
// / goes to HomeController.Index
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();