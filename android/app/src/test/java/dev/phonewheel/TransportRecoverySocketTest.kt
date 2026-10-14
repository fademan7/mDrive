package dev.phonewheel

import org.junit.Assert.*
import org.junit.Test
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.SocketTimeoutException
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicInteger
import java.util.concurrent.atomic.AtomicReference
import kotlin.concurrent.thread

class TransportRecoverySocketTest {
    @Test fun stalledEndpointRecoversAndBackgroundNeverReplaysHeldInput() {
        val key=ByteArray(32); val session=1uL
        val running=AtomicBoolean(true);val failOldPort=AtomicBoolean(false)
        val firstPort=AtomicInteger();val latest=AtomicReference<ControlFrame?>()
        val peerError=AtomicReference<Throwable?>();val statuses=AtomicInteger()
        val server=DatagramSocket(0).apply {soTimeout=100}
        val worker=thread(isDaemon=true) {
            var sequence=0u;var clientSequence: UInt?=null
            try {while(running.get()) {
                try {
                    val packet=DatagramPacket(ByteArray(128),128);server.receive(packet)
                    firstPort.compareAndSet(0,packet.port)
                    val bytes=packet.data.copyOf(packet.length)
                    val control=if(Pwr1.peekKind(bytes)==PacketKind.HELLO)null else Pwr1.decodeControl(bytes,key,session)
                    val header=control?.header?:Pwr1.decodeHello(bytes,key,session)
                    clientSequence?.let {check(Pwr1.newer(header.sequence,it)){"Sequence reset after socket replacement"}}
                    clientSequence=header.sequence
                    if(failOldPort.get()&&packet.port==firstPort.get())continue
                    if(control!=null)latest.set(control)
                    val reply=Pwr1.encodeStatus(StatusFrame(Header(PacketKind.STATUS,session,++sequence,0u,header.sequence),0,3),key)
                    server.send(DatagramPacket(reply,reply.size,packet.socketAddress))
                }catch(_:SocketTimeoutException){}
            }}catch(t:Throwable){if(running.get())peerError.set(t)}
        }
        val client=UdpControllerClient("127.0.0.1",server.localPort,session,key,{statuses.incrementAndGet()},{},nowNanos={System.nanoTime()})
        fun await(label:String, condition:()->Boolean) {
            val end=System.nanoTime()+6_000_000_000
            while(!condition()&&System.nanoTime()<end&&peerError.get()==null)Thread.sleep(10)
            peerError.get()?.let {throw AssertionError(label,it)}
            assertTrue(label,condition())
        }
        try {
            await("initial authenticated status"){statuses.get()>3}
            client.update(ControllerSnapshot(Controls(.6f,.8f,.3f),true,true,true,true,1u,System.nanoTime()))
            await("held input reaches transport"){latest.get()?.controls?.throttle==.8f}
            // Same lifecycle contract as Activity.onPause: no held inputs in background.
            client.update(ControllerSnapshot(foreground=false,touchReady=true))
            failOldPort.set(true);latest.set(null)
            Thread.sleep(1100);client.resumeConnection()
            await("replaced socket authenticates without app restart"){latest.get()!=null}
            val background=latest.get()!!
            assertTrue(background.controls.isNeutral())
            assertEquals(0,background.flags.toInt() and (Pwr1.ARM or 2 or 4))
            val received=statuses.get()
            client.update(ControllerSnapshot(sensorValid=true,foreground=true,touchReady=true,arm=false,sensorTimestampNs=System.nanoTime()))
            await("fresh resume traffic"){statuses.get()>received+2}
            assertTrue(latest.get()!!.controls.isNeutral())
            assertEquals(0,latest.get()!!.flags.toInt() and Pwr1.ARM)
        }finally {client.close();running.set(false);server.close();worker.join(1000)}
    }
}
