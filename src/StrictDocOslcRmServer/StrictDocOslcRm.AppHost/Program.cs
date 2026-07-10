var builder = DistributedApplication.CreateBuilder(args);

var strictDocOslcRm = builder
    .AddProject<Projects.StrictDocOslcRm>("strictdoc-oslc-rm", launchProfileName: null)
    .WithHttpEndpoint(env: "ASPNETCORE_HTTP_PORTS")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

var configuredPublicBaseUri = builder.Configuration["OSLC:PublicBaseUri"];

if (string.IsNullOrWhiteSpace(configuredPublicBaseUri))
{
    strictDocOslcRm.WithEnvironment("OSLC__PublicBaseUri", strictDocOslcRm.GetEndpoint("http"));
}
else
{
    strictDocOslcRm.WithEnvironment("OSLC__PublicBaseUri", configuredPublicBaseUri);
}

builder.Build().Run();
