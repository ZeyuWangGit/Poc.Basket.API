using Microsoft.Extensions.Configuration;

namespace Poc.Basket.API;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }
    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    public virtual IServiceProvider ConfigureServices(IServiceCollection services)
    {
        services.AddGrpc(options =>
        {
            options.EnableDetailedErrors = true;
        });

        services.AddApplicationInsightsTelemetry();
        services.AddApplicationInsightsKubernetesEnricher();

        services.AddControllers()
            .AddJsonOptions(options => options.JsonSerializerOptions.WriteIndented = true);



    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
    {


    }
}