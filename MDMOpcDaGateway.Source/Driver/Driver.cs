using IniParser.Model;
using System;
using System.Collections.Generic;
using System.Threading;

namespace MDMOpcDaGateway.Sources
{
    public abstract class Driver
    {
			private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
			protected enum StateDriver { Initilizing = 0, Initilized = 1, Starting = 2, Started = 3, Error = 4, Default = 5, };
			protected Queue<StateDriver> q_State;

			public string Name { get; set; }
			public int ID { get; set; }
			public SectionData Parameters { get; set; }

			public bool globalRunning { get; set; }
			public bool running { get; set; }

			public Driver()
			{
				q_State = new Queue<StateDriver>();
			}

			/// <summary>
			/// Run() is the Template Method of Template Pattern in the UML Diagram
			/// </summary>
			/// 

			public void Run()
			{
				log.Info(string.Format("Executing Driver: {0}", Name));			
													
				this.globalRunning = true;
				try
				{
					//general loop with restarting process
					while (globalRunning)
					{
						try
						{
							Start();								
							this.running = true;
							log.Info(string.Format("Starting Driver: {0}", Name));

							//local flag for internal loop   
							while (running)
							{
								//keepalive
								try
								{
									running = CheckRunning();
								}
								catch (Exception e)
								{
									log.Error(e);
									running = false;
								}
								Thread.Sleep(5000);
							}
							try
							{
								Close();
							}
							catch (Exception e)
							{
								log.Error(e);
							}
						}
						catch (Exception e)
						{
							log.Error(e);
						}
						log.Info(string.Format("Restarting Driver: {0}!", this.Name));
						Thread.Sleep(5000);
					}
				}
				catch (Exception e)
				{
					log.Error(e);
				}
			
			}

			public abstract void Init();
			protected abstract void Start();
			protected abstract bool CheckRunning();
			public abstract void Close();

			//public abstract void WriteItemValue(string itemName, object itemValue);

			public void ResetState()
			{
				q_State.Clear();
			}

	}
}
