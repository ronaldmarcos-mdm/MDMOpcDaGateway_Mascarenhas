using System;
using System.Collections.Generic;
using System.Text;

namespace MDMOpcDaGateway.Sources.Interfaces
{
    public interface IObserver
    {
        void DataUpdated(ISubject subject);
        void UpdateItemDriver();
    }
}
