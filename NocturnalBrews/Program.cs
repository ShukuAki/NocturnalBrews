using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using NocturnalBrews;
using NocturnalBrews.Controllers;
using NocturnalBrews.Services;

namespace NocturnalBrews
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddScoped<HomeController>();
                    services.AddHostedService<DailyInventoryInitializationService>();
                });
    }
}
