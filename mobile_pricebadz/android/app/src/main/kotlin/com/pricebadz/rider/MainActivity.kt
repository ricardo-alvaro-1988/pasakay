package com.pricebadz.rider

import android.Manifest
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.os.PowerManager
import android.provider.Settings
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
