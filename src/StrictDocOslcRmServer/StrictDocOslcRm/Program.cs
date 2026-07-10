using OSLC4Net.Server.Providers;
using StrictDocOslcRm.Middleware;
using StrictDocOslcRm.Services;

var builder = WebApplication.CreateBuilder(args);

// REVISIT: Replace the toy OAuth1 provider with a production OAuth consumer
// registry/token implementation, or delegate authentication to the deployment
// boundary once Jazz interop no longer depends on this in-process compatibility shim.
builder.Services.AddToyOAuth1(builder.Configuration);

// Add services to the container (controllers + views for Razor selection dialog)
builder.Services.AddControllersWithViews(o => o.OutputFormatters.Insert(0, new OslcRdfOutputFormatter()));

// Configure CORS to allow requests from any domain
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .WithMethods("GET", "HEAD", "OPTIONS", "PUT")
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
builder.Services.AddSingleton<ILinkSidecarService, FileLinkSidecarService>();

// Register OSLC Query evaluation service (oslc.where/select/orderBy/searchTerms/paging)
builder.Services.AddSingleton<IOslcQueryService, OslcQueryService>();

var app = builder.Build();

app.UseForwardedHeaders();
app.Use((context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/oslc") ||
        context.Request.Path.StartsWithSegments("/.well-known/oslc/rootservices.xml") ||
        context.Request.Path.StartsWithSegments("/.well-known/oslc/scr") ||
        (context.Request.Path == "/" && context.Request.Query.ContainsKey("a")))
    {
        context.Response.Headers["OSLC-Core-Version"] = "2.0";
    }

    return next();
});
app.MapToyOAuth1Provider();

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
