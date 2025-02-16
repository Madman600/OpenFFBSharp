﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Device.Net;
using Hid.Net;
using Hid.Net.Windows;
using static OpenFFBoard.Commands;

namespace OpenFFBoard
{
    public class Hid : Board
    {
        private static IHidDevice _board;

        public Hid(IHidDevice device)
        {
            _board = device;
        }

        public IHidDevice GetBoard()
        {
            return _board;
        }

        /// <summary>
        /// Fetch currently connected OpenFFBoards
        /// </summary>
        /// <returns></returns>
        public static async Task<IHidDevice[]> GetBoardsAsync()
        {
            var hidFactory =
                new FilterDeviceDefinition(0x1209, 0xFFB0, label: "OpenFFBoard")
                    .CreateWindowsHidDeviceFactory();

            var deviceDefinitions =
                (await hidFactory.GetConnectedDeviceDefinitionsAsync().ConfigureAwait(false)).ToArray();
            IHidDevice[] devices = new IHidDevice[deviceDefinitions.Length];

            for (int i = 0; i < deviceDefinitions.Length; i++)
                devices[i] = ((IHidDevice) await hidFactory.GetDeviceAsync(deviceDefinitions[i]).ConfigureAwait(false));

            return devices;
        }

        /// <summary>
        /// Send command to OpenFFBoard (and receive data)
        /// </summary>
        /// <param name="device">Device to send data to</param>
        /// <param name="boardClass">Target class. Used for directing commands at a specific class.</param>
        /// <param name="instance">Instance of the targeted class. Used for directing commands at a specific driver/axis.</param>
        /// <param name="cmd">Command to send (board parameters)</param>
        /// <param name="data">Data to send</param>
        /// <param name="address">Second data to send (optional)</param>
        /// <returns></returns>
        public static async Task<Commands.BoardResponse> SendCmdAsync(IHidDevice device, BoardClass boardClass, byte? instance, BoardCommand cmd, ulong? data, ulong? address)
        {
            CmdType type;
            if (address == null && data == null)
                type = CmdType.Request;
            else if (address != null && data == null)
                type = CmdType.RequestAddress;
            else if (address == null)
                type = CmdType.Write;
            else
                type = CmdType.WriteAddress;

            var buffer = new byte[25];
            buffer[0] = 0xA1;
            buffer[1] = (byte) type; //type
            BitConverter.GetBytes(boardClass.ClassId).CopyTo(buffer, 2); //classID
            buffer[4] = instance ?? 0; //instance
            BitConverter.GetBytes(cmd.Id).CopyTo(buffer, 5); //cmd
            BitConverter.GetBytes(data ?? 0).CopyTo(buffer, 9); //data 1
            BitConverter.GetBytes(address ?? 0).CopyTo(buffer, 17); //data 2 (address)

            await device.WriteAsync(buffer).ConfigureAwait(false);
            TransferResult readBuffer;
            do
            {
                readBuffer = await device.ReadAsync().ConfigureAwait(false);
            } while (readBuffer.Data[0] != 0xA1);

            return new Commands.BoardResponse
            {
                Type = (Commands.CmdType) readBuffer.Data[1],
                ClassId = BitConverter.ToUInt16(readBuffer.Data, 2),
                Instance = readBuffer.Data[4],
                Cmd = cmd,
                Data = BitConverter.ToInt64(readBuffer.Data, 9),
                Address = BitConverter.ToUInt64(readBuffer.Data, 17)
            };
        }

        public override void Connect()
        {
            if (_board != null)
            {
                _board.InitializeAsync().ConfigureAwait(false);
                //IsConnected = _board.IsInitialized;
                IsConnected = true;
            }
            else
            {
                IsConnected = false;
                throw new NullReferenceException(
                    "OpenFFBoard not assigned, please use the SetBoard function to assign a board.");
            }
        }

        public override void Disconnect()
        {
            if (_board != null)
                _board.Close();
            else
            {
                throw new NullReferenceException(
                    "OpenFFBoard not assigned, please use the SetBoard function to assign a board.");
            }
        }

        internal override Commands.BoardResponse GetBoardData(BoardClass boardClass, byte? instance, BoardCommand cmd, ulong? address, bool info = false)
        {
            return SendCmdAsync(_board, boardClass, instance, cmd, null, address).Result;
        }

        internal override Commands.BoardResponse SetBoardData<T>(BoardClass boardClass, byte instance, BoardCommand<T> cmd, T value, ulong? address)
        {
            return SendCmdAsync(_board, boardClass, instance, cmd, Convert.ToUInt64(value), address).Result;
        }
    }
}