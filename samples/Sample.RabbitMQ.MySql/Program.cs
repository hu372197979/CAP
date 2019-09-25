using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System;

namespace Sample.RabbitMQ.MySql
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();
        }

        public static IWebHost BuildWebHost(string[] args)
        {
            var configuration = new ConfigurationBuilder().SetBasePath(Environment.CurrentDirectory)
                                          .AddJsonFile("host.json")
                                          .Build();
            return WebHost.CreateDefaultBuilder(args)
                .UseConfiguration(configuration)
                .UseShutdownTimeout(TimeSpan.FromSeconds(10))
                .UseStartup<Startup>()
                .Build();
        }
    }
}
