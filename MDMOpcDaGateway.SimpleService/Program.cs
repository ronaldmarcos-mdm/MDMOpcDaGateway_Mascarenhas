﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Topshelf;

namespace MDMOpcDaGateway.SimpleService
{
    class Program
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        static void Main(string[] args)
        {
            var exitCode = HostFactory.Run(x =>
            {
                x.Service<Sources.Process>(s =>
                {
                    s.ConstructUsing(process => new Sources.Process(log));
                    s.WhenStarted(process => process.OnStart());
                    s.WhenStopped(process => process.OnStop());
                });

                x.RunAsLocalSystem();

                x.SetServiceName("MDM.OpcDaToAMQP"); 
                x.SetDisplayName("MDM OpcDa To AMQP");
                x.SetDescription("Driver Comunication from OpcDa To AMQP");
            });

            int exitCodeValue = (int)Convert.ChangeType(exitCode, exitCode.GetTypeCode());
            Environment.ExitCode = exitCodeValue;
        }
    }
}
