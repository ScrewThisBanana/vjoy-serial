namespace SerialFeeder
{
    using System;
    using System.IO.Ports;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.DependencyInjection;

    using SerialFeeder.serial;
    using SerialFeeder.joystick;
    using Microsoft.Extensions.Logging;

    public class Program
    {
        public async static Task Main(string[] args)
        {
            await Task.CompletedTask;
            
            IHostBuilder hostBuilder = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) => { 
                    services.AddSingleton<SerialJoyStick>();
                    services.AddSingleton<SerialReader>();

                    services.AddOptions<JoystickConfiguration>()
                        .Bind(context.Configuration.GetSection("Joystick"))
                        .ValidateDataAnnotations();
                    services.AddOptions<PortConfiguration>()
                        .Bind(context.Configuration.GetSection("Serial"))
                        .ValidateDataAnnotations();
                });

            var host = hostBuilder.Build();

            using var scope = host.Services.CreateAsyncScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            using var reader = scope.ServiceProvider.GetRequiredService<SerialReader>();
            using var joystick = scope.ServiceProvider.GetRequiredService<SerialJoyStick>();

            reader.DataReceived += (sender, e) =>
            {
                try
                {
                    var data = SerialJoyStick.Convert(e.Data);
                    joystick.Update(data);
                }
                catch (ArgumentException) { }
                catch (Exception ex) { logger.LogWarning(ex.ToString()); }
            };

            joystick.Initialize();            
            reader.Open();

            logger.LogCritical("Press ENTER to exit");
            Console.ReadLine();
        }
    }
}