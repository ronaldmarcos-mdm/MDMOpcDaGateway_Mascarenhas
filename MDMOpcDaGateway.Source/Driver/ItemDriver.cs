using System;
using System.Collections.Generic;
using MDMOpcDaGateway.Sources.Interfaces;

namespace MDMOpcDaGateway.Sources
{
    public class ItemDriver : ISubject
    {
			public string Id;
			public string Name;
			public object Value;
			public DateTime TimeStamp;
			public bool Active;

			private DateTime _timestamp;
			public DateTime InternalTimeStamp
			{
				get { return _timestamp; }
				set
				{
					_timestamp = value;
					Notify();
				}
			}

			public List<IObserver> _observers;

			public Action notifica;

			public ItemDriver()
			{
				_observers = new List<IObserver>();
			}
			public ItemDriver(string id, string name, object s4, DateTime s5, bool s6, DateTime s10)
			{
				_observers = new List<IObserver>();
				Id = id;
				Name = name;
				Value = s4;
				TimeStamp = s5;
				Active = s6;
				InternalTimeStamp = s10;
			}

			public void Attach(IObserver observer)
			{
				//Belong to ISubject interface
				_observers.Add(observer);
			}
			public void Notify()
			{
				////Belong to ISubject interface
				_observers.ForEach(o =>
				{
					o.DataUpdated(this);
					//Console.WriteLine("Notificando aos observadores de {0}" , Name);
				});
			}

	}
}
