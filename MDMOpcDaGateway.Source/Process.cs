using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using DriverAmqp.Sources;
using IniParser.Model;
using IniParser;
using NetApiSQL.ModeloMDM285.ModelosAnaq;

namespace MDMOpcDaGateway.Sources
{
    public class Process
    {
		    private static log4net.ILog log;//= log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
			private static readonly string path = @"config.ini";	
			public static readonly string base_path = AppDomain.CurrentDomain.BaseDirectory;
			public static readonly string pathJsonOpcMsg = @"lastDataOpcMsg.json";
			public static bool runOnce = true;
			private static IniData config;

			public static List<Driver> listDrivers;
			public static List<ItemDriver> Items;
			public static Publisher pub;

			private static GlobalMsgType message;
			private static WrapperConnection amqp;

			static string exchange;
			static string routinKey;
		
			/// <summary>
			/// LoadConfig() is the factory method, Read config.ini file and instance the specified driver
			/// </summary>
			/// 

		    public Process(log4net.ILog _log)
			{
				log = _log;
				Console.WriteLine("process..");
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

			public void OnStop()
			{

				log.Info(string.Format(" Stopping the Gateway MDM ..."));
				foreach (Driver driver in listDrivers)
				{
					driver.running = false;
					driver.globalRunning = false;
					driver.Close();
				}
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
							log.Info(DriverType + " : No Recognized!");
						}
					}
				}
				else
				{
					log.Error("File .ini Configuration do not exists!: " + fullPath);
					config = null;
				}

			}

			public static StreamReader ReadMapFromFile(string _map)
			{
				string mapPath = Path.Combine(base_path, _map);
				log.Info(string.Format("Path of Map: {0}", mapPath));

				StreamReader sr;
				if (File.Exists(mapPath))
				{
					try
					{
						sr = new StreamReader(mapPath);
					}
					catch (Exception e)
					{
						throw e;
					}
				}
				else
				{
					Exception e = new Exception("File Map does not Exists!!");
					throw e;
				}
				return sr;
			}


			public static void InitRabbitMQ()
			{				
				log.Info(string.Format("Loading Amqp Driver Config..."));
				var amqpConfig = Util.LoadAmqpConfig();
				amqp = WrapperConnection.GetInstance();
				amqp.SetConfig = amqpConfig;
				Console.WriteLine("Connecting Amqp Driver...");
				log.Info(string.Format("Connecting Amqp Driver..."));

				try { 
					amqp.Connect(); 
					Console.WriteLine("Amqp Driver Connected");
					log.Info(string.Format("Amqp Driver Connected"));
				}
				catch (Exception e)
				{
					Console.WriteLine(e);
					log.Error(e);
				}			

				exchange = amqpConfig.amqp.exchange; //"ANAQ.STREAM";
				routinKey = amqpConfig.amqp.baseRoutingKey;//"ALTO.StreamDataEstadosEquipamento.Json";
				log.Info(string.Format("exchange: " + exchange));
				log.Info(string.Format("routinKey: " + routinKey));
				RunAmqpPub();
			}

			public static void RunAmqpPub()
			{
				pub = new Publisher
				{
					SetConnection = amqp.GetConnection,
					SetExchange = exchange,
					SetRoutingKey = routinKey
				};				

				pub.Init();
				pub.Start();		

				Console.WriteLine("Publishing to RabbitMQ...\n");
				log.Info(string.Format("Publishing to RabbitMQ..."));
								
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
