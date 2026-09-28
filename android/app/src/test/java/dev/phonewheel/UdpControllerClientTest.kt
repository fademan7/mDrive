package dev.phonewheel

import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import org.junit.Assert.assertEquals
import org.junit.Test
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import org.junit.Assert.assertTrue

class UdpControllerClientTest {
    @Test fun sensorPublishedDuringClockReadDoesNotBecomeFalselyInvalid() {
        val key = ByteArray(32) { it.toByte() }; val session = 654uL
        val clientRef = java.util.concurrent.atomic.AtomicReference<UdpControllerClient?>()
        val paired = java.util.concurrent.atomic.AtomicBoolean(false)
        DatagramSocket(0, InetAddress.getLoopbackAddress()).use { server ->
            server.soTimeout = 3000
            UdpControllerClient("127.0.0.1", server.localPort, session, key, { paired.set(true) }, {}, {}, {
                val sampledNow = System.nanoTime()
                // Deterministically emulate the sensor publishing while the sender
                // is preempted immediately after sampling its clock.
                if (paired.get()) clientRef.get()?.update(ControllerSnapshot(sensorValid = true,
                    foreground = true, touchReady = true, arm = true, sensorTimestampNs = System.nanoTime()))
                sampledNow
            }).use { client ->
                clientRef.set(client)
                client.update(ControllerSnapshot(sensorValid = true, foreground = true,
                    touchReady = true, arm = true, sensorTimestampNs = System.nanoTime()))
                fun receive() = DatagramPacket(ByteArray(128), 128).also { server.receive(it) }
                val hello = receive()
                val response = Pwr1.encodeStatus(StatusFrame(Header(PacketKind.STATUS, session, 1u, 1uL, 1u), 1, 0), key)
                server.send(DatagramPacket(response, response.size, hello.socketAddress))
                repeat(5) {
                    val packet = receive()
                    val frame = Pwr1.decodeControl(packet.data.copyOf(packet.length), key, session)
                    assertEquals("A newer valid sensor sample must not look like a future/invalid timestamp", 31, frame.flags.toInt())
                }
            }
        }
    }
    @Test fun wifiMissingRepliesRetriesHelloWithoutResettingSequence() = verifyWifiRecovery(false)
    @Test fun callbackFailureCannotKillScheduledSender() = verifyWifiRecovery(true)
    private fun verifyWifiRecovery(throwCallback: Boolean) {
        val key = ByteArray(32) { it.toByte() }; val session = 321uL
        val lost = CountDownLatch(1)
        DatagramSocket(0, InetAddress.getLoopbackAddress()).use { server ->
            server.soTimeout = 4000
            UdpControllerClient("127.0.0.1", server.localPort, session, key, {}, {},
                { lost.countDown(); if (throwCallback) error("Synthetic optional callback failure") }, { System.nanoTime() }).use { client ->
                fun receive(): DatagramPacket = DatagramPacket(ByteArray(128), 128).also { server.receive(it) }
                val first = receive()
                val hello = Pwr1.decodeHello(first.data.copyOf(first.length), key, session)
                val status = Pwr1.encodeStatus(StatusFrame(Header(PacketKind.STATUS, session, 1u, 1uL, hello.sequence), 0, 0), key)
                server.send(DatagramPacket(status, status.size, first.socketAddress))
                val deadline = System.nanoTime() + 3_000_000_000L
                var retried = false
                while (System.nanoTime() < deadline) {
                    val packet = receive(); val bytes = packet.data.copyOf(packet.length)
                    if (Pwr1.peekKind(bytes) == PacketKind.HELLO) {
                        assertTrue(Pwr1.newer(Pwr1.decodeHello(bytes, key, session).sequence, hello.sequence))
                        retried = true; break
                    }
                }
                assertTrue(retried); assertTrue(lost.await(1, TimeUnit.SECONDS))
                if (throwCallback) assertEquals(1, client.sendFailures)
            }
        }
    }

    @Test fun sendsAuthenticatedHelloToConfiguredPort() {
        val loopback = InetAddress.getByName("127.0.0.1")
        val key = ByteArray(32) { it.toByte() }
        val session = 0x0102030405060708uL
        DatagramSocket(0, loopback).use { receiver ->
            receiver.soTimeout = 3000
            UdpControllerClient("127.0.0.1", receiver.localPort, session, key, {}, {}).use {
                val packet = DatagramPacket(ByteArray(128), 128)
                receiver.receive(packet)
                val hello = Pwr1.decodeHello(packet.data.copyOf(packet.length), key, session)
                assertEquals(session, hello.session)
            }
        }
    }
}
