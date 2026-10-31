using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmartTicketing.TVM
{
    public class Refund
    {
        private string refundId;
        private decimal amount;
        private string reference;
        private System.DateTime requestedAt;
        private RefundStatus status;
    }
}