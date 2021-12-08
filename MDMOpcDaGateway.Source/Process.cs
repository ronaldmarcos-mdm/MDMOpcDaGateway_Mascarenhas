using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using DriverAmqp.Sources;
using NetApiSQL.ModeloMDM285.ModelosAnaq;
using Newtonsoft.Json;
using System.Collections;
using IniParser.Model;
using IniParser;

namespace MDMOpcDaGateway.Sources
{
    public class Process
    {
		    private static log4net.ILog log;//= log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
			private static readonly string path = @"config.ini";
			private static readonly string pathCsvOpcDa = @"opc_da_map.csv";
			private static readonly string pathCsvMatrikon = @"dataMatrikon";
			private static readonly string base_path = AppDomain.CurrentDomain.BaseDirectory;
			public static bool runOnce = true;
			private static IniData config;

			public static List<Driver> listDrivers;
			public static List<ItemDriver> Items;
			
			public static ArrayList arrFullNames;
			public static ArrayList arrTestsValues;	

			private static GlobalMsgType message;
			private static WrapperConnection amqp;
			static string exchange;
			static string routinKey;

		    public static OPCClientDa opcDa;
			public static Random rand = new Random();
			/// <summary>
			/// LoadConfig() is the factory method, Read config.ini file and instance the specified driver
			/// </summary>
			/// 

		    public Process(log4net.ILog _log)
			{
				log = _log;
				Console.WriteLine("process..");
			}
			public static void LoadConfig()
			{				
				log.Info(string.Format("Loading Config ..."));
				string fullPath = Path.Combine(base_path, path);
				log.Info(string.Format(fullPath));
				Console.WriteLine("path to file: " + fullPath);

				var parser = new FileIniDataParser();
				listDrivers = new List<Driver>();
				Items = new List<ItemDriver>();

				if (File.Exists(fullPath))
				{
					config = parser.ReadFile(fullPath);				
					foreach (SectionData section in config.Sections.ToArray())
					{
						log.Debug("Section Name: " + section.SectionName);					
						foreach (KeyData key in section.Keys.ToArray())
						{
							log.Debug("Key Name:  " + key.KeyName + " Value:   " + key.Value.ToString());
						}

						string DriverType = config[section.SectionName]["Driver"];

						if (DriverType == "OPCClient")
						{						
							Driver opcda = new OPCClientDa();
							AddDriver(opcda, section);
						}						
						else
						{
							log.Info(DriverType + " : No reconized!");
						}
					}
				}
				else
				{
					log.Error("Ini File Configuration no exists!:  " + fullPath);
					config = null;
				}

			}		

			public void OnStart()
			{						
				LoadConfig();
				Initialize();				
		
				foreach (Driver driver in listDrivers)
				{
					Thread t1 = new Thread(driver.Run);
					t1.Name = driver.Name;
					t1.Start();
				}
				log.Info("Writing Items' Values to Server...");
				Console.WriteLine("Writing Items' Values to Server... \n");
				InitRabbitMQ();
			}
		

		    public static void InitRabbitMQ()
			{
				opcDa = new OPCClientDa();
				log.Info(string.Format("Loading Amqp Driver Config..."));
				var amqpConfig = Util.LoadAmqpConfig();
				amqp = WrapperConnection.GetInstance();
				amqp.SetConfig = amqpConfig;
				Console.WriteLine("Connecting Amqp Driver...");
				amqp.Connect();
				Console.WriteLine("Amqp Driver Connected");
				log.Info(string.Format("Amqp Driver Connected"));

				exchange = amqpConfig.amqp.exchange; //"ANAQ.STREAM";
				routinKey = amqpConfig.amqp.baseRoutingKey;//"ALTO.StreamDataEstadosEquipamento.Json";
				log.Info(string.Format("exchange: " + exchange));
				log.Info(string.Format("exchange: " + routinKey));
				RunAmqp();
			}

			public static void RunAmqp()
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
				
				Console.WriteLine("Listening from RabbitMQ...\n");
				log.Info(string.Format("Listening from RabbitMQ..."));
				sub.Listen();				
			}

			public void OnStop()
			{

				log.Info(string.Format(" Stopping the Gateway MDM ..."));
				foreach (Driver driver in Process.listDrivers)
				{
					driver.running = false;
					driver.globalRunning = false;
					driver.Close();
				}
			}


			private static void Sub_HandlerMessage(string mensage)
			{
				
				message = JsonConvert.DeserializeObject<GlobalMsgType>(mensage);		

				string jsonString = JsonConvert.SerializeObject(message.Data);
				var dataObj = JsonConvert.DeserializeObject<List<DataEstadoEquipamento>>(jsonString);

				arrFullNames = new ArrayList();	
				arrTestsValues = new ArrayList();

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

				/*while (runOnce)
				{
					SaveToCsvFileForMatrikon(arrFullNames);
					SaveToCsvFileForOpcDa(arrFullNames);
					runOnce = false;								
				}*/
				try
				{
					if (arrFullNames.Count>0 && arrTestsValues.Count>0)
					{
						opcDa.WriteValuesToOpcDa(arrFullNames, arrTestsValues);
					}
				}
				catch (Exception e)
				{
					log.Error(e);
					Console.WriteLine("Error: ", e.Message);
				}

			}

			private static void SaveToCsvFileForMatrikon(ArrayList names)
			{
				int nid = rand.Next(0, 100);
				Console.WriteLine("Save to Matrikon .csv file");
				string npathId = $"{pathCsvMatrikon}_{nid}.csv";			
				log.Info(string.Format("Save to Matrikon .csv file"));
				string fullPath = Path.Combine(base_path, npathId);

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
												first, second, "", 4, 0, 0, 0, 0, "", "", "", "", "", 0, 0, "Alias", 0, 1, "", 0, 0);
						w.WriteLine(line);
						w.Flush();
					}
				}
			}

			private static void SaveToCsvFileForOpcDa(ArrayList names)
			{
				Console.WriteLine("Save to OpcDa Map .csv file \n\n");
				log.Info(string.Format("Save to  OpcDa Map .csv file"));
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

			public static void AddDriver(Driver driver, SectionData section)
			{
				driver.Name = section.SectionName;
				driver.ID = Int32.Parse(section.Keys["ID"]);
				driver.Parameters = section;
				listDrivers.Add(driver);			
			}

			public static void Initialize()
			{
				foreach (Driver driver in listDrivers)
				{
					driver.Init();
				}
			}
			public static string ReadParameter(SectionData _Parameters, string _param, string Default)
			{
				string param;
				try
				{
					param = _Parameters.Keys[_param];
				}
				catch
				{
					param = Default;
				}

				return param;
			}

	}
}
