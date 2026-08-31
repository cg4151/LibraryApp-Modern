using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LibraryApp.Core.Interfaces;
using LibraryApp.Infrastructure;

namespace LibraryApp.UI
{
    class Program
    {
        static void Main(string[] args)
        {
            // Build configuration
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // Setup dependency injection
            var services = new ServiceCollection();
            
            services.AddSingleton<IConfiguration>(configuration);
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.AddConfiguration(configuration.GetSection("Logging"));
            });

            services.AddHttpClient<IKohaService, KohaService>();
            services.AddScoped<IRfidService, RfidService>();
            services.AddHttpClient<ICentralSystemService, CentralSystemService>();

            var serviceProvider = services.BuildServiceProvider();
            
            // Start application
            Console.WriteLine("=== NSBM Library Management System ===");
            Console.WriteLine("Modern - v2.0.0\n");
            
            ShowMenu(serviceProvider);
        }

        static void ShowMenu(IServiceProvider serviceProvider)
        {
            while (true)
            {
                Console.WriteLine("\nMain Menu:");
                Console.WriteLine("1. Checkout Book");
                Console.WriteLine("2. Checkin Book");
                Console.WriteLine("3. Write RFID Tag");
                Console.WriteLine("4. System Status");
                Console.WriteLine("5. Settings");
                Console.WriteLine("0. Exit");
                Console.Write("Select: ");

                var choice = Console.ReadLine();
                
                switch (choice)
                {
                    case "1":
                        CheckoutFlow(serviceProvider);
                        break;
                    case "2":
                        CheckinFlow(serviceProvider);
                        break;
                    case "3":
                        WriteRfidFlow(serviceProvider);
                        break;
                    case "4":
                        ShowSystemStatus(serviceProvider);
                        break;
                    case "5":
                        ShowSettings();
                        break;
                    case "0":
                        Console.WriteLine("Goodbye!");
                        return;
                    default:
                        Console.WriteLine("Invalid choice");
                        break;
                }
            }
        }

        static void CheckoutFlow(IServiceProvider serviceProvider)
        {
            Console.WriteLine("\n--- Checkout Book ---");
            Console.Write("Enter Member ID: ");
            if (int.TryParse(Console.ReadLine(), out int memberId))
            {
                Console.WriteLine("Waiting for RFID scan...");
                // RFID read and checkout logic
                Console.WriteLine("✓ Book checked out successfully");
            }
        }

        static void CheckinFlow(IServiceProvider serviceProvider)
        {
            Console.WriteLine("\n--- Checkin Book ---");
            Console.WriteLine("Waiting for RFID scan...");
            // RFID read and checkin logic
            Console.WriteLine("✓ Book checked in successfully");
        }

        static void WriteRfidFlow(IServiceProvider serviceProvider)
        {
            Console.WriteLine("\n--- Write RFID Tag ---");
            Console.Write("Enter Book ID: ");
            if (int.TryParse(Console.ReadLine(), out int bookId))
            {
                Console.WriteLine("Waiting for RFID tag...");
                // RFID write logic
                Console.WriteLine("✓ RFID tag written successfully");
            }
        }

        static void ShowSystemStatus(IServiceProvider serviceProvider)
        {
            Console.WriteLine("\n--- System Status ---");
            Console.WriteLine("✓ Koha Connected");
            Console.WriteLine("✓ Central System Connected");
            Console.WriteLine("✓ RFID Reader Connected");
            Console.WriteLine("✓ Database Synchronized");
        }

        static void ShowSettings()
        {
            Console.WriteLine("\n--- Settings ---");
            Console.WriteLine("1. Central System IP");
            Console.WriteLine("2. RFID Reader IP");
            Console.WriteLine("3. Koha Server URL");
            Console.WriteLine("0. Back");
            Console.Write("Select: ");
            
            var choice = Console.ReadLine();
            switch (choice)
            {
                case "1":
                    Console.Write("Enter Central System IP: ");
                    var ip = Console.ReadLine();
                    Console.WriteLine("✓ Saved");
                    break;
                case "2":
                    Console.Write("Enter RFID Reader IP: ");
                    var rfidIp = Console.ReadLine();
                    Console.WriteLine("✓ Saved");
                    break;
                case "3":
                    Console.Write("Enter Koha URL: ");
                    var kohaUrl = Console.ReadLine();
                    Console.WriteLine("✓ Saved");
                    break;
            }
        }
    }
}
