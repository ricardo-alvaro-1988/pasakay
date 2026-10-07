package com.yapasakay.rider

import android.app.AlarmManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.os.Build
import org.json.JSONArray
import org.json.JSONObject
import java.time.ZoneId
import java.time.ZonedDateTime

object NoticeScheduler {
    private const val PREFS = "yp_notice_alarms"
    private const val KEY = "items"
    private val zone: ZoneId = ZoneId.of("Asia/Manila")

    data class Item(val id: String, val title: String, val body: String, val minuteOfDay: Int)

    fun sync(context: Context, items: List<Item>): Boolean {
        val app = context.applicationContext
        val prefs = app.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
        cancelStored(app, prefs.getString(KEY, null))
        val stored = JSONArray()
        for (item in items) {
            if (item.id.isBlank() || item.minuteOfDay !in 0..1439) continue
            schedule(app, item)
            stored.put(
                JSONObject()
                    .put("id", item.id)
                    .put("title", item.title)
                    .put("body", item.body)
                    .put("minute", item.minuteOfDay),
            )
        }
        prefs.edit().putString(KEY, stored.toString()).apply()
        return stored.length() > 0
    }

    fun restore(context: Context) {
        val raw = context.applicationContext
            .getSharedPreferences(PREFS, Context.MODE_PRIVATE)
            .getString(KEY, null) ?: return
        val items = parse(raw)
        for (item in items) {
            schedule(context.applicationContext, item)
        }
    }

    fun onFired(context: Context, id: String) {
        val raw = context.applicationContext
            .getSharedPreferences(PREFS, Context.MODE_PRIVATE)
            .getString(KEY, null) ?: return
        val match = parse(raw).firstOrNull { it.id == id } ?: return
        schedule(context.applicationContext, match)
    }

    private fun schedule(context: Context, item: Item) {
        val alarm = context.getSystemService(AlarmManager::class.java) ?: return
        val pending = pending(context, item)
        val whenMs = nextTriggerMillis(item.minuteOfDay)
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
        val intent = Intent(context, NoticeAlarmReceiver::class.java)
            .putExtra("id", item.id)
            .putExtra("title", item.title)
            .putExtra("body", item.body)
        val flags = PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        return PendingIntent.getBroadcast(context, item.id.hashCode(), intent, flags)
    }

    private fun nextTriggerMillis(minuteOfDay: Int): Long {
        val now = ZonedDateTime.now(zone)
        var next = now
            .withHour(minuteOfDay / 60)
            .withMinute(minuteOfDay % 60)
            .withSecond(0)
            .withNano(0)
        if (!next.isAfter(now.plusSeconds(30))) {
            next = next.plusDays(1)
        }
        return next.toInstant().toEpochMilli()
    }

    private fun parse(raw: String): List<Item> {
        return try {
            val array = JSONArray(raw)
            buildList {
                for (i in 0 until array.length()) {
                    val obj = array.optJSONObject(i) ?: continue
                    val id = obj.optString("id")
                    val minute = obj.optInt("minute", -1)
                    if (id.isBlank() || minute !in 0..1439) continue
                    add(Item(id, obj.optString("title"), obj.optString("body"), minute))
                }
            }
        } catch (_: Throwable) {
            emptyList()
        }
    }

    private const val PICKUP_KEY = "pickup_items"

    data class PickupItem(val id: String, val title: String, val body: String, val atUtcMs: Long)

    fun syncPickup(context: Context, items: List<PickupItem>): Boolean {
        val app = context.applicationContext
        val prefs = app.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
        cancelPickupStored(app, prefs.getString(PICKUP_KEY, null))
        val stored = JSONArray()
        val now = System.currentTimeMillis()
        for (item in items) {
            if (item.id.isBlank() || item.atUtcMs <= now - 30_000) continue
            schedulePickup(app, item)
            stored.put(
                JSONObject()
                    .put("id", item.id)
                    .put("title", item.title)
                    .put("body", item.body)
                    .put("at", item.atUtcMs),
            )
        }
        prefs.edit().putString(PICKUP_KEY, stored.toString()).apply()
        return stored.length() > 0
    }

    fun restorePickup(context: Context) {
        val raw = context.applicationContext
            .getSharedPreferences(PREFS, Context.MODE_PRIVATE)
            .getString(PICKUP_KEY, null) ?: return
        for (item in parsePickup(raw)) {
            schedulePickup(context.applicationContext, item)
        }
    }

    private fun schedulePickup(context: Context, item: PickupItem) {
        val alarm = context.getSystemService(AlarmManager::class.java) ?: return
        val pending = pickupPending(context, item)
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

    private fun cancelPickupStored(context: Context, raw: String?) {
        if (raw.isNullOrBlank()) return
        val alarm = context.getSystemService(AlarmManager::class.java) ?: return
        for (item in parsePickup(raw)) {
            try {
                alarm.cancel(pickupPending(context, item))
            } catch (_: Throwable) {
            }
        }
    }

    private fun pickupPending(context: Context, item: PickupItem): PendingIntent {
        val intent = Intent(context, NoticeAlarmReceiver::class.java)
            .putExtra("id", item.id)
            .putExtra("title", item.title)
            .putExtra("body", item.body)
        val flags = PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        return PendingIntent.getBroadcast(context, item.id.hashCode(), intent, flags)
    }

    private fun parsePickup(raw: String): List<PickupItem> {
        return try {
            val array = JSONArray(raw)
            buildList {
                for (i in 0 until array.length()) {
                    val obj = array.optJSONObject(i) ?: continue
                    val id = obj.optString("id")
                    val at = obj.optLong("at", 0)
                    if (id.isBlank() || at <= 0) continue
                    add(PickupItem(id, obj.optString("title"), obj.optString("body"), at))
                }
            }
        } catch (_: Throwable) {
            emptyList()
        }
    }
}
