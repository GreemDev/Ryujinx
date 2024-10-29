using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Types;
using System;
using System.Net;
using System.Net.Sockets;

namespace Ryujinx.HLE.HOS.Services.Sockets.Bsd
{
    interface ISocket : IFileDescriptor
    {
        IPEndPoint RemoteEndPoint { get; }
        IPEndPoint LocalEndPoint { get; }

        AddressFamily AddressFamily { get; }

        SocketType SocketType { get; }

        ProtocolType ProtocolType { get; }

        nint Handle { get; }

        LinuxError Receive(out int receiveSize, Span<byte> buffer, BsdSocketFlags flags);

        LinuxError ReceiveFrom(out int receiveSize, Span<byte> buffer, int size, BsdSocketFlags flags, out IPEndPoint remoteEndPoint);

        LinuxError Send(out int sendSize, ReadOnlySpan<byte> buffer, BsdSocketFlags flags);

        LinuxError SendTo(out int sendSize, ReadOnlySpan<byte> buffer, int size, BsdSocketFlags flags, IPEndPoint remoteEndPoint);

        LinuxError RecvMMsg(out int vlen, BsdMMsgHdr message, BsdSocketFlags flags, TimeVal timeout);

        LinuxError SendMMsg(out int vlen, BsdMMsgHdr message, BsdSocketFlags flags);

        LinuxError GetSocketOption(BsdSocketOption option, SocketOptionLevel level, Span<byte> optionValue);

        LinuxError SetSocketOption(BsdSocketOption option, SocketOptionLevel level, ReadOnlySpan<byte> optionValue);

        bool Poll(int microSeconds, SelectMode mode);

        LinuxError Bind(IPEndPoint localEndPoint);

        LinuxError Connect(IPEndPoint remoteEndPoint);

        LinuxError Listen(int backlog);

        LinuxError Accept(out ISocket newSocket);

        void Disconnect();

        LinuxError Shutdown(BsdSocketShutdownFlags how);

        void Close();
    }
}
