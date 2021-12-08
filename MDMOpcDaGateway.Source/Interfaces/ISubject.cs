using MDMOpcDaGateway.Sources.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace MDMOpcDaGateway.Sources
{
    public interface ISubject
    {
        void Attach(IObserver observer);
        void Notify();
    }
}
