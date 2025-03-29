using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NocturnalBrews.Controllers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NocturnalBrews.Services
{
    public class DailyInventoryInitializationService : IHostedService
    {
        private readonly IServiceProvider _services;
        private Timer _timer;

        public DailyInventoryInitializationService(IServiceProvider services)
        {
            _services = services;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Run immediately when the application starts
            InitializeInventory();

            // Set up timer to check every day at midnight
            var now = DateTime.Now;
            var nextRunTime = now.Date.AddDays(1);
            var timeToNextRun = nextRunTime - now;

            _timer = new Timer(DoWork, null, timeToNextRun, TimeSpan.FromDays(1));

            return Task.CompletedTask;
        }

        private void DoWork(object state)
        {
            InitializeInventory();
        }

        private void InitializeInventory()
        {
            using (var scope = _services.CreateScope())
            {
                var controller = scope.ServiceProvider.GetRequiredService<HomeController>();
                controller.InitializeDailyInventory();
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Dispose();
            return Task.CompletedTask;
        }
    }
} 