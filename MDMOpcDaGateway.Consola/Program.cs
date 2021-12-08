using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MDMOpcDaGateway.Console
{
    internal class Program
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        static void Main(string[] args)
        {
            Sources.Process process = new Sources.Process();
            System.Console.WriteLine("Initializng Process....");
            process.OnStart();
			System.Console.ReadKey();
        }
    }
}
