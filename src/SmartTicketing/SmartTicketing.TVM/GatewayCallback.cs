using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmartTicketing.TVM
{
    public class GatewayCallback
    {
        private string transactionRef;
        private decimal amount;
        private string signature;
        private PaymentStatus status;
    }
}