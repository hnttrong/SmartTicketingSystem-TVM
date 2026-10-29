using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmartTicketing.TVM
{
    public class Destination
    {
        private int destinationId;
        private string name;
        private string origin;
        private TransportMode mode;
        private System.Collections.Generic.List<Fare> fares;
    }
}