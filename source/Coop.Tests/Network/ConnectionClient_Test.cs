﻿using System;
using Moq;
using Network.Infrastructure;
using Network.Protocol;
using Xunit;
using Version = Network.Protocol.Version;

namespace Coop.Tests.Network
{
    public class ConnectionClient_Test
    {
        public ConnectionClient_Test()
        {
            m_NetworkConnection
                .Setup(
                    con => con.SendRaw(It.IsAny<ArraySegment<byte>>(), It.IsAny<EDeliveryMethod>()))
                .Callback(
                    (ArraySegment<byte> arg, EDeliveryMethod eMethod) => m_SendRawParam = arg);
            m_GamePersistence = new Mock<IGameStatePersistence>();
            m_GamePersistence.Setup(per => per.Receive(It.IsAny<ArraySegment<byte>>()))
                             .Callback(
                                 (ArraySegment<byte> arg) => m_PersistenceReceiveParam = arg);
            m_Connection = new ConnectionClient(
                m_NetworkConnection.Object,
                m_GamePersistence.Object,
                m_WorldData.Object);
        }

        private readonly Mock<INetworkConnection> m_NetworkConnection =
            TestUtils.CreateMockConnection();

        private readonly Mock<IGameStatePersistence> m_GamePersistence;
        private readonly Mock<ISaveData> m_WorldData = TestUtils.CreateMockSaveData();
        private readonly ConnectionClient m_Connection;
        private ArraySegment<byte> m_PersistenceReceiveParam;

        private ArraySegment<byte> m_SendRawParam;

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        private void VerifyStateTransitionsUntilConnected(bool bExchangeWorldData)
        {
            m_WorldData.Setup(d => d.RequiresInitialWorldData).Returns(bExchangeWorldData);

            // Init
            Assert.Equal(EConnectionState.Disconnected, m_Connection.State);
            m_Connection.Connect();

            // Expect client hello
            m_NetworkConnection.Verify(
                c => c.SendRaw(It.IsAny<ArraySegment<byte>>(), It.IsAny<EDeliveryMethod>()),
                Times.Once);
            ArraySegment<byte> expectedSentData = TestUtils.MakeRaw(
                EPacket.Client_Hello,
                new Client_Hello(Version.Number).Serialize());
            Assert.Equal(expectedSentData, m_SendRawParam);
            Assert.Equal(EConnectionState.ClientJoinRequesting, m_Connection.State);

            // Ack client hello
            ArraySegment<byte> response = TestUtils.MakeRaw(
                EPacket.Server_RequestClientInfo,
                new Server_RequestClientInfo().Serialize());
            m_Connection.Receive(response);
            Assert.Equal(EConnectionState.ClientJoinRequesting, m_Connection.State);

            // Expect client info
            expectedSentData = TestUtils.MakeRaw(
                EPacket.Client_Info,
                new Client_Info(new Player("Unknown")).Serialize());
            Assert.Equal(expectedSentData, m_SendRawParam);
            Assert.Equal(EConnectionState.ClientJoinRequesting, m_Connection.State);

            // Ack client info
            response = TestUtils.MakeRaw(
                EPacket.Server_JoinRequestAccepted,
                new Server_JoinRequestAccepted().Serialize());
            m_Connection.Receive(response);

            if (bExchangeWorldData)
            {
                expectedSentData = TestUtils.MakeRaw(
                    EPacket.Client_RequestWorldData,
                    new Client_RequestWorldData().Serialize());
                Assert.Equal(expectedSentData, m_SendRawParam);
                Assert.Equal(EConnectionState.ClientAwaitingWorldData, m_Connection.State);

                // Send world data to client
                response = TestUtils.MakeRaw(
                    EPacket.Server_WorldData,
                    m_WorldData.Object.SerializeInitialWorldState());
                m_Connection.Receive(response);
                Assert.Equal(EConnectionState.ClientPlaying, m_Connection.State);
            }

            // Expect client joined
            expectedSentData = TestUtils.MakeRaw(
                EPacket.Client_Joined,
                new Client_Joined().Serialize());
            Assert.Equal(expectedSentData, m_SendRawParam);
            Assert.Equal(EConnectionState.ClientPlaying, m_Connection.State);

            // Send keep alive
            ArraySegment<byte> keepAliveFromServer = TestUtils.MakeKeepAlive(42);
            m_Connection.Receive(keepAliveFromServer);
            Assert.Equal(EConnectionState.ClientPlaying, m_Connection.State);

            // Expect client keep alive response
            expectedSentData = keepAliveFromServer;
            Assert.Equal(expectedSentData, m_SendRawParam);
        }

        [Fact]
        private void ReceiveForPersistenceIsIntercepted()
        {
            // Bring connection to EConnectionState.ClientPlaying
            VerifyStateTransitionsUntilConnected(false);
            Assert.Equal(EConnectionState.ClientPlaying, m_Connection.State);

            // Persistence has not received anything yet
            Assert.Null(m_PersistenceReceiveParam.Array);

            // Generate a payload
            ArraySegment<byte> persistencePayload = TestUtils.MakePersistencePayload(50);

            // Receive
            m_Connection.Receive(persistencePayload);

            // Verify
            Assert.Equal(persistencePayload, m_PersistenceReceiveParam);
            Assert.Equal(EConnectionState.ClientPlaying, m_Connection.State);

            // Interweave a keep alive
            ArraySegment<byte> keepAliveFromServer = TestUtils.MakeKeepAlive(42);
            m_Connection.Receive(keepAliveFromServer);
            Assert.Equal(keepAliveFromServer, m_SendRawParam); // Client ack

            // Send another persistence packet
            m_PersistenceReceiveParam = new ArraySegment<byte>();
            m_Connection.Receive(persistencePayload);

            // Verify
            Assert.Equal(persistencePayload, m_PersistenceReceiveParam);
            Assert.Equal(EConnectionState.ClientPlaying, m_Connection.State);
        }
    }
}
