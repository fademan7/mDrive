package dev.phonewheel

import android.content.Context
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.graphics.PointF
import android.graphics.RectF
import android.util.TypedValue
import android.view.MotionEvent
import android.view.View
import org.json.JSONArray
import org.json.JSONObject
import kotlin.math.max
import kotlin.math.roundToInt

internal object PedalResponse {
    const val EDGE_MARGIN = .10f

    fun valueAt(y: Float, height: Float): Float {
        require(height.isFinite() && height > 0f && y.isFinite())
        val h = height.toDouble()
        return ((h * (1.0 - EDGE_MARGIN) - y) / (h * (1.0 - 2.0 * EDGE_MARGIN))).coerceIn(0.0, 1.0).toFloat()
    }

    fun configure(pedal: Pedal, height: Int) {
        // Both pedals share the explicit, user-selected 10% endpoint margins.
        pedal.setTravel(max(1, height).toFloat())
    }
}

class Pedal(travelPx: Float) {
    private var travelPx = travelPx.also { require(it > 0f) }
    var pointerId: Int? = null; private set
    var value = 0f; private set
    fun setTravel(value: Float) {
        require(value.isFinite() && value > 0f)
        travelPx = value
    }
    fun down(id: Int, y: Float): Boolean {
        if (pointerId != null || !y.isFinite()) return false
        pointerId = id; move(id, y)
        return true
    }
    fun move(id: Int, y: Float): Float {
        if (id == pointerId && y.isFinite()) {
            value = PedalResponse.valueAt(y, travelPx)
        }
        return value
    }
    fun up(id: Int) { if (id == pointerId) cancel() }
    fun cancel() { pointerId = null; value = 0f }
}

private enum class ControlKind { BRAKE, THROTTLE, BUTTON }

private data class ControlBox(
    val id: String,
    val label: String,
    val kind: ControlKind,
    val bit: Int = 0,
    var x: Float,
    var y: Float,
    var w: Float,
    var h: Float
) {
    fun rect(width: Int, height: Int) = RectF(x * width, y * height, (x + w) * width, (y + h) * height)
    fun clamp(minX: Float = .202f, maxX: Float = .798f) {
        if (kind != ControlKind.BUTTON) {
            w = w.coerceIn(.20f, .25f); h = 1f; y = 0f
            x = if (kind == ControlKind.BRAKE) 0f else 1f - w
        } else {
            w = w.coerceIn(.045f, .20f); h = h.coerceIn(.07f, .22f)
            x = x.coerceIn(minX, maxX - w); y = y.coerceIn(.29f, 1f - h)
        }
    }
}

class PedalTouchView(context: Context) : View(context) {
    companion object {
        private const val PREFS = "phonewheel_controls"
        private const val LAYOUT_KEY = "layout_v3_edge_pedals"
        private fun defaults() = mutableListOf(
            ControlBox("brake", "Brake", ControlKind.BRAKE, x = 0f, y = 0f, w = .25f, h = 1f),
            ControlBox("throttle", "Throttle", ControlKind.THROTTLE, x = .75f, y = 0f, w = .25f, h = 1f),
            ControlBox("lb", "LB", ControlKind.BUTTON, 0x0100, .275f, .29f, .075f, .19f),
            ControlBox("rb", "RB", ControlKind.BUTTON, 0x0200, .65f, .29f, .075f, .19f),
            ControlBox("back", "BACK", ControlKind.BUTTON, 0x0020, .40f, .29f, .075f, .19f),
            ControlBox("start", "START", ControlKind.BUTTON, 0x0010, .525f, .29f, .075f, .19f),
            ControlBox("dleft", "◀", ControlKind.BUTTON, 0x0004, .26f, .66f, .065f, .16f),
            ControlBox("dup", "▲", ControlKind.BUTTON, 0x0001, .325f, .50f, .065f, .16f),
            ControlBox("ddown", "▼", ControlKind.BUTTON, 0x0002, .325f, .82f, .065f, .16f),
            ControlBox("dright", "▶", ControlKind.BUTTON, 0x0008, .39f, .66f, .065f, .16f),
            ControlBox("x", "X", ControlKind.BUTTON, 0x4000, .545f, .66f, .065f, .16f),
            ControlBox("y", "Y", ControlKind.BUTTON, 0x8000, .61f, .50f, .065f, .16f),
            ControlBox("a", "A", ControlKind.BUTTON, 0x1000, .61f, .82f, .065f, .16f),
            ControlBox("b", "B", ControlKind.BUTTON, 0x2000, .675f, .66f, .065f, .16f)
        )
    }

    private val brake = Pedal(72f * resources.displayMetrics.density)
    private val throttle = Pedal(72f * resources.displayMetrics.density)
    private val paint = Paint(Paint.ANTI_ALIAS_FLAG)
    private val prefs = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
    private var boxes = loadLayout()
    private val buttonPointers = mutableMapOf<Int, Int>()
    private val touchPoints = mutableMapOf<Int, PointF>()
    private val lookStick = LookStickPreview()
    val lookX get() = lookStick.x
    val lookY get() = -lookStick.y // Android down is positive; XInput up is positive.
    val hasActiveTouch get() = touchPoints.isNotEmpty()
    private var editPointer: Int? = null
    private var dragOffsetX = 0f
    private var dragOffsetY = 0f
    private var selectedId: String? = null
    private var steering = 0f
    private var steeringAngle = 0.0
    var editMode = false; private set
    var onValues: ((brake: Float, throttle: Float, buttons: UShort) -> Unit)? = null
    var onSelectionChanged: ((String?) -> Unit)? = null

    init { setBackgroundColor(Color.rgb(12, 16, 20)) }

    private fun configurePedals() {
        PedalResponse.configure(brake, height)
        PedalResponse.configure(throttle, height)
    }

    override fun onSizeChanged(w: Int, h: Int, oldw: Int, oldh: Int) {
        super.onSizeChanged(w, h, oldw, oldh)
        cancelAll(); configurePedals()
        if (!prefs.getBoolean("compact_square_v4", false)) {
            // Preserve the user's edge-pedal widths and keep their old button
            // arrangement recoverable before applying the requested new layout.
            val previous = prefs.getString(LAYOUT_KEY, null)
            if (previous != null) prefs.edit().putString("layout_before_compact_v4", previous).apply()
            val fresh = defaults()
            boxes.filter { it.kind == ControlKind.BUTTON }.forEach { box ->
                val replacement = fresh.first { it.id == box.id }
                box.x = replacement.x; box.y = replacement.y; box.w = replacement.w; box.h = replacement.h
            }
            squareDefaultButtons(); saveLayout(); prefs.edit().putBoolean("compact_square_v4", true).apply()
        }
        if (!prefs.getBoolean("look_layout_v5", false)) {
            prefs.getString(LAYOUT_KEY, null)?.let { prefs.edit().putString("layout_before_look_v5", it).apply() }
            val changedIds = setOf("dleft", "dup", "ddown", "dright", "x", "y", "a", "b")
            val fresh = defaults()
            boxes.filter { it.id in changedIds }.forEach { box ->
                val replacement = fresh.first { it.id == box.id }
                box.x = replacement.x; box.y = replacement.y; box.w = replacement.w
                box.h = (box.w * w / max(1, h)).coerceAtMost(.22f); clampBox(box)
            }
            saveLayout(); prefs.edit().putBoolean("look_layout_v5", true).apply()
        }
        if (!prefs.getBoolean("look_spacing_v6", false)) {
            prefs.getString(LAYOUT_KEY, null)?.let { prefs.edit().putString("layout_before_spacing_v6", it).apply() }
            spreadButtonGroups()
            saveLayout(); prefs.edit().putBoolean("look_spacing_v6", true).apply()
        }
    }

    private fun clampBox(box: ControlBox) {
        box.clamp(boxes.first { it.kind == ControlKind.BRAKE }.w + ButtonGroupSpacing.PEDAL_GAP,
            1f - boxes.first { it.kind == ControlKind.THROTTLE }.w - ButtonGroupSpacing.PEDAL_GAP)
    }

    private fun spreadButtonGroups() {
        for (left in listOf(true, false)) {
            val ids = if (left) ButtonGroupSpacing.leftIds else ButtonGroupSpacing.rightIds
            val group = boxes.filter { it.id in ids }
            val pedalWidth = boxes.first { it.kind == if (left) ControlKind.BRAKE else ControlKind.THROTTLE }.w
            val shift = ButtonGroupSpacing.shift(left, group.minOf { it.x }, group.maxOf { it.x + it.w }, pedalWidth)
            group.forEach { it.x += shift }
        }
    }

    private fun squareDefaultButtons() {
        if (height <= 0) return
        boxes.filter { it.kind == ControlKind.BUTTON }.forEach {
            it.h = (it.w * width / height).coerceAtMost(.22f); clampBox(it)
        }
    }

    fun setSteering(value: Float, angle: Double = 0.0) {
        steering = value.coerceIn(-1f, 1f); steeringAngle = angle; postInvalidateOnAnimation()
    }

    fun setEditMode(enabled: Boolean) {
        if (editMode == enabled) return
        cancelAll(); editMode = enabled; editPointer = null
        if (!enabled) { saveLayout(); selectedId = null; onSelectionChanged?.invoke(null) }
        invalidate()
    }

    fun resizeSelected(factor: Float): Boolean {
        val box = boxes.firstOrNull { it.id == selectedId } ?: return false
        val cx = box.x + box.w / 2f; val cy = box.y + box.h / 2f
        box.w *= factor; if (box.kind == ControlKind.BUTTON) box.h *= factor
        box.x = cx - box.w / 2f; box.y = cy - box.h / 2f; clampBox(box)
        if (box.kind != ControlKind.BUTTON) boxes.filter { it.kind == ControlKind.BUTTON }.forEach(::clampBox)
        saveLayout(); invalidate(); return true
    }

    fun resetLayout() {
        boxes = defaults(); squareDefaultButtons(); spreadButtonGroups(); selectedId = null; saveLayout(); onSelectionChanged?.invoke(null); invalidate()
    }

    override fun onTouchEvent(e: MotionEvent): Boolean {
        if (editMode) return onEditTouch(e)
        when (e.actionMasked) {
            MotionEvent.ACTION_DOWN, MotionEvent.ACTION_POINTER_DOWN -> {
                parent?.requestDisallowInterceptTouchEvent(true)
                val i = e.actionIndex; val id = e.getPointerId(i); val y = e.getY(i)
                touchPoints[id] = PointF(e.getX(i), y)
                val hit = hit(e.getX(i), y)
                when (hit?.kind) {
                    ControlKind.BUTTON -> buttonPointers[id] = hit.bit
                    ControlKind.BRAKE -> brake.down(id, y)
                    ControlKind.THROTTLE -> throttle.down(id, y)
                    null -> if (inLookStick(e.getX(i), y) && lookStick.down(id)) moveLookStick(id, e.getX(i), y)
                }
            }
            MotionEvent.ACTION_MOVE -> for (i in 0 until e.pointerCount) {
                val id = e.getPointerId(i)
                touchPoints[id]?.set(e.getX(i), e.getY(i))
                brake.move(id, e.getY(i)); throttle.move(id, e.getY(i))
                moveLookStick(id, e.getX(i), e.getY(i))
            }
            MotionEvent.ACTION_UP, MotionEvent.ACTION_POINTER_UP -> {
                val id = e.getPointerId(e.actionIndex); brake.up(id); throttle.up(id); buttonPointers.remove(id)
                touchPoints.remove(id)
                lookStick.up(id)
                if (e.actionMasked == MotionEvent.ACTION_UP) {
                    parent?.requestDisallowInterceptTouchEvent(false); performClick()
                }
            }
            MotionEvent.ACTION_CANCEL -> { parent?.requestDisallowInterceptTouchEvent(false); cancelAll() }
        }
        emit(); invalidate(); return true
    }

    private fun onEditTouch(e: MotionEvent): Boolean {
        when (e.actionMasked) {
            MotionEvent.ACTION_DOWN -> {
                val box = hit(e.x, e.y); selectedId = box?.id; editPointer = e.getPointerId(0)
                if (box != null) { dragOffsetX = e.x / max(1, width) - box.x; dragOffsetY = e.y / max(1, height) - box.y }
                onSelectionChanged?.invoke(box?.label)
            }
            MotionEvent.ACTION_MOVE -> {
                val pointer = editPointer ?: return true; val index = e.findPointerIndex(pointer)
                val box = boxes.firstOrNull { it.id == selectedId }
                if (index >= 0 && box != null) {
                    box.x = e.getX(index) / max(1, width) - dragOffsetX
                    box.y = e.getY(index) / max(1, height) - dragOffsetY
                    clampBox(box)
                }
            }
            MotionEvent.ACTION_UP, MotionEvent.ACTION_CANCEL -> {
                if (e.actionMasked == MotionEvent.ACTION_UP) performClick()
                editPointer = null; saveLayout()
            }
        }
        invalidate(); return true
    }

    private fun hit(x: Float, y: Float) = boxes.asReversed().firstOrNull { it.rect(width, height).contains(x, y) }
    override fun performClick(): Boolean { super.performClick(); return true }
    private fun buttonsMask(): UShort = buttonPointers.values.fold(0) { mask, bit -> mask or bit }.toUShort()
    private fun emit() = onValues?.invoke(brake.value, throttle.value, buttonsMask())

    fun cancelAll() { brake.cancel(); throttle.cancel(); lookStick.cancel(); buttonPointers.clear(); touchPoints.clear(); emit(); invalidate() }

    override fun onDraw(canvas: Canvas) {
        super.onDraw(canvas)
        drawLookStick(canvas)
        boxes.forEach { box ->
            val rect = box.rect(width, height)
            when (box.kind) {
                ControlKind.BRAKE, ControlKind.THROTTLE -> drawPedal(canvas, box, rect)
                ControlKind.BUTTON -> drawButton(canvas, box, rect)
            }
            if (editMode && box.id == selectedId) {
                paint.style = Paint.Style.STROKE; paint.strokeWidth = 4f * resources.displayMetrics.density; paint.color = Color.rgb(255, 210, 70)
                canvas.drawRoundRect(rect, 16f, 16f, paint); paint.style = Paint.Style.FILL
            }
        }
        drawSteeringIndicator(canvas)
    }

    private fun drawPedal(canvas: Canvas, box: ControlBox, rect: RectF) {
        val value = if (box.kind == ControlKind.BRAKE) brake.value else throttle.value
        val base = if (box.kind == ControlKind.BRAKE) Color.rgb(165, 56, 65) else Color.rgb(35, 145, 82)
        paint.color = Color.rgb(31, 38, 45); canvas.drawRect(rect, paint)
        val fill = RectF(rect.left, rect.bottom - rect.height() * value, rect.right, rect.bottom)
        paint.color = base; canvas.drawRect(fill, paint)
        paint.color = Color.argb(45, 255, 255, 255)
        canvas.drawRect(rect.left, rect.top, rect.right, rect.top + rect.height() * PedalResponse.EDGE_MARGIN, paint)
        canvas.drawRect(rect.left, rect.bottom - rect.height() * PedalResponse.EDGE_MARGIN, rect.right, rect.bottom, paint)
        paint.color = Color.LTGRAY; paint.textAlign = Paint.Align.CENTER
        paint.textSize = TypedValue.applyDimension(TypedValue.COMPLEX_UNIT_SP, 13f, resources.displayMetrics)
        canvas.drawText("Top 10% · full", rect.centerX(), rect.top + rect.height() * .07f, paint)
        canvas.drawText("Bottom 10% · zero", rect.centerX(), rect.bottom - rect.height() * .035f, paint)
        paint.color = Color.WHITE; paint.textAlign = Paint.Align.CENTER
        paint.textSize = TypedValue.applyDimension(TypedValue.COMPLEX_UNIT_SP, 25f, resources.displayMetrics)
        canvas.drawText(box.label, rect.centerX(), rect.centerY() - paint.textSize * .65f, paint)
        canvas.drawText("${(value * 100).roundToInt()}%", rect.centerX(), rect.centerY() + paint.textSize * .65f, paint)
        // Show Android's actual contact centroid, not an inferred fingertip edge.
        val pedal = if (box.kind == ControlKind.BRAKE) brake else throttle
        val point = pedal.pointerId?.let(touchPoints::get)
        if (point != null) {
            val y = point.y.coerceIn(rect.top, rect.bottom)
            paint.color = Color.WHITE; paint.strokeWidth = 2f * resources.displayMetrics.density
            canvas.drawLine(rect.left + 8f, y, rect.right - 8f, y, paint)
            paint.style = Paint.Style.STROKE
            canvas.drawCircle(point.x.coerceIn(rect.left + 14f, rect.right - 14f), y, 12f, paint)
            paint.style = Paint.Style.FILL
            paint.textSize = TypedValue.applyDimension(TypedValue.COMPLEX_UNIT_SP, 12f, resources.displayMetrics)
            val rawPercent = ((1f - point.y / height).coerceIn(0f, 1f) * 100).roundToInt()
            canvas.drawText("Touch position $rawPercent%", rect.centerX(), rect.top + rect.height() * .84f, paint)
        }
    }

    private fun drawButton(canvas: Canvas, box: ControlBox, rect: RectF) {
        val pressed = buttonPointers.values.any { it == box.bit }
        paint.color = if (pressed) Color.rgb(80, 190, 220) else Color.rgb(48, 59, 69)
        val radius = minOf(rect.width(), rect.height()) * .5f
        canvas.drawRoundRect(rect, radius, radius, paint)
        paint.color = Color.WHITE; paint.textAlign = Paint.Align.CENTER
        paint.textSize = minOf(TypedValue.applyDimension(TypedValue.COMPLEX_UNIT_SP, 18f, resources.displayMetrics), rect.height() * .38f)
        canvas.drawText(box.label, rect.centerX(), rect.centerY() - (paint.ascent() + paint.descent()) / 2f, paint)
    }

    private fun lookRadius() = minOf(width * .043f, height * .14f)
    private fun inLookStick(x: Float, y: Float): Boolean =
        kotlin.math.hypot(x - width * .5f, y - height * .81f) <= lookRadius()

    private fun moveLookStick(id: Int, x: Float, y: Float) {
        val travel = lookRadius() * .52f
        if (travel > 0) lookStick.move(id, (x - width * .5f) / travel, (y - height * .81f) / travel)
    }

    private fun drawLookStick(canvas: Canvas) {
        val cx = width * .5f; val cy = height * .81f; val radius = lookRadius()
        paint.style = Paint.Style.FILL; paint.color = Color.rgb(25, 40, 46)
        canvas.drawCircle(cx, cy, radius, paint)
        paint.style = Paint.Style.STROKE; paint.strokeWidth = 2f * resources.displayMetrics.density
        paint.color = Color.rgb(75, 115, 120); canvas.drawCircle(cx, cy, radius, paint)
        paint.style = Paint.Style.FILL; paint.textAlign = Paint.Align.CENTER
        paint.textSize = TypedValue.applyDimension(TypedValue.COMPLEX_UNIT_SP, 11f, resources.displayMetrics)
        paint.color = Color.LTGRAY
        canvas.drawText("LOOK · R STICK", cx, cy - radius - paint.textSize * .6f, paint)
        canvas.drawText("L", cx - radius * .76f, cy + paint.textSize * .35f, paint)
        canvas.drawText("R", cx + radius * .76f, cy + paint.textSize * .35f, paint)
        canvas.drawText("BACK", cx, cy + radius * .84f, paint)
        paint.color = if (lookStick.pointerId != null) Color.rgb(90, 215, 195) else Color.rgb(93, 130, 139)
        canvas.drawCircle(cx + lookStick.x * radius * .52f, cy + lookStick.y * radius * .52f, radius * .35f, paint)
    }

    private fun drawSteeringIndicator(canvas: Canvas) {
        val left = width * .49f; val right = width * .69f; val y = height * .255f
        val center = (left + right) / 2f
        paint.strokeWidth = 3f * resources.displayMetrics.density; paint.color = Color.rgb(75, 88, 100)
        canvas.drawLine(left, y, right, y, paint); canvas.drawLine(center, y - 9f, center, y + 9f, paint)
        paint.color = if (editMode) Color.rgb(255, 210, 70) else Color.rgb(90, 215, 195)
        val x = center + steering * (right - left) / 2f
        canvas.drawCircle(x, y, 7f * resources.displayMetrics.density, paint)
        paint.textAlign = Paint.Align.CENTER
        paint.textSize = TypedValue.applyDimension(TypedValue.COMPLEX_UNIT_SP, 13f, resources.displayMetrics)
        canvas.drawText("Steering ${steeringAngle.toInt()}° · ${(steering * 100).toInt()}%", width * .365f, y - (paint.ascent() + paint.descent()) / 2f, paint)
    }

    private fun saveLayout() {
        val array = JSONArray()
        boxes.forEach { b -> array.put(JSONObject().put("id", b.id).put("x", b.x).put("y", b.y).put("w", b.w).put("h", b.h)) }
        prefs.edit().putString(LAYOUT_KEY, array.toString()).apply()
    }

    private fun loadLayout(): MutableList<ControlBox> {
        val result = defaults(); val saved = prefs.getString(LAYOUT_KEY, null) ?: return result
        runCatching {
            val array = JSONArray(saved)
            for (i in 0 until array.length()) {
                val value = array.getJSONObject(i); val box = result.firstOrNull { it.id == value.getString("id") } ?: continue
                box.x = value.getDouble("x").toFloat(); box.y = value.getDouble("y").toFloat()
                box.w = value.getDouble("w").toFloat(); box.h = value.getDouble("h").toFloat(); box.clamp()
            }
        }
        val left = result.first { it.kind == ControlKind.BRAKE }.w + ButtonGroupSpacing.PEDAL_GAP
        val right = 1f - result.first { it.kind == ControlKind.THROTTLE }.w - ButtonGroupSpacing.PEDAL_GAP
        result.forEach { it.clamp(left, right) }
        return result
    }
}
