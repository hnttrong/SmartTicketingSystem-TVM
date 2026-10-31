using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmartTicketing.TVM
{
    public class TransactionRepository
    {
        public void Save(Transaction txn)
        {
            throw new System.NotImplementedException();
        }

        public Transaction FindById(string id)
        {
            throw new System.NotImplementedException();
        }

        public System.Collections.Generic.List<Transaction> FindUnfinished()
        {
            throw new System.NotImplementedException();
        }

        public System.Collections.Generic.List<Destination> GetDestinations()
        {
            throw new System.NotImplementedException();
        }

        public Fare GetFare(int destinationId)
        {
            throw new System.NotImplementedException();
        }
    }
}