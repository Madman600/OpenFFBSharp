﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management;

namespace OpenFFBoard
{
    public class Serial : Board
    {

        private readonly SerialPort _serialPort;

        public Serial(string comPort, int baudRate)
        {
            _serialPort = new SerialPort(comPort, baudRate);
        }

        public override void Connect()
        {
            _serialPort.Handshake = Handshake.None;
            _serialPort.Parity = Parity.None;
            _serialPort.DataBits = 8;
            _serialPort.StopBits = StopBits.One;
            _serialPort.DtrEnable = true;
            _serialPort.ReadTimeout = 200;
            _serialPort.WriteTimeout = 50;
            try
            {
                _serialPort.Open();
            }
            catch
            {
                throw new IOException("Could not connect to the OpenFFBoard on " + _serialPort.PortName);
            }
        }

        public override void Disconnect()
        {
            _serialPort.Close();
        }

        public static string[] GetBoards()
        {
            //TODO Filter to just OpenFFBoards
            return SerialPort.GetPortNames();
        }

        public override Commands.BoardResponse GetBoardData(BoardClass boardClass, byte instance, BoardCommand cmd)
        {
            return SendCmd(Commands.CmdType.Request, boardClass, instance, cmd);
        }

        public override Commands.BoardResponse GetBoardData(BoardClass boardClass, BoardCommand cmd)
        {
            return SendCmd(Commands.CmdType.Request, boardClass, null, cmd);
        }

        public Commands.BoardResponse SendCmd(Commands.CmdType type, BoardClass classId, byte? instance, BoardCommand cmd, ulong data = 0, ulong addr = 0)
        {
            string cmdBuffer = classId.Prefix + ".";
            if (instance != null)
                cmdBuffer += $"{instance}.";
            cmdBuffer += cmd.Name;
            switch (type)
            {
                case Commands.CmdType.Request:
                    cmdBuffer += '?';
                    break;
                case Commands.CmdType.Write:
                    cmdBuffer += '=';
                    cmdBuffer += data;
                    break;
                case Commands.CmdType.Info:
                    cmdBuffer += '!';
                    break;
            }

            _serialPort.WriteLine(cmdBuffer);
            string response = "";
            do
                response += _serialPort.ReadExisting();
            while (!response.Contains("]"));
            response = response.Trim();

            if (response.StartsWith("["))
            {
                response = response.TrimStart('[');
                response = response.TrimEnd(']');
                string[] splitResponse = response.Split('|');
                if (splitResponse[0] == cmdBuffer)
                {
                    string[] splitData = splitResponse[1].Split(new char[':'], 1);
                    string responseData;
                    ulong responseAddr;
                    if (splitData.Length >= 2)
                    {
                        responseData = splitData[0];
                        responseAddr = Convert.ToUInt64(splitData[1]);
                    }
                    else
                    {
                        responseData = splitResponse[1];
                        responseAddr = addr;
                    }
                    return new Commands.BoardResponse
                    {
                        Type = (Commands.CmdType)cmd.Id,
                        ClassId = classId.ClassId,
                        Instance = instance ?? 0,
                        Cmd = cmd,
                        Data = responseData,
                        Address = responseAddr
                    };

                }
            }
            return null;

        }

        public override Commands.BoardResponse SetBoardData(BoardClass boardClass, byte instance, BoardCommand cmd, ulong data, ulong address = 0)
        {
            throw new NotImplementedException();
        }
    }
}
