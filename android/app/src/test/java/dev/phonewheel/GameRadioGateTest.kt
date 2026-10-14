package dev.phonewheel

import org.junit.Assert.*
import org.junit.Test

class GameRadioGateTest {
    @Test fun holdAndReleaseGrace() {
        val gate=GameRadioGate()
        assertFalse(gate.blocked(0))
        gate.edge(true,100)
        assertTrue(gate.blocked(999999))
        gate.edge(false,1000000)
        assertTrue(gate.blocked(1007999))
        assertFalse(gate.blocked(1008000))
    }
    @Test fun repeatedEdgesDoNotShortenOrExtendWindow() {
        val gate=GameRadioGate()
        gate.edge(false,1); assertFalse(gate.blocked(1))
        gate.edge(true,10); gate.edge(true,20); gate.edge(false,100)
        gate.edge(false,200)
        assertFalse(gate.blocked(8100))
        gate.edge(true,9000); gate.edge(false,10000)
        gate.edge(true,11000); gate.edge(false,12000)
        assertTrue(gate.blocked(19999)); assertFalse(gate.blocked(20000))
    }
    @Test fun unrelatedGamepadButtonsNeverTriggerRadio() {
        val edges=ButtonPtt(); val gate=GameRadioGate()
        edges.update(0x1000u)?.let { gate.edge(it,0) }
        assertFalse(gate.blocked(0))
        edges.update(0x1100u)?.let { gate.edge(it,1) }; assertTrue(gate.blocked(1))
        edges.update(0x8100u)?.let { gate.edge(it,2) }; assertTrue(gate.blocked(2))
        edges.update(0x8000u)?.let { gate.edge(it,3) }
        assertTrue(gate.blocked(8002)); assertFalse(gate.blocked(8003))
    }
}
