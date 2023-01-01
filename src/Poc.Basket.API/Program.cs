using Azure.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using Poc.Basket.API.Infrastructure.Extensions;
using Poc.Basket.API.Infrastructure.Filters;
using System.IdentityModel.Tokens.Jwt;

var builder = WebApplication.CreateBuilder(args);

AddConfiguration(builder.Configuration);

builder.WebHost.ConfigureKestrel(options =>
{
    var grpcPort = builder.Configuration.GetValue("GRPC_PORT", 5001);
    var httpPort = builder.Configuration.GetValue("PORT", 80);

    options.Listen(IPAddress.Any, httpPort, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
    });

    options.Listen(IPAddress.Any, grpcPort, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
});

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = true;
});

builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddApplicationInsightsKubernetesEnricher();

builder.Services.AddControllers(options =>
    {
        options.Filters.Add(typeof(HttpGlobalExceptionFilter));
        options.Filters.Add(typeof(ValidateModelStateFilter));
    })
    .AddJsonOptions(options => options.JsonSerializerOptions.WriteIndented = true);

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "eShopOnContainers - Basket HTTP API",
        Version = "v1",
        Description = "The Basket Service HTTP API"
    });

    options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows()
        {
            Implicit = new OpenApiOAuthFlow()
            {
                AuthorizationUrl = new Uri($"{builder.Configuration.GetValue<string>("IdentityUrlExternal")}/connect/authorize"),
                TokenUrl = new Uri($"{builder.Configuration.GetValue<string>("IdentityUrlExternal")}/connect/token"),
                Scopes = new Dictionary<string, string>()
                {
                    { "basket", "Basket API" }
                }
            }
        }
    });
});

ConfigureAuthentication(builder);

builder.Services.AddCustomHealthCheck(builder.Configuration);
builder.Services.AddRedisConnection(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

Serilog.ILogger CreateSerilogLogger(IConfiguration configuration)
{
    var seqServerUrl = configuration["Serilog:SeqServerUrl"];
    var logstashUrl = configuration["Serilog:LogstashgUrl"];
    return new LoggerConfiguration()
        .MinimumLevel.Verbose()
        .Enrich.WithProperty("ApplicationContext", Program.AppName)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.Seq(string.IsNullOrWhiteSpace(seqServerUrl) ? "http://seq" : seqServerUrl)
        .WriteTo.Http(string.IsNullOrWhiteSpace(logstashUrl) ? "http://logstash:8080" : logstashUrl, null)
        .ReadFrom.Configuration(configuration)
        .CreateLogger();
}
void AddConfiguration(ConfigurationManager configuration)
{
    configuration.SetBasePath(Directory.GetCurrentDirectory());
    configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
    configuration.AddEnvironmentVariables();

    if (configuration.GetValue("UseVault", false))
    {
        TokenCredential credential = new ClientSecretCredential(
            configuration["Vault:TenantId"],
            configuration["Vault:ClientId"],
            configuration["Vault:ClientSecret"]);
        configuration.AddAzureKeyVault(new Uri($"https://{configuration["Vault:Name"]}.vault.azure.net/"), credential);
    }
}
void ConfigureAuthentication(WebApplicationBuilder builder)
{
    // prevent from mapping "sub" claim to nameidentifier.   
    JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Remove("sub");

    var identityUrl = builder.Configuration.GetValue<string>("IdentityUrl");

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;

    }).AddJwtBearer(options =>
    {
        options.Authority = identityUrl;
        options.RequireHttpsMetadata = false;
        options.Audience = "basket";
    });
}
public partial class Program
{

    public static string Namespace = typeof(Startup).Namespace;
    public static string AppName = Namespace.Substring(Namespace.LastIndexOf('.', Namespace.LastIndexOf('.') - 1) + 1);
}


