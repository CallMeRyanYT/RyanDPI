package com.ryandpi.android.engine

import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update

enum class VpnStatus {
    DISCONNECTED,
    CONNECTING,
    CONNECTED,
    ERROR
}

data class VpnUiState(
    val status: VpnStatus = VpnStatus.DISCONNECTED,
    val connectedSinceMs: Long = 0L,
    val txBytes: Long = 0L,
    val rxBytes: Long = 0L,
    val txBytesPerSec: Long = 0L,
    val rxBytesPerSec: Long = 0L,
    val dnsQueries: Long = 0L,
    val dnsCacheHits: Long = 0L,
    val activeDnsMode: DnsMode = DnsMode.ENCRYPTED_DOH,
    val activeProfile: BypassProfile = BypassProfile.AUTO_UNIVERSAL,
    val errorMessage: String? = null
)

object VpnStateHolder {
    private val _state = MutableStateFlow(VpnUiState())
    val state: StateFlow<VpnUiState> = _state.asStateFlow()

    fun setConnecting(dnsMode: DnsMode, profile: BypassProfile) {
        _state.value = VpnUiState(
            status = VpnStatus.CONNECTING,
            activeDnsMode = dnsMode,
            activeProfile = profile
        )
    }

    fun setConnected(dnsMode: DnsMode, profile: BypassProfile) {
        _state.value = VpnUiState(
            status = VpnStatus.CONNECTED,
            connectedSinceMs = System.currentTimeMillis(),
            activeDnsMode = dnsMode,
            activeProfile = profile
        )
    }

    fun setDisconnected() {
        _state.value = VpnUiState(status = VpnStatus.DISCONNECTED)
    }

    fun setError(message: String) {
        _state.value = VpnUiState(
            status = VpnStatus.ERROR,
            errorMessage = message
        )
    }

    fun updateStats(txBytes: Long, rxBytes: Long, dnsQueries: Long, dnsCacheHits: Long) {
        _state.update { prev ->
            if (prev.status != VpnStatus.CONNECTED) return@update prev
            val dTx = (txBytes - prev.txBytes).coerceAtLeast(0L)
            val dRx = (rxBytes - prev.rxBytes).coerceAtLeast(0L)
            prev.copy(
                txBytes = txBytes,
                rxBytes = rxBytes,
                txBytesPerSec = dTx,
                rxBytesPerSec = dRx,
                dnsQueries = dnsQueries,
                dnsCacheHits = dnsCacheHits
            )
        }
    }
}
