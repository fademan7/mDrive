package dev.phonewheel

import org.junit.Test
import org.junit.Assert.*
import java.nio.file.Files
import java.util.concurrent.atomic.AtomicLong

class ControllerDropoutTest {
    @Test fun duplicateSensorPreviouslyClearedCalibrationButNowIsDiscarded() {
        val q = Quaternion(1.0, 0.0, 0.0, 0.0)
        val old = SteeringEstimator(); old.calibrate(q); old.update(q, 100_000_000)
        assertThrows(IllegalStateException::class.java) { old.update(q, 100_000_000) }
        assertThrows(IllegalArgumentException::class.java) { old.update(q, 105_000_000) }
        val fixed = SteeringEstimator(); fixed.calibrate(q); fixed.update(q, 100_000_000)
        var last = 100_000_000L
        for (sample in listOf(100_000_000L, 99_000_000L, 105_000_000L)) {
            if (SensorTimestampGate.rejection(sample, last, 106_000_000) == 0) { fixed.update(q, sample); last = sample }
        }
        assertEquals(105_000_000L, last)
        assertEquals(0f, fixed.update(q, 110_000_000), 0f)
    }
    @Test fun rejectedSensorSamplesCannotRefreshFreshnessAndLongGapStillFails() {
        val last = 100_000_000L
        assertEquals(1, SensorTimestampGate.rejection(last, last, 250_000_000))
        assertEquals(2, SensorTimestampGate.rejection(300_000_000, last, 250_000_000))
        assertEquals(3, SensorTimestampGate.rejection(110_000_000, last, 250_000_000))
        val q = Quaternion(1.0, 0.0, 0.0, 0.0); val e = SteeringEstimator(); e.calibrate(q); e.update(q, last)
        assertThrows(IllegalStateException::class.java) { e.update(q, 250_000_000) }
    }
    @Test fun recorderWritesOnlyIncidentAndKeepsPreAndPostWindow() {
        val parent = Files.createTempDirectory("mdrive-flight-test").toFile()
        val dir = java.io.File(parent, "incidents"); val now = AtomicLong(0)
        val recorder = ControllerFlightRecorder(dir) { now.get() }
        try {
            for (i in 0..12) { now.set(i * 1000L); recorder.record(FlightEntry(FlightEvent.SEND, sequence = i.toLong())) }
            Thread.sleep(150); assertFalse(dir.exists())
            recorder.record(FlightEntry(FlightEvent.RELEASE, reason = 1), true)
            now.set(14000); recorder.record(FlightEntry(FlightEvent.RECONNECT))
            now.set(15000); recorder.record(FlightEntry(FlightEvent.ARM))
            repeat(100) { if (recorder.lastFile == null) Thread.sleep(20) }
            val text = java.io.File(dir, requireNotNull(recorder.lastFile)).readText()
            assertTrue(text.contains("RECONNECT")); assertTrue(text.contains("RELEASE"))
            assertFalse(text.lineSequence().any { it.startsWith("0,SEND,") || it.startsWith("1000,SEND,") })
            assertEquals(0, recorder.saveFailures.get())
        } finally { recorder.close() }
    }
}
