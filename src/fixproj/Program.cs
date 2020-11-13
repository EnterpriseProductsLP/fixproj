using System;
using System.Diagnostics;
using fixproj.Abstract;
using fixproj.Implementation;
using Microsoft.Extensions.DependencyInjection;
using Plossum.CommandLine;

namespace fixproj
{
    public class Program
    {
        private static readonly CommandLineOptions CommandLineOptions = new CommandLineOptions();

        /// <summary>
        /// Startup main method.
        /// </summary>
        /// <param name="args">A collection of input arguments.</param>
        /// <returns>Integer.</returns>
        public static int Main(string[] args)
        {
            ServiceProvider serviceProvider = null;
            try
            {
                new CommandLineParser(CommandLineOptions).Parse();

                serviceProvider = new ServiceCollection()
                    .AddSingleton<IProcess, ProcessFiles>(x => new ProcessFiles(CommandLineOptions))
                    .BuildServiceProvider();

                return serviceProvider.GetService<IProcess>().Run();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return 1;
            }
            finally
            {
                if (Debugger.IsAttached)
                {
                    Console.ReadLine();
                }

                if (serviceProvider is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
    }
}
