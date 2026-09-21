package dev.phonewheel

import android.content.Context
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.graphics.Typeface
import android.view.View

internal data class FlagStyle(val color: Int, val label: String = "", val checkered: Boolean = false) {
    companion object {
        fun forFlag(flag: String): FlagStyle = when (flag) {
            "GREEN" -> FlagStyle(0xFF20C461.toInt())
            "YELLOW" -> FlagStyle(0xFFFFD632.toInt())
            "RED" -> FlagStyle(0xFFEF3535.toInt())
            "BLUE" -> FlagStyle(0xFF268AFF.toInt())
            "SC" -> FlagStyle(0xFFFFD632.toInt(), "SC")
            "VSC" -> FlagStyle(0xFFFFD632.toInt(), "VSC")
            "CHECKERED" -> FlagStyle(0xFFFFFFFF.toInt(), checkered = true)
            else -> FlagStyle(0xFF303B45.toInt(), "—")
        }
    }
}

internal class FlagBadgeView(context: Context) : View(context) {
    private val paint = Paint(Paint.ANTI_ALIAS_FLAG)
    private var flag = "UNKNOWN"
    private var style = FlagStyle.forFlag(flag)

    init {
        contentDescription = "Flag unavailable"
        importantForAccessibility = IMPORTANT_FOR_ACCESSIBILITY_YES
        accessibilityLiveRegion = ACCESSIBILITY_LIVE_REGION_POLITE
    }

    fun setFlag(value: String) {
        if (flag == value) return
        flag = value; style = FlagStyle.forFlag(value)
        contentDescription = if (style.label == "—") "Flag unavailable" else "Flag $value"
        invalidate()
    }

    override fun onDraw(canvas: Canvas) {
        super.onDraw(canvas)
        val side = minOf(width, height).toFloat()
        val left = (width - side) / 2f; val top = (height - side) / 2f
        paint.color = style.color
        canvas.drawRect(left, top, left + side, top + side, paint)
        if (style.checkered) {
            paint.color = Color.BLACK
            val cell = side / 4f
            for (row in 0..3) for (column in 0..3) if ((row + column) % 2 == 0)
                canvas.drawRect(left + column * cell, top + row * cell, left + (column + 1) * cell, top + (row + 1) * cell, paint)
        } else if (style.label.isNotEmpty()) {
            paint.color = if (style.label == "—") Color.LTGRAY else Color.BLACK
            paint.textAlign = Paint.Align.CENTER; paint.textSize = side * .30f
            paint.typeface = Typeface.DEFAULT_BOLD
            canvas.drawText(style.label, width / 2f, height / 2f - (paint.ascent() + paint.descent()) / 2f, paint)
        }
    }
}
