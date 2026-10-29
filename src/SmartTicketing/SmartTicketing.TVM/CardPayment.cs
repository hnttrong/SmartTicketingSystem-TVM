using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmartTicketing.TVM
{
    public class CardPayment : Payment
    {
        private string cardScheme;
        private string last4;
        private string gatewayToken;

        public override void Start()
        {
            throw new NotImplementedException();
        }
    }
}