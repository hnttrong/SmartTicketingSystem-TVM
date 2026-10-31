using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmartTicketing.TVM
{
    public class VietQRAdapter : IPaymentGateway
    {
        public bool Cancel(string reference)
        {
            throw new NotImplementedException();
        }

        public string CreatePayment(string txnId, decimal amount)
        {
            throw new NotImplementedException();
        }

        public PaymentStatus QueryStatus(string reference)
        {
            throw new NotImplementedException();
        }

        public string Refund(string reference, decimal amount)
        {
            throw new NotImplementedException();
        }

        public bool VerifyCallback(GatewayCallback callback)
        {
            throw new NotImplementedException();
        }
    }
}