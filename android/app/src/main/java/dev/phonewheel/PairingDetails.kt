package dev.phonewheel

import java.net.URI
import java.util.Base64

// Strict, bounded, local-only payload. Never log a QR: it contains the session key.
data class PairingDetails(val host: String, val port: Int, val session: ULong, val keyBase64: String, val usb: Boolean = false) {
    companion object {
        fun parse(text: String): PairingDetails {
            require(text.length in 1..512) { "Invalid pairing QR" }
            val uri = URI(text)
            require(uri.scheme == "phonewheel" && uri.rawAuthority in setOf("pair", "usb") && uri.rawPath.isNullOrEmpty() && uri.rawFragment == null)
            val usb = uri.rawAuthority == "usb"
            val entries = (uri.rawQuery ?: error("Missing pairing data")).split('&').map {
                val pair = it.split('=', limit = 2); require(pair.size == 2); pair[0] to pair[1]
            }
            require(entries.size == 5 && entries.map { it.first }.toSet() == setOf("v", "host", "port", "session", "key"))
            val values = entries.toMap()
            require(values.getValue("v") == "1")
            val host = values.getValue("host")
            val octets = host.split('.')
            require(octets.size == 4 && octets.all { it.matches(Regex("0|[1-9][0-9]{0,2}")) && it.toInt() in 0..255 })
            if (usb) require(host == "127.0.0.1")
            else require(octets[0].toInt() in 1..223 && octets[0] != "127")
            val portText = values.getValue("port"); require(portText.matches(Regex("[0-9]{1,5}")))
            val port = portText.toInt(); require(port in 1..65535)
            val sessionText = values.getValue("session"); require(sessionText.matches(Regex("[0-9a-fA-F]{16}")))
            val session = sessionText.toULong(16); require(session != 0uL)
            val encodedKey = values.getValue("key"); require(encodedKey.matches(Regex("[A-Za-z0-9_-]{43}")))
            val key = Base64.getUrlDecoder().decode(encodedKey); require(key.size == 32)
            require(Base64.getUrlEncoder().withoutPadding().encodeToString(key) == encodedKey)
            return PairingDetails(host, port, session, Base64.getEncoder().encodeToString(key), usb)
        }
    }
}
