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
    val rxSpeedHistory: List<Long> = emptyList(),
    val txSpeedHistory: List<Long> = emptyList(),
    val dnsQueries: Long = 0L,
    val dnsCacheHits: Long = 0L,
    val networkLabel: String = "",
    val activeDnsMode: DnsMode = DnsMode.ENCRYPTED_DOH,
    val activeProfile: BypassProfile = BypassProfile.AUTO_UNIVERSAL,
    val errorMessage: String? = null
)

object VpnStateHolder {
    private const val MAX_SPARKLINE_POINTS = 20
    private val _state = MutableStateFlow(VpnUiState())
    val state: StateFlow<VpnUiState> = _state.asStateFlow()

    fun setConnecting(dnsMode: DnsMode, profile: BypassProfile) {
        _state.value = VpnUiState(
            status = VpnStatus.CONNECTING,
            activeDnsMode = dnsMode,
            activeProfile = profile
        )
    }

    fun setConnected(dnsMode: DnsMode, profile: BypassProfile, networkLabel: String = "") {
        _state.value = VpnUiState(
            status = VpnStatus.CONNECTED,
            connectedSinceMs = System.currentTimeMillis(),
            networkLabel = networkLabel,
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

    fun updateNetworkLabel(label: String) {
        _state.update { prev ->
            if (prev.status != VpnStatus.CONNECTED || prev.networkLabel == label) prev
            else prev.copy(networkLabel = label)
        }
    }

    fun updateStats(
        txBytes: Long,
        rxBytes: Long,
        txBytesPerSec: Long,
        rxBytesPerSec: Long,
        dnsQueries: Long,
        dnsCacheHits: Long
    ) {
        _state.update { prev ->
            if (prev.status != VpnStatus.CONNECTED) return@update prev
            val nextRxHist = (prev.rxSpeedHistory + rxBytesPerSec).takeLast(MAX_SPARKLINE_POINTS)
            val nextTxHist = (prev.txSpeedHistory + txBytesPerSec).takeLast(MAX_SPARKLINE_POINTS)
            prev.copy(
                txBytes = txBytes,
                rxBytes = rxBytes,
                txBytesPerSec = txBytesPerSec,
                rxBytesPerSec = rxBytesPerSec,
                rxSpeedHistory = nextRxHist,
                txSpeedHistory = nextTxHist,
                dnsQueries = dnsQueries,
                dnsCacheHits = dnsCacheHits
            )
        }
    }
}
