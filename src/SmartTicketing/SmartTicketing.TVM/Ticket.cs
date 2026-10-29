using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmartTicketing.TVM
{
    public class Ticket
    {
        private string ticketNumber;
        private string barcode;
        private TransportMode mode;
        private string origin;
        private string destination;
        private System.DateTime issuedAt;
        private System.DateTime validUntil;
        private decimal fare;

        public string GenerateBarcode(int sequence)
        {
            throw new System.NotImplementedException();
        }
        public bool IsValid(DateTime checkedAt)
        {
            return checkedAt >= issuedAt && checkedAt < validUntil;
        }
    }
}