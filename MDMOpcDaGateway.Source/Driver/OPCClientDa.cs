using MDMOpcDaGateway.Sources.Interfaces;
using Opc.Da;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MDMOpcDaGateway.Sources
{
    public class OPCClientDa : Driver
    {
			private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
			private enum StateOPC { unknown = 0, running = 1, failed = 2, noConfig = 3, suspended = 4, test = 5, commFault = 6 }
			static Subscription group;

			private OPCParameters param;

			#region Attributes of the Opc Da Driver
			private Server server;
			private List<ItemOpcDa> listItemOpcDa;
			private List<Item> listItems;


			static Item[] itemsToAdd;
			static ItemValue[] writeValues;
			#endregion

			public OPCClientDa()
			{
				listItemOpcDa = new List<ItemOpcDa>();
				listItems = new List<Item>();

			}

			public override void Init()
			{
				param = new OPCParameters
				{
					NodeServer = Process.ReadParameter(base.Parameters, "NodeServer", "localhost"),
					ProgID = base.Parameters.Keys["ProgID"],
					Map = base.Parameters.Keys["Map"],
				};
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

			public void WriteValuesToOpcDa(ArrayList itemNames, ArrayList itemValues)
			{
				List<Item> itemsNotFound;			
				//Console.WriteLine("Writing Items' Values to Server... \n");				

				//create the items to write (if the group does not have it, we need to insert it)
				itemsToAdd = new Item[itemNames.Count];
				for (int idx = 0; idx < itemNames.Count; idx++)
				{
					itemsToAdd[idx] = new Item { ItemName = (string)itemNames[idx] };
				}
							   
				//create the items that contains the values to write
				writeValues = new ItemValue[itemValues.Count];
				for (int idj = 0; idj < itemValues.Count; idj++)
				{
					writeValues[idj] = new ItemValue (itemsToAdd[idj]);					
				}

				if (group != null)
				{
					int numbersOfItems = group.Items.Length;
					if (numbersOfItems > 0)
					{
						//make a scan of group to see if it already contains the item	
						itemsNotFound = CheckIfGroupContainItem(itemsToAdd);
						if (itemsNotFound.Count > 0)
						{
							Console.WriteLine("Adding {0} Not-found Items to Group", itemsNotFound.Count);
							AddAllItemsToGroup(itemsNotFound.ToArray());
						}
					}

					if (numbersOfItems == 0) { AddAllItemsToGroup(itemsToAdd); }

					SetAndWriteValues(writeValues, itemValues);
				}
				else
				{
					log.Error("Group of OpcDa Items is Null");
					Console.WriteLine("Group of OpcDa Items is Null");
				}
				
			}

			private void AddAllItemsToGroup(Item[] itemsToAdd)
			{				
				Console.WriteLine("Adding all {0} Items to Group", itemsToAdd.Length);			
				
				Item[] writeItems = new Item[itemsToAdd.Length];
				int idx = 0;
				foreach (Item item in itemsToAdd)
				{
					writeItems[idx] = item;
				    idx++;							
					Console.WriteLine("New Item inserted: " + item.ItemName);						
				}
		
				group.AddItems(writeItems);				
				Console.WriteLine("Recongized Items in Group: {0} \n", group.Items.Length);
				//PrintItemsNames();
			}

			private void SetAndWriteValues(ItemValue[] writeValues, ArrayList itemValues)
			{
				Console.WriteLine("Writing Values for {0} Items, if found in Group ({1} items)", itemValues.Count, group.Items.Length);			
				int idx = 0;				
				foreach (ItemValue writeValue in writeValues)
				{ 	
					foreach (Item item in group.Items)
					{
						if (string.Equals(writeValue.ItemName, item.ItemName))
						{
							writeValue.ServerHandle = item.ServerHandle;
							writeValue.Value = itemValues[idx];
							Console.WriteLine("Writing item: " + writeValue.ItemName + " " + idx.ToString());							
						}						
					}
					idx++;					
				}
				//write the value of the item
				Console.WriteLine("\n");
				group.Write(writeValues);
			}


			private List<Item> CheckIfGroupContainItem(Item[] itemsToAdd)
			{
			    //Console.WriteLine("Group items Size = {0}\n", group.Items.Length);				
				List<Item> itemsNotFound = new();
			    bool itemsFound;				

				foreach (Item itemAdd in itemsToAdd)
				{
					itemsFound = false;
					foreach (Item item in group.Items)
				    {
						if (String.Equals(itemAdd.ItemName, item.ItemName)) //if it find the iem, set the new value
						{						
							itemsFound = true;							
						}																
					}
					if(!itemsFound) { itemsNotFound.Add(itemAdd); }  
					
				}
				//Console.WriteLine("Not-Found Items Size = {0}\n", itemsNotFound.Count);	
				return itemsNotFound;
			}

			private void PrintItemsNames()
			{
				foreach (Item item in group.Items)
				{
					Console.WriteLine("New Item: " + item.ItemName);
				}
				Console.WriteLine("\n");
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
					
					SubscriptionState groupState = new SubscriptionState();
					groupState.Name = base.Name;
					groupState.Active = true;
					groupState.UpdateRate = 5000;
					group = (Subscription)server.CreateSubscription(groupState);					
					
				}
				catch (Exception e)
				{
					log.Error(e);
					Console.WriteLine("Error: ", e.Message);
				}
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
