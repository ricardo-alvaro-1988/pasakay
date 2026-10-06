package com.trygoride.passenger

import android.Manifest
import android.app.AlarmManager
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.provider.Settings
import androidx.core.app.ActivityCompat
import androidx.core.content.ContextCompat
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel
import org.json.JSONArray

class MainActivity : FlutterActivity() {
    private val channelName = "yapasakay.passenger/alerts"

    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, channelName).setMethodCallHandler { call, result ->
            try {
                when (call.method) {
                    "syncPickupAlarms" -> {
                        val raw = call.arguments as? String ?: "[]"
                        val items = mutableListOf<PickupAlarm.Item>()
                        val array = JSONArray(raw)
                        for (i in 0 until array.length()) {
                            val obj = array.optJSONObject(i) ?: continue
                            val id = obj.optString("id")
                            val at = obj.optLong("at", 0)
                            if (id.isBlank() || at <= 0) continue
                            items.add(
                                PickupAlarm.Item(
                                    id,
                                    obj.optString("title"),
                                    obj.optString("body"),
                                    at,
                                ),
                            )
                        }
                        val scheduled = PickupAlarm.sync(this, items)
                        if (scheduled) {
                            requestExactAlarm()
                            requestNotifyPermission()
                        }
                        result.success(scheduled)
                    }
                    "pingNotice" -> {
                        val title = call.argument<String>("title") ?: "Pickup in 10 minutes"
                        val body = call.argument<String>("body") ?: "Your scheduled ride is soon."
                        PickupAlarm.notifyNow(this, title, body)
                        result.success(true)
                    }
                    "requestNotify" -> result.success(requestNotifyPermission())
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
            val prefs = getSharedPreferences("yp_passenger_pickup", MODE_PRIVATE)
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
                ActivityCompat.requestPermissions(this, arrayOf(Manifest.permission.POST_NOTIFICATIONS), 72)
                return false
            }
        }
        return true
    }
}
