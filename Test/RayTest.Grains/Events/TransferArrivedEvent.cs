﻿using Ray.Core.Event;
using Ray.Core.Serialization;

namespace RayTest.Grains.Events
{
    [TCode(nameof(TransferArrivedEvent))]
    public class TransferArrivedEvent : IEvent
    {
        public decimal Amount { get; set; }
        public decimal Balance { get; set; }
    }
}
