package com.ryandpi.android.ui

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.ui.graphics.Color

data class RyanDpiColors(
    val bgCanvas: Color,
    val bgCard: Color,
    val bgCardAlt: Color,
    val bgInput: Color,
    val borderSubtle: Color,
    val borderActive: Color,
    val textPrimary: Color,
    val textSecondary: Color,
    val textMuted: Color,
    val accentRed: Color,
    val accentRedDeep: Color,
    val success: Color,
    val successBg: Color,
    val warning: Color,
    val warningBg: Color,
    val danger: Color,
    val dangerBg: Color,
    val isDark: Boolean
)

// Exact match to Windows src/RyanDPI/Themes/DarkTheme.xaml (Pitch-Black & Neon Red)
val NeonRedDarkPalette = RyanDpiColors(
    bgCanvas = Color(0xFF060608),
    bgCard = Color(0xFF0D0D11),
    bgCardAlt = Color(0xFF151116),
    bgInput = Color(0xFF09080B),
    borderSubtle = Color(0xFF2C141B),
    borderActive = Color(0xFFFF1E3C),
    textPrimary = Color(0xFFFAFAFC),
    textSecondary = Color(0xFFA69CA6),
    textMuted = Color(0xFF6E626C),
    accentRed = Color(0xFFFF1E3C),
    accentRedDeep = Color(0xFFC9002B),
    success = Color(0xFF00E676),
    successBg = Color(0xFF082417),
    warning = Color(0xFFFFB020),
    warningBg = Color(0xFF261A08),
    danger = Color(0xFFFF2E4D),
    dangerBg = Color(0xFF290A12),
    isDark = true
)

// Exact match to Windows src/RyanDPI/Themes/LightTheme.xaml (Crisp White & Crimson)
val CrimsonLightPalette = RyanDpiColors(
    bgCanvas = Color(0xFFF5F2F3),
    bgCard = Color(0xFFFFFFFF),
    bgCardAlt = Color(0xFFFAF5F6),
    bgInput = Color(0xFFF3EEF0),
    borderSubtle = Color(0xFFE4D4D9),
    borderActive = Color(0xFFE61535),
    textPrimary = Color(0xFF110C0E),
    textSecondary = Color(0xFF52474B),
    textMuted = Color(0xFF7D6E74),
    accentRed = Color(0xFFE61535),
    accentRedDeep = Color(0xFFA30824),
    success = Color(0xFF059669),
    successBg = Color(0xFFDCFCE7),
    warning = Color(0xFFD97706),
    warningBg = Color(0xFFFEF3C7),
    danger = Color(0xFFDC2626),
    dangerBg = Color(0xFFFEE2E2),
    isDark = false
)

val LocalRyanDpiColors = staticCompositionLocalOf { NeonRedDarkPalette }

@Composable
fun RyanDpiTheme(
    darkTheme: Boolean,
    content: @Composable () -> Unit
) {
    val palette = if (darkTheme) NeonRedDarkPalette else CrimsonLightPalette
    val materialScheme = if (darkTheme) {
        darkColorScheme(
            primary = palette.accentRed,
            onPrimary = Color.White,
            secondary = palette.accentRedDeep,
            background = palette.bgCanvas,
            surface = palette.bgCard,
            surfaceVariant = palette.bgCardAlt,
            onBackground = palette.textPrimary,
            onSurface = palette.textPrimary,
            onSurfaceVariant = palette.textSecondary,
            outline = palette.borderSubtle,
            error = palette.danger
        )
    } else {
        lightColorScheme(
            primary = palette.accentRed,
            onPrimary = Color.White,
            secondary = palette.accentRedDeep,
            background = palette.bgCanvas,
            surface = palette.bgCard,
            surfaceVariant = palette.bgCardAlt,
            onBackground = palette.textPrimary,
            onSurface = palette.textPrimary,
            onSurfaceVariant = palette.textSecondary,
            outline = palette.borderSubtle,
            error = palette.danger
        )
    }

    CompositionLocalProvider(LocalRyanDpiColors provides palette) {
        MaterialTheme(
            colorScheme = materialScheme,
            content = content
        )
    }
}
