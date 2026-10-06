var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddHttpClient("fatewake-api", client => client.BaseAddress = new("https+http://api"));
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
var app = builder.Build();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<Fatewake.Web.Components.App>().AddInteractiveServerRenderMode();
app.Run();
