package dev.phonewheel

import java.io.DataInputStream
import java.io.DataOutputStream
import java.io.IOException
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.net.InetSocketAddress
import java.net.Socket

internal interface PacketTransport : AutoCloseable {
    fun send(packet: ByteArray)
    fun receive(): ByteArray
}

internal class WifiPacketTransport(address: InetAddress, destinationPort: Int) : PacketTransport {
    private val socket = DatagramSocket().also { udp ->
        try { udp.connect(address, destinationPort); udp.soTimeout = 100 }
        catch (e: Exception) { udp.close(); throw e }
    }
    override fun send(packet: ByteArray) = socket.send(DatagramPacket(packet, packet.size))
    override fun receive(): ByteArray {
        val packet = DatagramPacket(ByteArray(128), 128)
        socket.receive(packet)
        return packet.data.copyOf(packet.length)
    }
    override fun close() = socket.close()
}

// ADB reverse maps the phone's loopback socket to the PC over USB, not Wi-Fi.
internal class UsbPacketTransport(destinationPort: Int) : PacketTransport {
    private val socket = Socket().also { tcp ->
        try { tcp.tcpNoDelay = true; tcp.soTimeout = 1000; tcp.connect(InetSocketAddress("127.0.0.1", destinationPort), 3000) }
        catch (e: Exception) { tcp.close(); throw e }
    }
    private val input = DataInputStream(socket.getInputStream())
    private val output = DataOutputStream(socket.getOutputStream())
    override fun send(packet: ByteArray) {
        require(packet.size in 48..76)
        output.writeShort(packet.size); output.write(packet); output.flush()
    }
    override fun receive(): ByteArray {
        val length = input.readUnsignedShort()
        if (length !in 48..76) throw IOException("Invalid USB frame length")
        return ByteArray(length).also { input.readFully(it) }
    }
    override fun close() = socket.close()
}
