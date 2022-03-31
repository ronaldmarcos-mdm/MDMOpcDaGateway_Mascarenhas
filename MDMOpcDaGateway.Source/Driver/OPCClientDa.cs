using DriverAmqp.Sources;
using Newtonsoft.Json;
using Opc.Da;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace MDMOpcDaGateway.Sources
{
    public class OPCClientDa : Driver
    {
			private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
			private enum StateOPC { unknown = 0, running = 1, failed = 2, noConfig = 3, suspended = 4, test = 5, commFault = 6 }
			static Subscription group;

			private OPCParameters param;
		
			private Server server;
			private List<ItemOpcDa> listItemOpcDa;
			private List<Item> listItems;
			private List<ItemOpcDa.ItemOPCMapResult> listToOpcMsg;
		

			public OPCClientDa()
			{
				listItemOpcDa = new List<ItemOpcDa>();
				listItems = new List<Item>();
			    listToOpcMsg = new List<ItemOpcDa.ItemOPCMapResult>();
			}

			public override void Init()
			{
				param = new OPCParameters
				{
					NodeServer = Process.ReadParameter(base.Parameters, "NodeServer", "localhost"),
					ProgID = base.Parameters.Keys["ProgID"],
					Map = base.Parameters.Keys["Map"],
				};

				try
				{
					///Create ItemsOpcDa in the list of object driver
					CreateItems(param.Map); /// get items from csv file map
					base.q_State.Enqueue(StateDriver.Initilized);

				}
				catch (Exception e)
				{
					log.Error(e);
				}
			}
			
			protected override void Start()
			{
				try
				{	// Start OPC DA Driver						
					StarOpcDa();									

					AutoResetEvent autoEvent = new AutoResetEvent(false);
					var logTimer = new Timer(LogState, autoEvent, 0, 600000);
				    base.q_State.Enqueue(StateDriver.Started);
				}
				catch (Exception e)
				{
					log.Error(string.Format("Error to Starting OPC Server:  {0} - {1}", param.NodeServer, param.ProgID), e);
				}

			}
			protected override bool CheckRunning()
			{
				StateOPC _state = GetState();
				return ((_state == StateOPC.running) || (_state == StateOPC.suspended));

			}
			public override void Close()
			{
				base.q_State.Clear();
				this.server.Disconnect();
			}


			private void CreateItems(string _map)
			{

				try
				{
					StreamReader sr = Process.ReadMapFromFile(_map);

					//Discard the Header Line
					string header = sr.ReadLine();

					while (!sr.EndOfStream)
					{
						var line = sr.ReadLine();
						var columns = line.Split(';');

						ItemOpcDa itemOpcDa = ItemOpcDa.CreateItemOpcDa(columns);

						itemOpcDa.refDriver = this;
						///Add to list of the object
						listItemOpcDa.Add(itemOpcDa);
					}
				}
				catch (Exception e)
				{
					log.Error("Error Reading Map File!", e);
				}
			}			

			private void LogState(object stateinfo)
			{
					try
					{
						StateOPC _state = GetState();
						log.Debug(string.Format("State of OPC Serve Conection: '{0}'  - '{1}'", base.Name, _state.ToString()));
					}
					catch (Exception e)
					{
						throw e;
					}
			}

			public void StarOpcDa()
			{
				try
				{
					string scadaUrl = string.Format("opcda://{0}/{1}", this.param.NodeServer, this.param.ProgID);
					server = new Server(new OpcCom.Factory(), new Opc.URL(scadaUrl));
					server.Connect();
				
					log.Info(string.Format("Connected to the Server : {0} - {1} ", this.param.NodeServer, this.param.ProgID));
					Console.WriteLine("Connected to the Server : {0} - {1}\n", this.param.NodeServer, this.param.ProgID);

					SubscriptionState groupState = new SubscriptionState
					{
						Name = base.Name,
						Active = true,
						UpdateRate = 5000
					};
					group = (Subscription)server.CreateSubscription(groupState);

					foreach (ItemOpcDa itemOpcDa in listItemOpcDa)
					{
						try
						{
							Item _item = new Item
							{
								ItemName = itemOpcDa.Address,
								ClientHandle = itemOpcDa
							};
							listItems.Add(_item);
						}
						catch (Exception e)
						{
							throw e;
						}
					}
					group.AddItems(listItems.ToArray());
					SetItemsToOpcMsg();
					group.DataChanged += new DataChangedEventHandler(OnTransactionCompleted);

				}
				catch (Exception e)
				{
					log.Error(e);
					Console.WriteLine("Error: ", e.Message);
				}
			}		   

			private void OnTransactionCompleted(object group, object hReq, ItemValueResult[] results)
			{
				try
				{
					if (results.Length > 0)
					{
						Console.WriteLine("Number Items Updated {0}",results.Length);

						foreach (ItemValueResult result in results)
						{						
							foreach (var opcMsg in listToOpcMsg)
							{
								if (string.Equals(opcMsg.ItemName, result.ItemName))
                                {
									if (result.Quality.ToString().ToLower() == "good" || result.Quality.ToString().ToLower() == "goodoverride")
									{
										listToOpcMsg.Find(p => p.ItemName == result.ItemName).Value = result.Value;
										listToOpcMsg.Find(p => p.ItemName == result.ItemName).Validated = true;									
										Console.WriteLine("Item DataChange: {0}, Value: {1} at TimeStamp: {2}", result.ItemName, result.Value, result.Timestamp);
										log.Info(string.Format("Item DataChange: {0}, Value: {1} at TimeStamp: {2}", result.ItemName, result.Value, result.Timestamp));
									} 
									else
									{
										listToOpcMsg.Find(p => p.ItemName == result.ItemName).Value = -9999;
										listToOpcMsg.Find(p => p.ItemName == result.ItemName).Validated = false;
									}
									listToOpcMsg.Find(p => p.ItemName == result.ItemName).TimeStamp = DateTime.Now;
								}
							}								
						}
						Console.WriteLine("-------------------<");

						var myOpcMsg = new OpcMsgType() { Data = listToOpcMsg.ToArray() };
						Process.pub.Publish(JsonConvert.SerializeObject(myOpcMsg)); // publishing to Rabbitmq

						Util.SaveJsonFile(myOpcMsg, Path.Combine(Process.base_path, Process.pathJsonOpcMsg));
						PrintItemsOpcMsg();
					}
				}
				catch (Exception e)
				{
					log.Error(e);
				}
			}

			private void SetItemsToOpcMsg()
			{
				for (int i = 0; i < listItems.Count; i++)
				{
					try
					{
						var resultOpc = new ItemOpcDa.ItemOPCMapResult
						{
							ItemName = listItems[i].ItemName,
							TimeStamp = DateTime.Now,
							Validated = false,
							Value = -9999,
						};
						listToOpcMsg.Add(resultOpc);
					}
					catch (Exception e)
					{
						throw e;
					}
				}
			}

			private void PrintItemsOpcMsg()
			{
				foreach (var item in listToOpcMsg)
				{
					Console.WriteLine("Item: {0}, Value: {1}, TimeStamp: {2} ",  item.ItemName, item.Value, item.TimeStamp);
				}
				Console.WriteLine("\n");
			}

			private struct OPCParameters
			{
				public string NodeServer, ProgID, Map;
				public OPCParameters(string NodeServer, string ProgID, string Map)
				{
					this.NodeServer = NodeServer;
					this.ProgID = ProgID;
					this.Map = Map;
				}
			}

			private StateOPC GetState()
			{
				StateOPC state = StateOPC.unknown;
				try
				{
					string rawState = this.server.GetStatus().ServerState.ToString();
					try
					{
						state = (StateOPC)Enum.Parse(typeof(StateOPC), rawState, true);
					}
					catch
					{
						state = StateOPC.unknown;
					}

				}
				catch (Exception e)
				{
					log.Error("ERROR to get Status of Server, defined this as UNKNOWN", e);
					state = StateOPC.unknown;
				}
				return state;
			}
	}
}
