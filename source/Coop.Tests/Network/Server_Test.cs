﻿using System;
using System.Diagnostics;
using System.Threading;
using Common;
using Network.Infrastructure;
using Xunit;

namespace Coop.Tests.Network
{
    public class Server_Test
    {
        public Server_Test()
        {
            m_Server = new Server(Server.EType.Threaded);
            m_Module = new TimingModule();
            m_Config = new ServerConfiguration
            {
                TickRate = 0
            };
            m_Server.Updateables.Add(m_Module);
        }

        private class TimingModule : IUpdateable
        {
            public readonly MovingAverage AverageTicksPerFrame = new MovingAverage(100000);
            public readonly object Lock = new object();
            public readonly AutoResetEvent OnTick = new AutoResetEvent(false);
            public int iCounter;

            private Stopwatch m_Timer;

            public void Update(TimeSpan frameTime)
            {
                ++iCounter;
                OnTick.Set();
                lock (Lock)
                {
                }

                if (m_Timer == null)
                {
                    m_Timer = Stopwatch.StartNew();
                }
                else
                {
                    AverageTicksPerFrame.Push(m_Timer.ElapsedTicks);
                    m_Timer.Restart();
                }
            }
        }

        private readonly Server m_Server;
        private readonly TimingModule m_Module;
        private readonly ServerConfiguration m_Config;

        [Theory]
        [InlineData(5)]
        [InlineData(10)]
        [InlineData(60)]
        [InlineData(100)]
        [InlineData(144)]
        [InlineData(500)]
        public void TickLimiter(uint uiTickRate)
        {
            m_Config.TickRate = uiTickRate;
            TimeSpan sleepTime = TimeSpan.FromMilliseconds(1000);
            TimeSpan expectedTickTime = TimeSpan.FromMilliseconds(1000 / (double) uiTickRate);

            Assert.True(m_Server.State == Server.EState.Inactive);
            m_Server.Start(m_Config);

            Thread.Sleep(sleepTime);
            m_Server.Stop();
            Assert.True(m_Server.State == Server.EState.Inactive);
            double diff = Math.Abs(m_Module.AverageTicksPerFrame.Average - expectedTickTime.Ticks);
            Assert.True(diff < .7 * expectedTickTime.Ticks);
        }

        [Fact]
        public void StartAndStop()
        {
            // start server
            Assert.True(m_Server.State == Server.EState.Inactive);
            m_Server.Start(m_Config);
            Assert.Equal(Server.EState.Running, m_Server.State);

            // wait for first sim tick
            m_Module.OnTick.WaitOne();
            Assert.Equal(Server.EState.Running, m_Server.State);
            Assert.True(m_Module.iCounter > 0);

            // stop server
            m_Server.Stop();
            Assert.True(m_Server.State == Server.EState.Inactive);
            int iCounterAfterStop = m_Module.iCounter;
            m_Module.OnTick.WaitOne(5);
            Assert.Equal(iCounterAfterStop, m_Module.iCounter);
        }
    }
}
