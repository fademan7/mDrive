package dev.phonewheel

import android.app.Instrumentation
import android.content.Intent
import android.os.Bundle
import android.os.SystemClock
import android.view.MotionEvent
import java.util.concurrent.atomic.AtomicReference

/** Actual Activity lifecycle + Wi-Fi. Explicit null receiver only, no microphone. */
internal object ControllerResumeTest {
    fun run(test: Instrumentation, args: Bundle) {
        val result=Bundle()
        var diagnostics:()->Unit={}
        try {
            require(args.getString("nullBackend")=="true" || args.getString("rumbleRelay")=="true")
            val monitor=test.addMonitor(MainActivity::class.java.name,null,false)
            val launched=test.startActivitySync(Intent(test.targetContext,MainActivity::class.java).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)) as MainActivity
            Thread.sleep(1500)
            var activity=(monitor.lastActivity as? MainActivity)?:launched
            fun field(name:String):Any? {
                // Rotation/locale restoration can recreate more than once.
                // Never keep asserting against a destroyed intermediate screen.
                activity=(monitor.lastActivity as? MainActivity)?:activity
                return MainActivity::class.java.getDeclaredField(name).apply{isAccessible=true}.get(activity)
            }
            diagnostics={
                result.putBoolean("activityDestroyed",activity.isDestroyed)
                result.putBoolean("peerPresent",field("connectedDetails")!=null)
                result.putBoolean("clientPresent",field("client")!=null)
                result.putLong("latestAcks",(field("client") as? UdpControllerClient)?.receivedStatuses?:0)
            }
            fun await(label:String, condition:()->Boolean) {
                val until=SystemClock.elapsedRealtime()+10000
                while(!condition()&&SystemClock.elapsedRealtime()<until)Thread.sleep(25)
                check(condition()){label}
            }
            test.runOnMainSync {
                // Disable optional phone voice for this transport-only test.
                (field("wendy") as WendyVoice).foreground(false)
                MainActivity::class.java.getDeclaredMethod("connect",String::class.java,String::class.java,String::class.java,Int::class.javaPrimitiveType)
                    .apply{isAccessible=true}.invoke(activity,args.getString("host"),args.getString("session"),args.getString("key"),26760)
            }
            await("No authenticated Wi-Fi ACKs"){(field("client") as? UdpControllerClient)?.receivedStatuses?.let{it>10}==true}
            var client=field("client") as UdpControllerClient
            result.putBoolean("initialArmObserved",field("arm")==true)
            val pedals=field("pedals") as PedalTouchView
            if (args.getString("rumbleRelay") == "true") {
                await("Hold upright, level and untouched for initial ARM"){field("arm")==true && field("hostActive")==true}
                val haptics=field("haptics") as HapticScheduler
                val before=client.receivedHaptics
                test.sendStatus(0,Bundle().apply{putBoolean("rumbleReady",true)})
                Thread.sleep(7000)
                result.putLong("receivedHaptics",client.receivedHaptics-before)
                result.putLong("rejectedHaptics",client.rejectedHaptics)
                result.putLong("playedFrames",haptics.gameFramesPlayed)
                result.putInt("levelMask",haptics.observedGameLevels)
                result.putLong("maxSendGapMs",client.maxSendGapMs)
                check(client.receivedHaptics>before+10 && haptics.gameFramesPlayed>10 && haptics.observedGameLevels and 14 == 14){"Xbox -> Wi-Fi -> phone haptic path incomplete"}
                result.putString("result","PASS actual Xbox feedback via receiver and Wi-Fi to phone vibration scheduler")
                test.finish(0,result);return
            }
            if (args.getString("heldRecovery") == "true") {
                await("Phone must be upright, centered and untouched to arm initially"){field("arm")==true && field("hostActive")==true}
                val beforeEpoch=field("epoch")
                test.runOnMainSync {
                    val t=SystemClock.uptimeMillis()
                    val e=MotionEvent.obtain(t,t,MotionEvent.ACTION_DOWN,pedals.width*.9f,pedals.height*.5f,0)
                    try{activity.dispatchTouchEvent(e)}finally{e.recycle()}
                }
                try {
                    check((field("throttle") as Float) in .49f.. .51f)
                    val socket=UdpControllerClient::class.java.getDeclaredField("socket").apply{isAccessible=true}.get(client) as AtomicReference<*>
                    val at=SystemClock.elapsedRealtime();val acks=client.receivedStatuses
                    (socket.get() as AutoCloseable).close()
                    await("Fresh ACK/ARM did not recover while pedal held"){client.receivedStatuses>acks+5 && field("hostActive")==true}
                    check(field("arm")==true && field("epoch")==beforeEpoch && (field("throttle") as Float) in .49f.. .51f){"Recovery changed live input/calibration"}
                    result.putLong("heldSocketRecoveryMs",SystemClock.elapsedRealtime()-at)
                    result.putLong("maxSendGapMs",client.maxSendGapMs)
                    result.putLong("maxStatusGapMs",client.maxStatusGapMs)
                    result.putString("result","PASS actual Wi-Fi held-pedal socket recovery; null backend only")
                } finally {
                    test.runOnMainSync {
                        val t=SystemClock.uptimeMillis();val e=MotionEvent.obtain(t,t,MotionEvent.ACTION_CANCEL,0f,0f,0)
                        try{activity.dispatchTouchEvent(e)}finally{e.recycle()}
                    }
                }
                test.finish(0,result);return
            }
            test.runOnMainSync {
                val now=SystemClock.uptimeMillis()
                val e=MotionEvent.obtain(now,now,MotionEvent.ACTION_DOWN,pedals.width*.9f,pedals.height*.5f,0)
                try{activity.dispatchTouchEvent(e)}finally{e.recycle()}
                check((field("throttle") as Float) in .49f.. .51f){"Held pedal test input missing"}
            }
            test.runOnMainSync {activity.startActivity(Intent(Intent.ACTION_MAIN).addCategory(Intent.CATEGORY_HOME).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))}
            await("Activity did not enter background"){field("foreground")==false}
            check(field("arm")==false && field("throttle")==0f && field("brake")==0f){"Background failed to release input"}
            Thread.sleep(3000)
            val before=client.receivedStatuses
            val resumeAt=SystemClock.elapsedRealtime()
            // Android blocks background Activity launches. The host performs the
            // user-equivalent explicit launch with adb, not a BAL exemption.
            test.sendStatus(0,Bundle().apply{putBoolean("hostResumeRequested",true)})
            val backgroundActivity=activity
            await("Foreground did not return"){
                activity=(monitor.lastActivity as? MainActivity)?:activity
                field("foreground")==true
            }
            val wasRecreated=activity!==backgroundActivity
            if(wasRecreated) {
                await("Pairing not restored after background recreation"){(field("client") as? UdpControllerClient)?.receivedStatuses?.let{it>5}==true}
                client=field("client") as UdpControllerClient
            }else await("ACKs did not resume"){client.receivedStatuses>before+5}
            result.putBoolean("backgroundRecreatedActivity",wasRecreated)
            result.putLong("resumeAckMs",SystemClock.elapsedRealtime()-resumeAt)
            check(field("client")===client){"Normal app switching replaced the whole client"}
            check(field("throttle")==0f && field("brake")==0f){"Old pedal replayed on resume"}
            // Explicit closed-socket fault; original bug kept a broken socket forever.
            val socket=UdpControllerClient::class.java.getDeclaredField("socket").apply{isAccessible=true}.get(client) as AtomicReference<*>
            val faultAt=SystemClock.elapsedRealtime();val faultCount=client.receivedStatuses
            (socket.get() as AutoCloseable).close()
            await("Closed socket did not automatically recover"){client.receivedStatuses>faultCount+10}
            result.putLong("closedSocketRecoveryMs",SystemClock.elapsedRealtime()-faultAt)
            result.putInt("injectedFaultSendFailures",client.sendFailures)
            val old=activity
            test.runOnMainSync {old.recreate()}
            await("Activity not recreated"){monitor.lastActivity!=null && monitor.lastActivity!==old}
            activity=monitor.lastActivity as MainActivity
            await("Pairing not restored after recreation"){(field("client") as? UdpControllerClient)?.receivedStatuses?.let{it>10}==true}
            check(field("throttle")==0f && field("brake")==0f){"Recreation restored old pedal input"}
            result.putBoolean("pairingRestoredAfterRecreation",true)
            result.putBoolean("finalArmObserved",field("arm")==true)
            result.putLong("maxSendGapMsIncludingFault",client.maxSendGapMs)
            result.putLong("maxAckGapMsIncludingFault",client.maxStatusGapMs)
            result.putString("result","PASS actual Wi-Fi, background neutral, foreground ACKs, closed socket recovery and Activity recreation")
            test.finish(0,result)
        }catch(t:Throwable){diagnostics();result.putString("failure",t.cause?.message?:t.message?:t.javaClass.simpleName);test.finish(1,result)}
    }
}
