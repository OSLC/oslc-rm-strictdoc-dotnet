var builder = DistributedApplication.CreateBuilder(args);

var strictDocOslcRm = builder
    .AddProject<Projects.StrictDocOslcRm>("strictdoc-oslc-rm", launchProfileName: null)
    .WithHttpEndpoint(env: "ASPNETCORE_HTTP_PORTS")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

strictDocOslcRm.WithEnvironment("OSLC__PublicBaseUri", strictDocOslcRm.GetEndpoint("http"));

builder.Build().Run();
