using System;
using System.Diagnostics;
using FixProjects.Abstract;
using FixProjects.Implementation;
using Microsoft.Extensions.DependencyInjection;
using Plossum.CommandLine;

namespace FixProjects
{
    public class Program
    {
        private static readonly CommandLineOptions CommandLineOptions = new CommandLineOptions();

        /// <summary>
        ///     Startup main method.
        /// </summary>
        /// <returns>Integer.</returns>
        public static int Main()
        {
            ServiceProvider serviceProvider = null;
            try
            {
                new CommandLineParser(CommandLineOptions).Parse();

                serviceProvider = new ServiceCollection()
                    .AddSingleton<IManageProjectFileOperations, ProjectFileOperationManager>(x => new ProjectFileOperationManager(CommandLineOptions))
                    .BuildServiceProvider();

                return serviceProvider.GetService<IManageProjectFileOperations>().Run();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);

                return 1;
            }
            finally
            {
                if (Debugger.IsAttached) Console.ReadLine();

                if (serviceProvider is IDisposable disposable) disposable.Dispose();
            }
        }
    }
}