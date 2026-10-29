using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmartTicketing.TVM
{
    public class PaymentService
    {
        private System.Collections.Generic.List<IPaymentGateway> gateways;

        public Payment StartPayment(Transaction txn, PaymentMethod method)
        {
            throw new System.NotImplementedException();
        }

        public PaymentResult HandleCallback(GatewayCallback callback)
        {
            throw new System.NotImplementedException();
        }

        public PaymentStatus QueryStatus(Transaction txn)
        {
            throw new System.NotImplementedException();
        }

        public bool CancelPayment(Transaction txn)
        {
            throw new System.NotImplementedException();
        }

        public Refund Refund(Transaction txn)
        {
            throw new System.NotImplementedException();
        }
    }
}