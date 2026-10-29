using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmartTicketing.TVM
{
    public interface IPaymentGateway
    {
        string CreatePayment(string txnId, decimal amount);
        PaymentStatus QueryStatus(string reference);
        bool Cancel(string reference);
        string Refund(string reference, decimal amount);
        bool VerifyCallback(GatewayCallback callback);
    }
}