using System;

namespace ParsecVDisplay.Vdd
{
    internal class ErrorDriverStatus : Exception
    {
        public readonly Device.Status Status;

        public ErrorDriverStatus(Device.Status status)
        {
            this.Status = status;
        }
    }

    internal class ErrorDeviceHandle : Exception
    {
    }

    internal class ErrorExceededLimit : Exception
    {
        public readonly int Limit;

        public ErrorExceededLimit(int limit)
        {
            this.Limit = limit;
        }
    }

    internal class ErrorOperationFailed : Exception
    {
        public enum Operation
        {
            AddDisplay,
            RemoveDisplay,
        }

        public readonly Operation Type;

        public ErrorOperationFailed(Operation type)
        {
            this.Type = type;
        }
    }
}