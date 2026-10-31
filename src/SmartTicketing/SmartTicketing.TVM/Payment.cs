using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmartTicketing.TVM
{
    public abstract class Payment
    {
        protected string paymentId;
        protected decimal amount;
        protected PaymentStatus status;
        protected string gatewayRef;
        protected IPaymentGateway gateway;

        public abstract void Start();

        public bool Cancel()
        {
            throw new System.NotImplementedException();
        }
    }
}