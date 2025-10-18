using OSLC4Net.Server.Providers;
using StrictDocOslcRm.Middleware;
using StrictDocOslcRm.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container (controllers + views for Razor selection dialog)
builder.Services.AddControllersWithViews(o => o.OutputFormatters.Insert(0, new OslcRdfOutputFormatter()));

// Configure CORS to allow requests from any domain
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .WithMethods("GET", "HEAD", "OPTIONS")
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Add HTTP context accessor for accessing request information in services
builder.Services.AddHttpContextAccessor();

// Add memory cache for StrictDoc data caching
builder.Services.AddMemoryCache();

// Register base URL service for URI generation (supports reverse proxy scenarios)
builder.Services.AddScoped<IBaseUrlService, BaseUrlService>();

// Register StrictDoc service
builder.Services.AddSingleton<IStrictDocService, StrictDocService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

// 
// app.UseHttpsRedirection();

// Enable CORS
app.UseCors();

// Add custom middleware for static file serving with content negotiation
app.UseMiddleware<StaticFileWithContentNegotiationMiddleware>();

app.UseStaticFiles();

app.UseRouting();

// Map controllers (attribute routes) and enable view endpoints
app.MapControllers();

app.Run();
