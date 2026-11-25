package com.ryandpi.android.ui

import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.size
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.StrokeJoin
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.sp

/**
 * Custom vector Black & Neon Red DPI Shield emblem matching RyanDPI's brand identity.
 */
@Composable
fun DpiShieldLogo(
    size: Dp,
    active: Boolean = false,
    pulsing: Boolean = false,
    modifier: Modifier = Modifier
) {
    val transition = rememberInfiniteTransition(label = "shield_glow")
    val pulseAlpha by transition.animateFloat(
        initialValue = if (pulsing) 0.25f else 0.16f,
        targetValue = if (pulsing) 0.55f else 0.30f,
        animationSpec = infiniteRepeatable(
            animation = tween(if (pulsing) 750 else 1800),
            repeatMode = RepeatMode.Reverse
        ),
        label = "glow_alpha"
    )

    val ringColor = if (active) Color(0xFF00E676) else Color(0xFFFF1E3C)
    val accentRed = Color(0xFFFF1E3C)
    val textSizeSp = (size.value * 0.27f).sp

    Box(
        modifier = modifier.size(size),
        contentAlignment = Alignment.Center
    ) {
        Canvas(modifier = Modifier.size(size)) {
            val w = this.size.width
            val h = this.size.height

            // Outer ambient radial glow
            drawCircle(
                brush = Brush.radialGradient(
                    colors = listOf(
                        ringColor.copy(alpha = pulseAlpha),
                        Color.Transparent
                    ),
                    center = Offset(w / 2f, h / 2f),
                    radius = w * 0.52f
                ),
                radius = w * 0.52f,
                center = Offset(w / 2f, h / 2f)
            )

            // Shield polygon path
            fun buildShieldPath(inset: Float): Path {
                val left = w * (0.14f + inset)
                val right = w * (0.86f - inset)
                val top = h * (0.08f + inset)
                val shoulderY = h * (0.23f + inset * 0.6f)
                val midY = h * 0.54f
                val bottom = h * (0.92f - inset)

                return Path().apply {
                    moveTo(w * 0.5f, top)
                    lineTo(right, shoulderY)
                    lineTo(right, midY)
                    cubicTo(
                        right, h * 0.74f,
                        w * 0.71f, h * 0.85f,
                        w * 0.5f, bottom
                    )
                    cubicTo(
                        w * 0.29f, h * 0.85f,
                        left, h * 0.74f,
                        left, midY
                    )
                    lineTo(left, shoulderY)
                    close()
                }
            }

            val shieldPath = buildShieldPath(0f)

            // Obsidian black shield body
            drawPath(
                path = shieldPath,
                brush = Brush.linearGradient(
                    colors = listOf(Color(0xFF141118), Color(0xFF07070A)),
                    start = Offset(0f, 0f),
                    end = Offset(w, h)
                )
            )

            // Crisp glowing Neon Red (or Emerald when active) shield border
            drawPath(
                path = shieldPath,
                color = ringColor,
                style = Stroke(width = w * 0.055f, join = StrokeJoin.Round)
            )

            // Subtle inner shield highlight stroke
            drawPath(
                path = buildShieldPath(0.065f),
                color = accentRed.copy(alpha = if (active) 0.35f else 0.42f),
                style = Stroke(width = w * 0.018f, join = StrokeJoin.Round)
            )
        }

        // Bold "DPI" centered inside the shield
        Text(
            text = "DPI",
            color = if (active) Color(0xFF00E676) else Color(0xFFFFFFFF),
            fontSize = textSizeSp,
            fontWeight = FontWeight.Black,
            letterSpacing = (size.value * 0.02f).sp
        )
    }
}
