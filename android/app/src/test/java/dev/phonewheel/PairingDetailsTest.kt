package dev.phonewheel

import org.junit.Assert.*
import org.junit.Test

class PairingDetailsTest {
    private val fixture = "phonewheel://pair?v=1&host=192.168.1.25&port=26760&session=0102030405060708&key=AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8"

    @Test fun usbPairingIsLoopbackOnly() {
        val usb = fixture.replace("://pair?", "://usb?").replace("192.168.1.25", "127.0.0.1")
        assertTrue(PairingDetails.parse(usb).usb)
        assertFalse(PairingDetails.parse(fixture).usb)
        assertThrows(IllegalArgumentException::class.java) { PairingDetails.parse(usb.replace("127.0.0.1", "192.168.1.25")) }
    }

    @Test fun readsWindowsFixture() {
        val p = PairingDetails.parse(fixture)
        assertEquals("192.168.1.25", p.host); assertEquals(26760, p.port)
        assertEquals(0x0102030405060708uL, p.session)
        assertEquals("AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=", p.keyBase64)
        assertEquals(12345, PairingDetails.parse(fixture.replace("26760", "12345")).port)
    }

    @Test fun rejectsUnrelatedOrMalformedQr() {
        val invalid = listOf("https://example.com", fixture + "&v=1", fixture + "#fragment",
            fixture.replace("v=1", "v=2"), fixture.replace("26760", "0"), fixture.replace("26760", "65536"),
            fixture.replace("192.168.1.25", "example.com"), fixture.replace("192.168.1.25", "999.1.1.1"),
            fixture.replace("192.168.1.25", "127.0.0.1"), fixture.replace("0102030405060708", "0000000000000000"),
            fixture.replace("Hh8", "Hh9"), fixture.replace("&key=", "&extra="), "x".repeat(513))
        invalid.forEach { value ->
            try { PairingDetails.parse(value); fail("Accepted invalid QR") } catch (_: IllegalArgumentException) { }
        }
    }
}
