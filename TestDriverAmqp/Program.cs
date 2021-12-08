using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DriverAmqp.Sources;
using NetApiSQL.ModeloMDM285.ModelosAnaq;
//using NetApiSQL.ModeloMDM285.Util;
using Newtonsoft.Json;

namespace TestDriverAmqp
{
    public class Program
    {
        private static GlobalMsgType message;
        private static WrapperConnection amqp;
        static string exchange;
        static string routinKey;
       // static uu = NetApiSQL.ModeloMDM285.Util.Util;

        private static readonly string path = @"data.json";
        private static readonly string pathCsvOpcDa = @"opc_da_map.csv";
        private static readonly string pathCsvMatrikon = @"dataMatrikon.csv";
        private static readonly string base_path = AppDomain.CurrentDomain.BaseDirectory;
        static void Main(string[] args)
        {

            var amqpConfig = Util.LoadAmqpConfig();
            amqp = WrapperConnection.GetInstance();
            amqp.SetConfig = amqpConfig;
            Console.WriteLine("Connecting");
            amqp.Connect();
            Console.WriteLine("Connected");

            exchange = "ANAQ.STREAM";
            routinKey = "ALTO.StreamDataEstadosEquipamento.Json";
            
            Run();

            //var util  = NetApiSQL.ModeloMDM285.Util.Util.CreateJsonFile()
                
           /* var globalMsgType = new GlobalMsgType
            {
                MsgType = "",
                Data = "",
            };*/
        }

        public static void Run()
        {
           
            Subscriber sub = new Subscriber
            {
                SetConnection = amqp.GetConnection,
                SetExchange = exchange,
            };
            sub.AddRoutingKey(routinKey);
            sub.HandlerMessage += Sub_HandlerMessage;

            sub.Init();
            sub.Start();
           
            System.Console.WriteLine("Listening...\n");
         
            sub.Listen();
            System.Console.ReadLine();
        }

        private static void Sub_HandlerMessage(string mensage)
        {
            message = JsonConvert.DeserializeObject<GlobalMsgType>(mensage);
            System.Console.WriteLine("data: " + message.Data);
            System.Console.WriteLine("msgType: " + message.MsgType);

            string jsonString = JsonConvert.SerializeObject(message.Data);
            var dataObj = JsonConvert.DeserializeObject<List<DataEstadoEquipamento>>(jsonString);

            var arrFullNames = new ArrayList();
            //var arrTestsNames = new ArrayList();
            var arrTestsValues = new ArrayList();

            foreach (var data in dataObj)
            {
                foreach (var point in data.monitoredPoints)
                {
                    point.tests.ForEach(
                       test => {
                           arrFullNames.Add($"{point.fullName}.{test.name}");                           
                           arrTestsValues.Add(test.value);
                       }
                    );
                }                       
            }

            SaveToCsvFileForMatrikon(arrFullNames);
            SaveToCsvFileForOpcDa(arrFullNames);       

        }

        private static void SaveToCsvFileForMatrikon(ArrayList names)
        {
            System.Console.WriteLine("Save to Matrikon .csv file");
            string fullPath = Path.Combine(base_path, pathCsvMatrikon);           

            if (!File.Exists(fullPath))
            {
                // Create a file to write to.
                string head = "#OPC Server for Matrikon - Alias CSV File" + Environment.NewLine;
                File.WriteAllText(fullPath, head);
            }                   

            using (var w = File.CreateText(fullPath))
            {
                 string header = "#OPC Server for Matrikon - Alias CSV File";
                 w.WriteLine(header);

                 foreach (string name in names)
                 {
                    var first = name.Remove(name.LastIndexOf('.')); ;
                    var second = name.Split('.').Last();
                    var line = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20}", 
                                            first, second, " ", 4, 0, 0, 0, 0, " ", " ", " ", " ", " ", 0, 0, "Alias", 0, 1 ," ", 0, 0);
                    w.WriteLine(line);
                    w.Flush();
                 }
            }

        }

        private static void SaveToCsvFileForOpcDa(ArrayList names)
        {
            System.Console.WriteLine("Save to OpcDa .csv file");
            string fullPath = Path.Combine(base_path, pathCsvOpcDa);

            if (!File.Exists(fullPath))
            {
                // Create a file to write to.
                string head = "UrlOPC;Description;Mode;Outgoing Address;Format;Scale;Offset" + Environment.NewLine;
                File.WriteAllText(fullPath, head);
            }

            using (var w = File.CreateText(fullPath))
            {
                string header = "UrlOPC;Description;Mode;Outgoing Address;Format;Scale;Offset";
                w.WriteLine(header);

                foreach (string name in names)
                {
                    var urlOpc = name;
                    var description = name.Remove(name.LastIndexOf('.'));
                    var mode = "Write";
                    var outAddress = name;
                    var format = "Float";                    
                        
                    var line = string.Format("{0};{1};{2};{3};{4};{5};{6}",
                                            urlOpc, description, mode, outAddress, format, 1, 0);
                    w.WriteLine(line);
                    w.Flush();
                }
            }

        }

        private class Message
        {
            public string nome { get; set; }
            public string idade { get; set; }
        }
    }
}
