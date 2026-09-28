package dev.phonewheel

import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress

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
