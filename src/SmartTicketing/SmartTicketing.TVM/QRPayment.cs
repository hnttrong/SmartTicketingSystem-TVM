using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmartTicketing.TVM
{
    public class QRPayment : Payment
    {
        private string qrContent;
        private System.DateTime expiresAt;
        private WalletProvider? provider;

        public string GenerateQR()
        {
            throw new System.NotImplementedException();
        }

        public bool IsExpired()
        {
            throw new System.NotImplementedException();
        }

        public override void Start()
        {
            throw new NotImplementedException();
        }
    }
}