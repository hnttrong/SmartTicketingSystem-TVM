using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmartTicketing.TVM
{
    public class Transaction
    {
        public string LastFailureReason { get; private set; }
        private string transactionId;
        private int quantity;
        private decimal fareSnapshot;
        private decimal totalAmount;
        private int retryCount;
        private int qrRegenCount;
        private TransactionStatus status;
        private System.DateTime createdAt;
        private string gatewayRef;
        private Destination destination;
        private System.Collections.Generic.List<Ticket> tickets;
        private System.Collections.Generic.List<Payment> payments;
        private Refund refund;

        public void LockFare(Fare fare)
        {
            throw new System.NotImplementedException();
        }

        public decimal CalculateTotal()
        {
            throw new System.NotImplementedException();
        }

        public bool CanRetry()
        {
            throw new System.NotImplementedException();
        }

        public bool CanRegenerateQR()
        {
            throw new System.NotImplementedException();
        }

        public void MarkConfirmed()
        {
            throw new System.NotImplementedException();
        }

        public void MarkFailed(string reason)
        {
            LastFailureReason = reason;
            retryCount++;
            status = TransactionStatus.Failed;
        }

        public void MarkCancelled()
        {
            throw new System.NotImplementedException();
        }

        public void MarkCompleted()
        {
            throw new System.NotImplementedException();
        }

        public void MarkRefundPending()
        {
            throw new System.NotImplementedException();
        }

        public void MarkPendingReconciliation()
        {
            throw new System.NotImplementedException();
        }
    }
}