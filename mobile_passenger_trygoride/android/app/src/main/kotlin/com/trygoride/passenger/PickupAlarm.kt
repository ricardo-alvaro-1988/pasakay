package com.trygoride.passenger

import android.app.AlarmManager
import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.os.Build
import org.json.JSONArray
import org.json.JSONObject

object PickupAlarm {
    private const val PREFS = "yp_passenger_pickup"
    private const val KEY = "items"
    private const val CHANNEL = "trygoride_pickup_alarm"

    data class Item(val id: String, val title: String, val body: String, val atUtcMs: Long)

    fun sync(context: Context, items: List<Item>): Boolean {
        val app = context.applicationContext
        val prefs = app.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
        cancelStored(app, prefs.getString(KEY, null))
        val stored = JSONArray()
        val now = System.currentTimeMillis()
        for (item in items) {
            if (item.id.isBlank() || item.atUtcMs <= now - 30_000) continue
            schedule(app, item)
            stored.put(
                JSONObject()
                    .put("id", item.id)
                    .put("title", item.title)
                    .put("body", item.body)
                    .put("at", item.atUtcMs),
            )
        }
        prefs.edit().putString(KEY, stored.toString()).apply()
        return stored.length() > 0
    }

    fun restore(context: Context) {
        val raw = context.applicationContext
            .getSharedPreferences(PREFS, Context.MODE_PRIVATE)
            .getString(KEY, null) ?: return
        for (item in parse(raw)) {
            schedule(context.applicationContext, item)
        }
    }

    fun notifyNow(context: Context, title: String, body: String) {
        val app = context.applicationContext
        ensureChannel(app)
        val launch = Intent(app, MainActivity::class.java)
            .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_SINGLE_TOP or Intent.FLAG_ACTIVITY_CLEAR_TOP)
        val open = PendingIntent.getActivity(
            app,
            41,
            launch,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
        )
        val notification = if (Build.VERSION.SDK_INT >= 26) {
            Notification.Builder(app, CHANNEL)
        } else {
            @Suppress("DEPRECATION")
            Notification.Builder(app)
        }
            .setSmallIcon(android.R.drawable.ic_lock_idle_alarm)
            .setContentTitle(title)
            .setContentText(body)
            .setAutoCancel(true)
            .setContentIntent(open)
            .build()
        app.getSystemService(NotificationManager::class.java).notify(4101, notification)
    }

    private fun schedule(context: Context, item: Item) {
        val alarm = context.getSystemService(AlarmManager::class.java) ?: return
        val pending = pending(context, item)
        val whenMs = maxOf(item.atUtcMs, System.currentTimeMillis() + 2_000)
        try {
            val exactOk = Build.VERSION.SDK_INT < 31 || alarm.canScheduleExactAlarms()
            if (exactOk) {
                alarm.setExactAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, whenMs, pending)
            } else {
                alarm.setAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, whenMs, pending)
            }
        } catch (_: Throwable) {
            try {
                alarm.setAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, whenMs, pending)
            } catch (_: Throwable) {
            }
        }
    }

    private fun cancelStored(context: Context, raw: String?) {
        if (raw.isNullOrBlank()) return
        val alarm = context.getSystemService(AlarmManager::class.java) ?: return
        for (item in parse(raw)) {
            try {
                alarm.cancel(pending(context, item))
            } catch (_: Throwable) {
            }
        }
    }

    private fun pending(context: Context, item: Item): PendingIntent {
        val intent = Intent(context, PickupAlarmReceiver::class.java)
            .putExtra("id", item.id)
            .putExtra("title", item.title)
            .putExtra("body", item.body)
        return PendingIntent.getBroadcast(
            context,
            item.id.hashCode(),
            intent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
        )
    }

    private fun parse(raw: String): List<Item> {
        return try {
            val array = JSONArray(raw)
            buildList {
                for (i in 0 until array.length()) {
                    val obj = array.optJSONObject(i) ?: continue
                    val id = obj.optString("id")
                    val at = obj.optLong("at", 0)
                    if (id.isBlank() || at <= 0) continue
                    add(Item(id, obj.optString("title"), obj.optString("body"), at))
                }
            }
        } catch (_: Throwable) {
            emptyList()
        }
    }

    private fun ensureChannel(context: Context) {
        if (Build.VERSION.SDK_INT < 26) return
        val nm = context.getSystemService(NotificationManager::class.java)
        nm.createNotificationChannel(
            NotificationChannel(CHANNEL, "Pickup reminders", NotificationManager.IMPORTANCE_HIGH).apply {
                description = "Alarms 10 minutes before a scheduled pickup."
                enableVibration(true)
            },
        )
    }
}

class PickupAlarmReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        val title = intent.getStringExtra("title")?.ifBlank { null } ?: "Pickup in 10 minutes"
        val body = intent.getStringExtra("body")?.ifBlank { null } ?: "Your scheduled ride is soon."
        PickupAlarm.notifyNow(context, title, body)
    }
}

class PickupBootReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        if (intent.action == Intent.ACTION_BOOT_COMPLETED) {
            PickupAlarm.restore(context)
        }
    }
}
