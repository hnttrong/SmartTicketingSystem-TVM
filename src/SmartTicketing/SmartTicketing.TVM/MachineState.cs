using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmartTicketing.TVM
{
    public enum MachineState
    {
        Idle,
        SelectingDestination,
        ReviewingOrder,
        SelectingPayment,
        AwaitingPayment,
        PaymentConfirmed,
        Printing,
        Completed,
        Refunding,
        Cancelled,
        OutOfService,
        Maintenance,
        Recovering
    }
}