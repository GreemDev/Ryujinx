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

            string path = args[0];

            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Error: path is null or empty!");

            if (!Path.Exists(args[0]))
                throw new ArgumentException($"path {{{path}}} does not exist!");

            path = Path.GetFullPath(path);

            if (!Directory.GetDirectories(path).Contains($"{path}src"))
                throw new ArgumentException($"path {{{path}}} is not a valid ryujinx project!");

            LocalesValidationTask.Execute(path);
        }
    }
}
