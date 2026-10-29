using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmartTicketing.TVM
{
    public class Display
    {
        public void ShowMenu(System.Collections.Generic.List<Destination> destinations)
        {
            throw new System.NotImplementedException();
        }

        public void ShowSummary(Transaction txn)
        {
            throw new System.NotImplementedException();
        }

        public void ShowPaymentModes(System.Collections.Generic.List<PaymentMethod> modes)
        {
            throw new System.NotImplementedException();
        }

        public void ShowQR(string qrContent, int secondsLeft)
        {
            throw new System.NotImplementedException();
        }

        public void ShowMessage(string text)
        {
            throw new System.NotImplementedException();
        }

        public void ShowOutOfService()
        {
            throw new System.NotImplementedException();
        }
    }
}