using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmartTicketing.TVM
{
    public class TicketPrinter
    {
        private int paperRemaining;
        private bool ready;

        public bool IsReady()
        {
            throw new System.NotImplementedException();
        }

        public int GetPaperLevel()
        {
            throw new System.NotImplementedException();
        }

        public bool Print(Ticket ticket)
        {
            throw new System.NotImplementedException();
        }
        public bool HasEnoughPaper(int quantity)
        {
            return quantity > 0 && paperRemaining >= quantity;
        }

    }
}