using System;
using System.Threading;

namespace ParsecVDisplay
{
    internal static class Daemon
    {
        static Thread EventThread;
        static CancellationTokenSource Cancellation;

        public static void Start()
        {
            Cancellation = new CancellationTokenSource();
            var token = Cancellation.Token;

            EventThread = new Thread(() => EventLoop(token));
            EventThread.IsBackground = false;
            EventThread.Priority = ThreadPriority.Highest;

            EventThread.Start();
        }

        public static void Stop()
        {
            Cancellation?.Cancel();
            EventThread?.Join();
        }

        static void EventLoop(CancellationToken cancellation)
        {
            while (true)
            {
                if (cancellation.IsCancellationRequested)
                    break;

                ParsecVDD.Ping();

                Thread.Sleep(100);
            }
        }
    }
}
