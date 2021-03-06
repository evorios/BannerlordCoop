﻿using System;
using RailgunNet.System.Encoding;
using RailgunNet.System.Types;

namespace Coop.Mod.Persistence.RPC
{
    public static class ArgumentSerializer
    {
        private static int NumberOfBitsForArgType => GetNumberOfBitsForArgType();

        private static int GetNumberOfBitsForArgType()
        {
            int numberOfValues = Enum.GetNames(typeof(EventArgType)).Length;
            return Convert.ToInt32(Math.Ceiling(Math.Log(numberOfValues, 2)));
        }

        [Encoder]
        public static void EncodeEventArg(this RailBitBuffer buffer, Argument arg)
        {
            buffer.Write(NumberOfBitsForArgType, Convert.ToByte(arg.EventType));
            switch (arg.EventType)
            {
                case EventArgType.EntityReference:
                    buffer.WriteEntityId(arg.RailId.Value);
                    break;
                case EventArgType.MBGUID:
                    buffer.WriteMBGUID(arg.MbGUID.Value);
                    break;
                case EventArgType.Null:
                    // Empty
                    break;
                case EventArgType.Int:
                    buffer.WriteInt(arg.Int.Value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        [Decoder]
        public static Argument DecodeEventArg(this RailBitBuffer buffer)
        {
            EventArgType eType = (EventArgType) buffer.Read(NumberOfBitsForArgType);
            switch (eType)
            {
                case EventArgType.EntityReference:
                    return new Argument(buffer.ReadEntityId());
                case EventArgType.MBGUID:
                    return new Argument(buffer.ReadMBGUID());
                case EventArgType.Null:
                    return Argument.Null;
                case EventArgType.Int:
                    return new Argument(buffer.ReadInt());
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
