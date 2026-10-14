package dev.phonewheel

import android.app.Instrumentation
import android.os.Bundle
import android.os.SystemClock
import android.view.MotionEvent

/** Detached UI only: no controller client, receiver, microphone or game output. */
internal object ControllerSurfaceTest {
    fun run(test: Instrumentation) {
        val result=Bundle()
        try {
            test.runOnMainSync {
                val view=PedalTouchView(test.targetContext)
                view.layout(0,0,2400,1080)
                var brake=0f; var throttle=0f; var buttons=0
                val voice=mutableListOf<Boolean>()
                // Disabled, detached voice object: verifies the real LB arbitration
                // callback without opening a microphone, socket or gamepad.
                val radio=WendyVoice(android.app.Activity()) { _,_,_ -> }
                view.onValues={ b,t,k -> brake=b; throttle=t; buttons=k.toInt() }
                view.onPtt={ voice.add(it); radio.gameRadioButton(it) }
                val down=SystemClock.uptimeMillis()
                fun send(action:Int, points:List<Pair<Float,Float>>, index:Int=0) {
                    val properties=points.indices.map { i -> MotionEvent.PointerProperties().apply { id=i; toolType=1 } }.toTypedArray()
                    val coords=points.map { p -> MotionEvent.PointerCoords().apply { x=p.first*view.width; y=p.second*view.height; pressure=1f; size=1f } }.toTypedArray()
                    val e=MotionEvent.obtain(down,SystemClock.uptimeMillis(),action or (index shl 8),points.size,properties,coords,0,0,1f,1f,0,0,android.view.InputDevice.SOURCE_TOUCHSCREEN,0)
                    try { view.dispatchTouchEvent(e) } finally { e.recycle() }
                }
                val held=listOf(.1f to .5f,.9f to .5f)
                send(MotionEvent.ACTION_DOWN,held.take(1))
                send(MotionEvent.ACTION_POINTER_DOWN,held,1)
                fun maintained() { check(brake in .49f.. .51f && throttle in .49f.. .51f) { "Held pedal interrupted" } }
                for(p in listOf(.5f to .968f,.5f to .53f)) {
                    send(MotionEvent.ACTION_POINTER_DOWN,held+p,2); maintained()
                    check(buttons==0 && voice.isEmpty()) { "Removed UI target still active" }
                    send(MotionEvent.ACTION_POINTER_UP,held+p,2); maintained()
                }
                val boxes=PedalTouchView::class.java.getDeclaredField("boxes").apply { isAccessible=true }.get(view) as List<*>
                var checked=0
                for(box in boxes.filterNotNull()) {
                    fun field(name:String)=box.javaClass.getDeclaredField(name).apply { isAccessible=true }.get(box)
                    val bit=field("bit") as Int
                    if(bit==0) continue
                    val p=((field("x") as Float)+(field("w") as Float)/2) to ((field("y") as Float)+(field("h") as Float)/2)
                    send(MotionEvent.ACTION_POINTER_DOWN,held+p,2);maintained();check(buttons==bit) { "Original button mapping changed" }
                    send(MotionEvent.ACTION_POINTER_UP,held+p,2);maintained();check(buttons==0)
                    checked++
                }
                check(checked==12 && voice==listOf(true,false)) { "Gamepad buttons or LB PTT edges incorrect" }
                check(radio.voiceState=="GAME RADIO" && radio.recognitionStarts==0) { "LB must suspend Wendy, not start PTT" }
                send(MotionEvent.ACTION_POINTER_DOWN,held+(.5f to .81f),2)
                send(MotionEvent.ACTION_MOVE,held+(.52f to .81f));maintained();check(view.lookX>0)
                send(MotionEvent.ACTION_POINTER_UP,held+(.52f to .81f),2);check(view.lookX==0f)
                send(MotionEvent.ACTION_CANCEL,held);check(brake==0f && throttle==0f && buttons==0)
                radio.close()
                result.putInt("originalButtonsVerified",checked)
                result.putString("result","PASS both held pedals; 12 gamepad buttons; LB pauses Wendy without mic start; right stick; removed targets inert")
            }
            test.finish(0,result)
        } catch(ex:Throwable) { result.putString("failure",ex.message ?: ex.javaClass.simpleName);test.finish(1,result) }
    }
}
