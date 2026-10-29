using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmartTicketing.TVM
{
    public enum TransactionStatus
    {
        Created,
        AwaitingPayment,
        Confirmed,
        Completed,
        Failed,
        Cancelled,
        RefundPending,
        Refunded,
        PendingReconciliation
    }
}