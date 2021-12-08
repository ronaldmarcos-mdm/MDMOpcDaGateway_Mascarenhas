using MDMOpcDaGateway.Sources.Interfaces;
using Opc.Da;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MDMOpcDaGateway.Sources
{
    public class ItemOpcDa : IObserver
    {
			//trocado Index para string para evitar erros
			public string Id, Description, Mode, Format, Address;
			public double Scale, Offset;
			public bool Active;
			public ItemOPCMapResult result { get; set; }
			public ItemDriver refItemDriver { get; set; }
			public Driver refDriver { get; set; }

			public ItemOpcDa() { }
			public ItemOpcDa(string id, string description, string mode, string address, string format, double scale, double offset, bool b)
			{
				this.Id = id;
				this.Description = description;
				this.Mode = mode;
				this.Address = address;
				this.Format = format;
				this.Scale = scale;
				this.Offset = offset;
				this.Active = b;
			}
			public void UpdateItemDriver()
			{
				this.refItemDriver.Name = this.result.ItemName;
				this.refItemDriver.Value = this.result.Value;
				this.refItemDriver.Active = this.result.Validated;
				try
				{
					this.refItemDriver.TimeStamp = this.result.TimeStamp;
				}
				catch
				{
					this.refItemDriver.TimeStamp = DateTime.Now;
				}
				this.refItemDriver.InternalTimeStamp = DateTime.Now;
			}

			public void UpdateItemOpcDaResult(ItemValueResult value)
			{

				Console.WriteLine("ItemName: {0} - TimeStamp: {1} - Quality: {2} - Value: {3} ",
					value.ItemName, value.Timestamp, value.Quality, value.Value);

				this.result.ItemName = value.ItemName;
				this.result.TimeStamp = value.Timestamp;
				this.result.Validated = false;



				if (value.Quality.ToString().ToLower() == "good" || value.Quality.ToString().ToLower() == "goodoverride")
				{
					result.Validated = true;

					try
					{
						this.result.Value = value.Value;
					}
					catch (Exception e)
					{
						throw e;
					}
				}

				else
				{
					this.result.Validated = false;
				}
			}


			public void DataUpdated(ISubject subject)
			{
			//	throw new NotImplementedException();
			}

			public class ItemOPCMapResult
			{
				public string ItemName { get; set; }
				public DateTime TimeStamp { get; set; }
				public object Value { get; set; }
				public bool Validated { get; set; }

			}


			public static ItemDriver CreateItemDriver(ItemOpcDa _item)
			{
				ItemDriver itemDriver = new ItemDriver
				{
					Id = _item.Id,//trocar isto para o certo
					Name = _item.Description,
					TimeStamp = DateTime.Now,
					Active = _item.Active,

				};

				return itemDriver;
			}

			public static ItemOpcDa CreateItemOpcDa(string[] columns)
			{
				ItemOpcDa itemOpcDa = new ();

				RawItemOpcDa rawItemOpcDa = new RawItemOpcDa
				{
					Id = columns[0],
					Description = columns[1],
					Mode = columns[2],
					Address = columns[3],
					Format = columns[4],
					Scale = columns[5],
					Offset = columns[6],
				};				

				try
				{
					itemOpcDa.Id = rawItemOpcDa.Id;
					itemOpcDa.Description = rawItemOpcDa.Description;
					itemOpcDa.Mode = rawItemOpcDa.Mode;
					itemOpcDa.Address = rawItemOpcDa.Address;
					itemOpcDa.Format = rawItemOpcDa.Format;
					itemOpcDa.Scale = Double.Parse(rawItemOpcDa.Scale);
					itemOpcDa.Offset = Double.Parse(rawItemOpcDa.Offset);
					itemOpcDa.Active = true;

					itemOpcDa.result = new ItemOPCMapResult();

				}
				catch (Exception e)
				{
					System.Console.WriteLine("error: " + e);
					throw e;
				}
			     
				return itemOpcDa;
			}

			//Estrutura RawItemOpcDa de acordo ao formato do arquivo de endereços a ser carregado
			private struct RawItemOpcDa
			{
				public string Id, Description, Mode, Address, Format, Scale, Offset;
				public RawItemOpcDa(string id, string description, string mode, string address, string format, string scale, string offset)
				{
					this.Id = id;
					this.Description = description;
					this.Mode = mode;
					this.Address = address;
					this.Format = format;
					this.Scale = scale;
					this.Offset = offset;
				}
			}

	}
}
