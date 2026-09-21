package dev.phonewheel

import java.nio.ByteBuffer
import java.nio.ByteOrder
import java.security.MessageDigest
import javax.crypto.Mac
import javax.crypto.spec.SecretKeySpec

internal object WendyWire {
    const val MAX_JSON = 2048
    fun encode(json: ByteArray, key: ByteArray, nonce: ByteArray, direction: Int, sequence: Long): ByteArray {
        require(json.size in 2..MAX_JSON && key.size == 32 && nonce.size == 32 && direction in 0..1 && sequence > 0)
        val body = ByteBuffer.allocate(8 + json.size).order(ByteOrder.BIG_ENDIAN).putLong(sequence).put(json).array()
        return body + tag(body, key, nonce, direction)
    }
    fun decode(packet: ByteArray, key: ByteArray, nonce: ByteArray, direction: Int, expected: Long): ByteArray {
        require(packet.size in 42..MAX_JSON + 40 && key.size == 32 && nonce.size == 32 && direction in 0..1 && expected > 0)
        val body = packet.copyOfRange(0, packet.size - 32)
        require(MessageDigest.isEqual(packet.copyOfRange(body.size, packet.size), tag(body, key, nonce, direction)))
        require(ByteBuffer.wrap(body).order(ByteOrder.BIG_ENDIAN).long == expected)
        return body.copyOfRange(8, body.size)
    }
    private fun tag(body: ByteArray, key: ByteArray, nonce: ByteArray, direction: Int): ByteArray {
        val mac = Mac.getInstance("HmacSHA256"); mac.init(SecretKeySpec(key, "HmacSHA256"))
        return mac.doFinal("WDY1".toByteArray(Charsets.US_ASCII) + nonce + byteArrayOf(direction.toByte()) + body)
    }
}
