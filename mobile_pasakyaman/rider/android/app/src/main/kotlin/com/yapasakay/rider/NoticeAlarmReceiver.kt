package com.yapasakay.rider

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent

class NoticeAlarmReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        val title = intent.getStringExtra("title")?.ifBlank { null } ?: "Announcement"
        val body = intent.getStringExtra("body")?.ifBlank { null } ?: "Open Ya! Pasakay to read it."
        OnlineService.pingNotice(context, title, body)
        val id = intent.getStringExtra("id") ?: return
        NoticeScheduler.onFired(context, id)
    }
}

class NoticeBootReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        if (intent.action == Intent.ACTION_BOOT_COMPLETED) {
            NoticeScheduler.restore(context)
            NoticeScheduler.restorePickup(context)
        }
    }
}
