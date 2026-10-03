package com.pricebadz.rider

import android.Manifest
import android.app.AlarmManager
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.os.PowerManager
import android.provider.Settings
import org.json.JSONArray
import android.view.WindowManager
import androidx.core.app.ActivityCompat
import androidx.core.content.ContextCompat
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel

class MainActivity : FlutterActivity() {
    private val channelName = "pricebadz.rider/alerts"

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        try {
            if (Build.VERSION.SDK_INT >= 27) {
                setShowWhenLocked(true)
                setTurnScreenOn(true)
            } else {
                @Suppress("DEPRECATION")
                window.addFlags(
                    WindowManager.LayoutParams.FLAG_SHOW_WHEN_LOCKED or
                        WindowManager.LayoutParams.FLAG_TURN_SCREEN_ON or
                        WindowManager.LayoutParams.FLAG_DISMISS_KEYGUARD,
                )
            }
        } catch (_: Throwable) {
        }
    }

    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, channelName).setMethodCallHandler { call, result ->
            try {
                when (call.method) {
                    "startOnline" -> result.success(OnlineService.start(this))
                    "stopOnline" -> {
                        OnlineService.stop(this)
                        result.success(true)
                    }
                    "ringOffer" -> {
                        val title = call.argument<String>("title") ?: "New job offer"
                        val body = call.argument<String>("body") ?: "Open Pricebadz to accept."
                        result.success(OnlineService.ring(this, title, body))
                    }
                    "stopRing" -> {
                        OnlineService.stopRing(this)
                        result.success(true)
                    }
                    "syncNotices" -> {
                        val raw = call.arguments as? String ?: "[]"
                        val items = mutableListOf<NoticeScheduler.Item>()
                        val array = JSONArray(raw)
                        for (i in 0 until array.length()) {
                            val obj = array.optJSONObject(i) ?: continue
                            val id = obj.optString("id")
                            val minute = obj.optInt("minute", -1)
                            if (id.isBlank() || minute !in 0..1439) continue
                            items.add(
                                NoticeScheduler.Item(
                                    id,
                                    obj.optString("title"),
                                    obj.optString("body"),
                                    minute,
                                ),
                            )
                        }
                        val scheduled = NoticeScheduler.sync(this, items)
                        if (scheduled) {
                            requestExactAlarm()
                        }
                        result.success(scheduled)
                    }
                    "pingNotice" -> {
                        val title = call.argument<String>("title") ?: "Announcement"
                        val body = call.argument<String>("body") ?: "Open Pricebadz to read it."
                        result.success(OnlineService.pingNotice(this, title, body))
                    }
                    "pingChat" -> {
                        val title = call.argument<String>("title") ?: "New chat"
                        val body = call.argument<String>("body") ?: "Open Pricebadz to reply."
                        result.success(OnlineService.pingChat(this, title, body))
                    }
                    "requestNotify" -> result.success(requestNotifyPermission())
                    "requestBattery" -> {
                        requestIgnoreBattery()
                        result.success(true)
                    }
                    else -> result.notImplemented()
                }
            } catch (_: Throwable) {
                result.success(false)
            }
        }
    }

    private fun requestExactAlarm() {
        if (Build.VERSION.SDK_INT < 31) return
        try {
            val alarm = getSystemService(AlarmManager::class.java) ?: return
            if (alarm.canScheduleExactAlarms()) return
            val prefs = getSharedPreferences("yp_notice_alarms", MODE_PRIVATE)
            if (prefs.getBoolean("askedExact", false)) return
            prefs.edit().putBoolean("askedExact", true).apply()
            startActivity(
                Intent(Settings.ACTION_REQUEST_SCHEDULE_EXACT_ALARM)
                    .setData(Uri.parse("package:$packageName")),
            )
        } catch (_: Throwable) {
        }
    }

    private fun requestNotifyPermission(): Boolean {
        if (Build.VERSION.SDK_INT >= 33) {
            val granted = ContextCompat.checkSelfPermission(this, Manifest.permission.POST_NOTIFICATIONS) ==
                PackageManager.PERMISSION_GRANTED
            if (!granted) {
                ActivityCompat.requestPermissions(this, arrayOf(Manifest.permission.POST_NOTIFICATIONS), 71)
                return false
            }
        }
        return true
    }

    private fun requestIgnoreBattery() {
        if (Build.VERSION.SDK_INT < 23) return
        try {
            val pm = getSystemService(PowerManager::class.java) ?: return
            if (pm.isIgnoringBatteryOptimizations(packageName)) return
            startActivity(
                Intent(Settings.ACTION_REQUEST_IGNORE_BATTERY_OPTIMIZATIONS).apply {
                    data = Uri.parse("package:$packageName")
                },
            )
        } catch (_: Throwable) {
        }
    }
}
