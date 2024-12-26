using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ryujinx.BuildValidationTasks
{
    public class Program
    {
        static void Main(string[] args)
        {
            // Display the number of command line arguments.
            if (args.Length != 1)
            {
                if (args.Length == 0)
                    throw new ArgumentException("Error: too few arguments!");
                else
                    throw new ArgumentException("Error: too many arguments!");
            }
            if (string.IsNullOrEmpty(args[0]))
                throw new ArgumentException("Error: path is null or empty!");

            if (!Path.Exists(args[0]))
                throw new ArgumentException("Error: path does not exist!");

            Console.WriteLine(args[0]);
            Console.WriteLine(Path.GetFullPath(args[0]));
            LocalesValidationTask.Execute(args[0]);
        }
    }
}
