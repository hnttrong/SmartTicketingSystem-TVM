using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmartTicketing.TVM
{
    public class TVMController
    {
        private MachineState state;
        private int idleSeconds;
        private Display display;
        private TicketPrinter printer;
        private PaymentService paymentService;
        private Transaction current;
        private CardReader cardReader;
        private TransactionRepository repository;
        private AlertService alerts;

        public void OnStart()
        {
            throw new System.NotImplementedException();
        }

        public void SelectDestination(int destinationId, int quantity)
        {
            throw new System.NotImplementedException();
        }

        public void ConfirmOrder()
        {
            throw new System.NotImplementedException();
        }

        public void SelectPaymentMethod(PaymentMethod method)
        {
            throw new System.NotImplementedException();
        }

        public void OnPaymentResult(PaymentResult result)
        {
            throw new System.NotImplementedException();
        }

        public void Cancel()
        {
            throw new System.NotImplementedException();
        }

        public void OnIdleTimeout()
        {
            throw new System.NotImplementedException();
        }

        public void PrintTickets()
        {
            throw new System.NotImplementedException();
        }

        public void RecoverLastTransaction()
        {
            throw new System.NotImplementedException();
        }
    }
}